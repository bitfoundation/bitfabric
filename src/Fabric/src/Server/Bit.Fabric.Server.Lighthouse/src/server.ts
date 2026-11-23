import { createServer, type IncomingMessage, type ServerResponse } from 'node:http';
import { execFile } from 'node:child_process';
import { createRequire } from 'node:module';
import { lookup } from 'node:dns/promises';
import { isIP } from 'node:net';
import { audit, AuditTimeout, type AuditResult, type FormFactor } from './audit.ts';
import { isPublic, startGuard } from './guard.ts';

/**
 * bit Fabric's Lighthouse service (bitfabric-internal/specs/scanners.md, PR 11): POST /audit with one address and a form
 * factor, and it answers the performance and accessibility categories' scores, the Core Web Vitals and the failing
 * audits, reduced to what the api maps to its findings. One run at a time, a few more waiting and the rest turned away,
 * each run on a Chrome of its own that is killed afterwards, within a time limit. Both kinds of a project's scans ask for
 * the same pages; the api names the request they belong to (reuseKey), so the second kind reads the first one's run
 * instead of running Chrome again, and a later request never reads an earlier one's.
 */

const port = Number(process.env['PORT'] ?? 3000);
const runTimeoutMs = Number(process.env['LIGHTHOUSE_RUN_TIMEOUT_MS'] ?? 90_000);
const maxWaiting = Number(process.env['LIGHTHOUSE_MAX_WAITING'] ?? 4);
const keepForMs = Number(process.env['LIGHTHOUSE_KEEP_FOR_MS'] ?? 30 * 60_000);
const maxKept = 64;
const chromePath = process.env['CHROME_PATH'] || 'chromium';

const lighthouseVersion: string = createRequire(import.meta.url)('lighthouse/package.json').version;
const chromeVersion = await new Promise<string | null>(resolve => {
    execFile(chromePath, ['--version'], { timeout: 10_000 }, (error, stdout) => resolve(error ? null : stdout.trim()));
});

const guard = await startGuard();

/** The runs asked for, by request, form factor and address, kept a while for the other kind of the same request. */
const kept = new Map<string, { at: number; result: Promise<AuditResult> }>();

let queue: Promise<unknown> = Promise.resolve();
let waiting = 0;

/** One run at a time, in the order they were asked for. */
function enqueue(work: () => Promise<AuditResult>): Promise<AuditResult> {
    waiting++;
    const run = queue.then(work, work);
    queue = run.catch(() => undefined);
    return run.finally(() => waiting--);
}

async function run(url: string, formFactor: FormFactor): Promise<AuditResult> {
    const startedAt = Date.now();
    guard.takeRefusals();

    try {
        const result = { ...await audit(url, formFactor, guard.port, runTimeoutMs, chromePath), refusedRequests: guard.takeRefusals() };
        log({ event: 'audit', url, formFactor, ms: Date.now() - startedAt, performance: result.performanceScore, accessibility: result.accessibilityScore, runtimeError: result.runtimeError, refused: result.refusedRequests });
        return result;
    } catch (error) {
        log({ event: 'audit-failed', url, formFactor, ms: Date.now() - startedAt, error: String(error) });
        throw error;
    }
}

function find(reuseKey: string | null, url: string, formFactor: FormFactor): { result: Promise<AuditResult>; isNew: boolean } {
    const now = Date.now();

    for (const [key, entry] of kept) {
        if (now - entry.at > keepForMs) {
            kept.delete(key);
        }
    }

    const key = reuseKey === null ? null : `${reuseKey}|${formFactor}|${url}`;
    const found = key === null ? undefined : kept.get(key);
    if (found !== undefined) {
        return { result: found.result, isNew: false };
    }

    const result = enqueue(() => run(url, formFactor));

    if (key !== null) {
        kept.set(key, { at: now, result });
        // A run that failed is asked for again next time, not answered from here.
        result.catch(() => kept.delete(key));
        while (kept.size > maxKept) {
            kept.delete(kept.keys().next().value!);
        }
    }

    return { result, isNew: true };
}

/** The address's host must resolve to the public internet before Chrome is started for it; the guard checks every request after. */
async function isPublicHost(host: string): Promise<boolean> {
    const bare = host.replace(/^\[|\]$/g, '');
    const addresses = isIP(bare) !== 0 ? [bare] : (await lookup(bare, { all: true }).catch(() => [])).map(entry => entry.address);
    return addresses.some(isPublic);
}

async function handleAudit(request: IncomingMessage, response: ServerResponse): Promise<void> {
    let body: { url?: unknown; formFactor?: unknown; reuseKey?: unknown };

    try {
        body = JSON.parse(await readBody(request, 4096));
    } catch {
        return send(response, 400, { error: 'BAD_REQUEST', message: 'Send {"url": "https://...", "formFactor": "mobile" | "desktop"}.' });
    }

    const url = typeof body.url === 'string' && URL.canParse(body.url) ? new URL(body.url) : null;
    const formFactor = body.formFactor === 'mobile' || body.formFactor === 'desktop' ? body.formFactor : null;
    const reuseKey = typeof body.reuseKey === 'string' && body.reuseKey.length > 0 && body.reuseKey.length <= 200 ? body.reuseKey : null;

    if (url === null || (url.protocol !== 'https:' && url.protocol !== 'http:') || url.username !== '' || url.password !== '' || formFactor === null) {
        return send(response, 400, { error: 'BAD_REQUEST', message: 'Send {"url": "https://...", "formFactor": "mobile" | "desktop"}.' });
    }

    if (await isPublicHost(url.hostname) === false) {
        return send(response, 422, { error: 'NOT_PUBLIC', message: `${url.hostname} doesn't resolve to a public address.` });
    }

    const reusable = reuseKey !== null && kept.has(`${reuseKey}|${formFactor}|${url.href}`);
    if (reusable === false && waiting > maxWaiting) {
        response.setHeader('Retry-After', String(Math.ceil(runTimeoutMs / 1000)));
        return send(response, 503, { error: 'BUSY', message: `The service is busy: ${waiting} run(s) are waiting or running.` });
    }

    try {
        const { result } = find(reuseKey, url.href, formFactor);
        send(response, 200, await result);
    } catch (error) {
        if (error instanceof AuditTimeout) {
            send(response, 504, { error: 'TIMEOUT', message: error.message });
        } else {
            send(response, 500, { error: 'FAILED', message: String(error) });
        }
    }
}

function readBody(request: IncomingMessage, limit: number): Promise<string> {
    return new Promise((resolve, reject) => {
        let body = '';
        request.setEncoding('utf8');
        request.on('data', chunk => {
            body += chunk;
            if (body.length > limit) {
                reject(new Error('Too large.'));
                request.destroy();
            }
        });
        request.on('end', () => resolve(body));
        request.on('error', reject);
    });
}

function send(response: ServerResponse, status: number, body: unknown): void {
    if (response.headersSent || response.destroyed) {
        return;
    }
    response.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' });
    response.end(JSON.stringify(body));
}

function log(entry: Record<string, unknown>): void {
    console.log(JSON.stringify({ at: new Date().toISOString(), ...entry }));
}

const server = createServer((request, response) => {
    const path = new URL(request.url ?? '/', 'http://localhost').pathname;

    if (request.method === 'GET' && (path === '/health' || path === '/alive')) {
        return send(response, 200, { status: 'ok', lighthouse: lighthouseVersion, chrome: chromeVersion, waiting });
    }

    if (request.method === 'POST' && path === '/audit') {
        handleAudit(request, response).catch(error => send(response, 500, { error: 'FAILED', message: String(error) }));
        return;
    }

    send(response, 404, { error: 'NOT_FOUND' });
});

// A run waits its turn and then takes up to runTimeoutMs, so a request may stay open for minutes.
server.requestTimeout = 0;
server.headersTimeout = 10_000;

server.listen(port, '0.0.0.0', () => log({ event: 'listening', port, lighthouse: lighthouseVersion, chrome: chromeVersion }));

for (const signal of ['SIGTERM', 'SIGINT'] as const) {
    process.on(signal, () => {
        server.close();
        guard.close().finally(() => process.exit(0));
    });
}
