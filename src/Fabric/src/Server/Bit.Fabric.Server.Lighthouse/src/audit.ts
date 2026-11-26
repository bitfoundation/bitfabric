import { spawn } from 'node:child_process';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import lighthouse, { desktopConfig, type Flags, type Result } from 'lighthouse';
import { userAgents } from 'lighthouse/core/config/constants.js';

/** What bit Fabric asks for: one address, as a phone or a computer loads it. */
export type FormFactor = 'mobile' | 'desktop';

/** One failing audit of the two categories, reduced to what bit Fabric maps to its findings (ScanFindings). */
export interface AuditFinding {
    id: string;
    category: 'performance' | 'accessibility';
    /** The audit's group in its category, such as insights, diagnostics or a11y-names-labels. */
    group: string | null;
    /** Its weight in the category's score; accessibility's 10, 7, 3 and 1 follow axe-core's impact. */
    weight: number;
    /** 0 to 1, below 0.9 here. */
    score: number;
    numericValue: number | null;
    numericUnit: string | null;
    /** What fixing it would save of each metric, such as { LCP: 300, FCP: 200 } or { TBT: 3750 }. */
    metricSavings: Record<string, number> | null;
    savingsMs: number | null;
    savingsBytes: number | null;
    /** How many elements, files or requests it lists. */
    itemCount: number;
    /** Up to ten of them: an address, or an element's HTML. */
    where: string[];
}

/** One Lighthouse run of one page, as this service answers it. */
export interface AuditResult {
    requestedUrl: string;
    finalUrl: string | null;
    formFactor: FormFactor;
    lighthouseVersion: string;
    fetchTime: string;
    /** Lighthouse's own code when the page couldn't be loaded or measured, such as FAILED_DOCUMENT_REQUEST or NO_FCP. */
    runtimeError: string | null;
    /** Requests the guard refused because they went to an address that isn't public. */
    refusedRequests: number;
    /** How fast this machine's CPU is by Lighthouse's own benchmark; the simulated metrics scale by it. */
    benchmarkIndex: number | null;
    performanceScore: number | null;
    accessibilityScore: number | null;
    metrics: {
        firstContentfulPaintMs: number | null;
        largestContentfulPaintMs: number | null;
        totalBlockingTimeMs: number | null;
        cumulativeLayoutShift: number | null;
        speedIndexMs: number | null;
        timeToFirstByteMs: number | null;
    };
    findings: AuditFinding[];
}

/** Chrome's flags for a run: headless, through the guard and nowhere else, and with no state of a previous run. */
function chromeFlags(guardPort: number): string[] {
    return [
        '--headless=new',
        // The container is Chrome's sandbox: Docker's default seccomp profile leaves Chrome's own no namespaces to make.
        '--no-sandbox',
        '--disable-dev-shm-usage',
        '--disable-gpu',
        // Every request through the guard, loopback included (<-loopback> takes away the bypass Chrome adds for it), so a
        // page can't reach this container's own ports, the DevTools one among them.
        `--proxy-server=http://127.0.0.1:${guardPort}`,
        '--proxy-bypass-list=<-loopback>',
        // WebRTC's UDP doesn't go through a proxy; with this, it doesn't go at all.
        '--force-webrtc-ip-handling-policy=disable_non_proxied_udp',
        '--webrtc-ip-handling-policy=disable_non_proxied_udp',
        '--disable-background-networking',
        '--disable-component-update',
        '--disable-domain-reliability',
        '--disable-sync',
        '--metrics-recording-only',
        '--no-default-browser-check',
        '--no-first-run',
        '--no-pings'
    ];
}

/** The page's runs are measured but never shown: no screenshots or treemaps to carry back. */
const skipAudits = ['screenshot-thumbnails', 'final-screenshot', 'full-page-screenshot', 'script-treemap-data'];

/**
 * Lighthouse's own user agent for the form factor with "Chrome-Lighthouse" at its end, as PageSpeed Insights sends it.
 * Lighthouse dropped that token from its default, and an app may serve Lighthouse a page of its own by it, as one made
 * from bit Boilerplate does, without the Blazor scripts; without it, a scan would score a page PageSpeed Insights never sees.
 */
export function userAgentFor(formFactor: FormFactor): string {
    return `${userAgents[formFactor]} Chrome-Lighthouse`;
}

/** How Lighthouse runs for bit Fabric, on the Chrome listening on `port`. */
export function flagsFor(formFactor: FormFactor, port: number): Flags {
    return {
        port,
        output: 'json',
        logLevel: 'error',
        onlyCategories: ['performance', 'accessibility'],
        skipAudits,
        disableFullPageScreenshot: true,
        emulatedUserAgent: userAgentFor(formFactor),
        maxWaitForFcp: 20_000,
        maxWaitForLoad: 35_000
    };
}

/** Lighthouse's desktop settings for a computer; its defaults, a phone, otherwise. */
export function configFor(formFactor: FormFactor) {
    return formFactor === 'desktop' ? desktopConfig : undefined;
}

/**
 * One Lighthouse run of performance and accessibility on a Chrome of its own, which is killed with its whole process
 * group afterwards, its profile deleted, whatever happens, a timeout included.
 */
export async function audit(url: string, formFactor: FormFactor, guardPort: number, timeoutMs: number, chromePath: string): Promise<Omit<AuditResult, 'refusedRequests'>> {
    let chrome: Chrome | undefined;
    let timer: NodeJS.Timeout | undefined;

    try {
        chrome = await startChrome(chromePath, chromeFlags(guardPort));

        const run = lighthouse(url, flagsFor(formFactor, chrome.port), configFor(formFactor));
        // Lighthouse rejects once its Chrome is killed; that is the timeout's doing and nobody waits for it any more.
        run.catch(() => undefined);

        const result = await Promise.race([
            run,
            new Promise<never>((_, reject) => {
                timer = setTimeout(() => reject(new AuditTimeout(timeoutMs)), timeoutMs);
            })
        ]);

        if (result === undefined) {
            throw new Error('Lighthouse returned no result.');
        }

        return reduce(url, formFactor, result.lhr);
    } finally {
        clearTimeout(timer);
        await chrome?.kill();
    }
}

interface Chrome {
    port: number;
    kill(): Promise<void>;
}

/**
 * Starts Chrome on a profile of its own in a process group of its own, and waits for its DevTools port. Started here
 * rather than by chrome-launcher, which takes a Linux container on a Windows machine's WSL (Podman's) for WSL itself and
 * gives Chrome a Windows path for its profile.
 */
async function startChrome(chromePath: string, flags: string[]): Promise<Chrome> {
    const profile = await mkdtemp(join(tmpdir(), 'chrome-'));
    const child = spawn(chromePath, [...flags, `--user-data-dir=${profile}`, '--remote-debugging-port=0', 'about:blank'], {
        detached: process.platform !== 'win32',
        stdio: ['ignore', 'ignore', 'pipe'],
        // Its temporary files too, which a killed Chrome leaves behind, go with its profile.
        env: { ...process.env, TMPDIR: profile }
    });

    const kill = async () => {
        if (child.exitCode === null && child.signalCode === null && child.pid !== undefined) {
            const exited = new Promise(resolve => child.once('exit', resolve));
            try {
                // The whole group: the renderers and the GPU and network processes go with the browser.
                process.kill(process.platform === 'win32' ? child.pid : -child.pid, 'SIGKILL');
            } catch {
                // It ended by itself meanwhile.
            }
            await Promise.race([exited, new Promise(resolve => setTimeout(resolve, 5_000))]);
        }
        await rm(profile, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
    };

    try {
        const port = await new Promise<number>((resolve, reject) => {
            let output = '';
            const timer = setTimeout(() => reject(new Error(`Chrome didn't start within 30 s: ${output.slice(-500)}`)), 30_000);
            child.stderr.setEncoding('utf8');
            child.stderr.on('data', (chunk: string) => {
                output += chunk;
                const match = /DevTools listening on ws:\/\/[^\s]+?:(\d+)\//.exec(output);
                if (match?.[1] !== undefined) {
                    clearTimeout(timer);
                    resolve(Number(match[1]));
                }
            });
            child.once('error', error => {
                clearTimeout(timer);
                reject(error);
            });
            child.once('exit', code => {
                clearTimeout(timer);
                reject(new Error(`Chrome exited with ${code}: ${output.slice(-500)}`));
            });
        });

        // Chrome keeps writing to stderr; read on, so it never blocks on a full pipe.
        child.stderr.resume();

        return { port, kill };
    } catch (error) {
        await kill();
        throw error;
    }
}

export class AuditTimeout extends Error {
    constructor(timeoutMs: number) {
        super(`The run took longer than ${timeoutMs} ms.`);
    }
}

/** One Lighthouse report, reduced to what this service answers (exported for audit.test.ts). */
export function reduce(url: string, formFactor: FormFactor, lhr: Result): Omit<AuditResult, 'refusedRequests'> {
    const metricItem = (lhr.audits['metrics']?.details as { items?: Record<string, number | undefined>[] } | undefined)?.items?.[0] ?? {};
    const value = (id: string) => lhr.runtimeError ? null : lhr.audits[id]?.numericValue ?? null;

    const findings: AuditFinding[] = [];

    if (lhr.runtimeError === undefined) {
        for (const category of ['performance', 'accessibility'] as const) {
            for (const ref of lhr.categories[category]?.auditRefs ?? []) {
                const audit = lhr.audits[ref.id];

                if (audit === undefined || audit.score === null || audit.score >= 0.9 || ref.group === 'metrics' || ref.group === 'hidden'
                    || ['binary', 'numeric', 'metricSavings'].includes(audit.scoreDisplayMode) === false) {
                    continue;
                }

                const details = audit.details as Details | undefined;
                const items = rowsOf(details);

                findings.push({
                    id: ref.id,
                    category,
                    group: ref.group ?? null,
                    weight: ref.weight,
                    score: audit.score,
                    numericValue: audit.numericValue ?? null,
                    numericUnit: audit.numericUnit ?? null,
                    metricSavings: audit.metricSavings ? Object.fromEntries(Object.entries(audit.metricSavings).filter((entry): entry is [string, number] => typeof entry[1] === 'number')) : null,
                    savingsMs: details?.overallSavingsMs ?? null,
                    savingsBytes: details?.overallSavingsBytes ?? (audit.numericUnit === 'byte' ? audit.numericValue ?? null : null),
                    itemCount: items.length,
                    where: [...new Set(items.map(describe).filter((where): where is string => where !== null))].slice(0, 10)
                });
            }
        }
    }

    return {
        requestedUrl: url,
        finalUrl: lhr.finalDisplayedUrl ?? null,
        formFactor,
        lighthouseVersion: lhr.lighthouseVersion,
        fetchTime: lhr.fetchTime,
        runtimeError: lhr.runtimeError?.code ?? null,
        benchmarkIndex: lhr.environment?.benchmarkIndex ?? null,
        performanceScore: lhr.runtimeError ? null : lhr.categories['performance']?.score ?? null,
        accessibilityScore: lhr.runtimeError ? null : lhr.categories['accessibility']?.score ?? null,
        metrics: {
            firstContentfulPaintMs: value('first-contentful-paint'),
            largestContentfulPaintMs: value('largest-contentful-paint'),
            totalBlockingTimeMs: value('total-blocking-time'),
            cumulativeLayoutShift: value('cumulative-layout-shift'),
            speedIndexMs: value('speed-index'),
            timeToFirstByteMs: lhr.runtimeError ? null : metricItem['timeToFirstByte'] ?? null
        },
        findings
    };
}

interface Details {
    overallSavingsMs?: number;
    overallSavingsBytes?: number;
}

/**
 * What an audit lists, in whichever of Lighthouse's shapes its details come: the rows of a table or an opportunity, the
 * tables and sections of an insight's list, the failed checks of a checklist (whose items are an object keyed by check,
 * not an array), or one element. Shapes that list nothing, such as a request chain, a network tree or a text, give none.
 */
function rowsOf(details: unknown): Record<string, unknown>[] {
    if (details === null || typeof details !== 'object') {
        return [];
    }

    const detail = details as Record<string, unknown>;
    const items = detail['items'];
    const isRow = (item: unknown): item is Record<string, unknown> => item !== null && typeof item === 'object';

    switch (detail['type']) {
        case 'list':
            return Array.isArray(items) ? items.flatMap(rowsOf) : [];
        case 'list-section':
            return rowsOf(detail['value']);
        case 'checklist':
            // Such as document-latency-insight's { noRedirects: { label, value: false }, serverResponseIsFast: { ... } }.
            return isRow(items) ? Object.values(items).filter(isRow).filter(check => check['value'] === false) : [];
        case 'node':
            return [{ node: detail }];
        default:
            return Array.isArray(items) ? items.filter(isRow) : [];
    }
}

/** What a row is about: its address, or the element's HTML, short enough to read. */
function describe(row: Record<string, unknown>): string | null {
    const node = row['node'] as { snippet?: string; selector?: string } | undefined;
    // A file's address, or the element that asked for something, as the preconnect table names its link.
    const source = row['source'] as { url?: string; type?: string; snippet?: string; selector?: string } | undefined;

    const text = typeof row['url'] === 'string' && /^https?:/.test(row['url']) ? row['url']
        : node?.snippet ?? node?.selector
        ?? (typeof source?.url === 'string' ? source.url : source?.type === 'node' ? source.snippet ?? source.selector ?? null : null);

    return text === null || text === undefined ? null : text.length > 200 ? `${text.slice(0, 199)}…` : text;
}
