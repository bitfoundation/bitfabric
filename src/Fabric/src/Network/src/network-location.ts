import { DurableObject } from 'cloudflare:workers';
import { isLocation, type Location } from './locations';

export interface Env {
    LOCATIONS: DurableObjectNamespace<NetworkLocation>;
    /** bit Fabric's api, such as https://api.bitfabric.co; the deployment sets it with --var. */
    FABRIC_URL: string;
    /** The api's Monitoring:NetworkSecret; the deployment sets it with wrangler secret put. */
    NETWORK_SECRET?: string;
}

/** The outside requests one invocation may make on the Workers free plan: a target, a redirect, bit Fabric and the trace alike. */
const SUBREQUESTS_PER_INVOCATION = 50;

/**
 * Redirects within the app are followed as a visitor's browser follows them, and only the last request is timed (MonitoringHttp
 * in Server.Api). bit Fabric gives the page the front door lands on once its own rounds found it, so there is rarely one.
 */
const MAX_REDIRECTS = 3;

/** Six requests may wait for their headers at once in an invocation, and a seventh would queue and time its wait too. */
const CONCURRENCY = 5;

/** A target that hasn't answered by then didn't answer. Short, so a minute's round fits in its minute. */
const TIMEOUT_MS = 10_000;

/** The data center an object runs in only changes when the object is moved, which starts it again; this is a safety net. */
const COLO_TTL_MS = 10 * 60_000;

const SECRET_HEADER = 'X-Network-Secret';

const USER_AGENT = 'bitFabric-Monitoring/1.0 (+https://bitfabric.co)';

/** A project this location times (NetworkTargetDto in Shared). */
interface Target {
    projectId: string;
    url: string;
    isServerHealth: boolean;
}

/** How it did (NetworkResultDto in Shared). */
interface Result {
    projectId: string;
    at: string;
    responseMs: number | null;
    error: string | null;
    cacheHit: boolean;
}

/** A minute's targets and how far this location got through them. */
interface Round {
    minute: number;
    targets: Target[];
    next: number;
}

/**
 * One location of the bitplatform Network: an object created with that location's hint (src/index.ts), which places it in
 * that area for good. Its alarm runs once a minute, reads the location's targets from bit Fabric, times each one and
 * posts the readings back, naming the data center it really ran in. A minute's targets that don't fit one invocation's
 * outside requests go on in another alarm right away (README.md, Limits).
 */
export class NetworkLocation extends DurableObject<Env> {
    /** In memory only, to keep storage writes to the alarm itself; an evicted object starts the minute over. */
    private round: Round | undefined;
    private colo: { code: string; at: number } | undefined;

    /** The cron trigger's call, every minute: remembers the location and arms the alarm if nothing has. */
    async poke(location: Location): Promise<void> {
        if (await this.ctx.storage.get<Location>('location') !== location) {
            await this.ctx.storage.put('location', location);
        }

        if (await this.ctx.storage.getAlarm() === null) {
            await this.ctx.storage.setAlarm(Date.now());
        }
    }

    override async alarm(): Promise<void> {
        const minute = Math.floor(Date.now() / 60_000);
        let nextAlarm = (minute + 1) * 60_000;

        try {
            const location = await this.ctx.storage.get<Location>('location');
            const fabricUrl = this.env.FABRIC_URL?.replace(/\/+$/, '');
            const secret = this.env.NETWORK_SECRET;

            if (!isLocation(location) || !fabricUrl || !secret) {
                console.warn({ message: 'Not set up: the location, FABRIC_URL or NETWORK_SECRET is missing.', location, hasFabricUrl: !!fabricUrl, hasSecret: !!secret });
                return;
            }

            // One request is kept for posting what this invocation found.
            const budget = new Budget(SUBREQUESTS_PER_INVOCATION - 1);

            if (this.round?.minute !== minute) {
                // A new minute: whatever was left of the last one is dropped rather than timed late.
                this.round = { minute, targets: await this.readTargets(fabricUrl, secret, location, budget), next: 0 };
            }

            const round = this.round;

            if (round.next >= round.targets.length)
                return;

            // Every reading names the data center it ran in, so none is taken without one.
            const colo = await this.findColo(budget);

            if (!colo) {
                round.next = round.targets.length;
                return;
            }

            const results = await this.timeBatch(round, budget);

            if (results.length > 0) {
                await this.postResults(fabricUrl, secret, location, colo, results);
            }

            if (round.next < round.targets.length) {
                // The rest of the minute in another invocation, with a fresh allowance of outside requests.
                nextAlarm = Date.now();
            }
        } catch (error) {
            console.error({ message: 'The round failed.', error: String(error) });
        } finally {
            await this.ctx.storage.setAlarm(nextAlarm);
        }
    }

    private async readTargets(fabricUrl: string, secret: string, location: Location, budget: Budget): Promise<Target[]> {
        budget.take(1);

        const response = await fetch(`${fabricUrl}/api/v1/MonitoringNetwork/Targets?location=${location}`, {
            headers: { [SECRET_HEADER]: secret, 'User-Agent': USER_AGENT },
            signal: AbortSignal.timeout(TIMEOUT_MS)
        });

        if (!response.ok)
            throw new Error(`bit Fabric answered HTTP ${response.status} for the targets of ${location}.`);

        const targets = await response.json<unknown>();

        if (!Array.isArray(targets))
            throw new Error('bit Fabric answered something that is not a list of targets.');

        return targets.filter((target): target is Target =>
            typeof target?.projectId === 'string' && typeof target?.url === 'string' && typeof target?.isServerHealth === 'boolean');
    }

    /** Cloudflare's trace says which data center answered it, which is the one this object runs in. */
    private async findColo(budget: Budget): Promise<string | undefined> {
        if (this.colo && Date.now() - this.colo.at < COLO_TTL_MS)
            return this.colo.code;

        if (!budget.reserve(1))
            return this.colo?.code;

        try {
            const response = await fetch('https://www.cloudflare.com/cdn-cgi/trace', { signal: AbortSignal.timeout(TIMEOUT_MS) });
            const code = /^colo=([A-Za-z0-9]{2,8})$/m.exec(await response.text())?.[1];

            if (code) {
                this.colo = { code: code.toUpperCase(), at: Date.now() };
            }
        } catch (error) {
            console.warn({ message: 'Cloudflare\'s trace didn\'t answer.', error: String(error) });
        }

        return this.colo?.code;
    }

    /**
     * Times as many of the minute's targets as this invocation's outside requests allow, a few at a time. Each target holds
     * the most it can take, its redirects included, until it is done and gives back what it didn't use.
     */
    private async timeBatch(round: Round, budget: Budget): Promise<Result[]> {
        const results: Result[] = [];
        const perTarget = 1 + MAX_REDIRECTS;

        await Promise.all(Array.from({ length: CONCURRENCY }, async () => {
            while (round.next < round.targets.length && budget.reserve(perTarget)) {
                const target = round.targets[round.next++]!;
                const reading = await time(target);
                budget.release(perTarget - reading.requests);
                results.push(reading.result);
            }
        }));

        return results;
    }

    private async postResults(fabricUrl: string, secret: string, location: Location, colo: string, results: Result[]): Promise<void> {
        const response = await fetch(`${fabricUrl}/api/v1/MonitoringNetwork/Results`, {
            method: 'POST',
            headers: { [SECRET_HEADER]: secret, 'Content-Type': 'application/json', 'User-Agent': USER_AGENT },
            body: JSON.stringify({ location, colo, results }),
            signal: AbortSignal.timeout(TIMEOUT_MS)
        });

        if (!response.ok) {
            console.error({ message: `bit Fabric answered HTTP ${response.status} for the readings.`, location, colo, count: results.length });
            return;
        }

        const { accepted } = await response.json<{ accepted?: number }>();

        if (accepted !== results.length) {
            // A project moved off this location during the minute, or was removed.
            console.info({ message: 'bit Fabric kept fewer readings than were sent.', location, colo, sent: results.length, accepted });
        }
    }
}

/**
 * How long a visitor waits for the target: the page the app's home page lands on with a query string no one else uses, so
 * any cache that honors it has to miss, or a static front end's server's /health as is (ProjectProber.TimeFrontDoor in
 * Server.Api does the same from Germany). One request is timed: a redirect within the app is followed and only the request
 * after it counts, with a query string of its own; one to another address is the answer, as bit Fabric approved only this
 * one. Any answer counts, whatever its status: the health report says what's wrong with the app.
 */
async function time(target: Target): Promise<{ result: Result; requests: number }> {
    const at = new Date().toISOString();
    let requests = 0;

    const answer = (responseMs: number | null, error: string | null, cacheHit = false) =>
        ({ result: { projectId: target.projectId, at, responseMs, error, cacheHit }, requests });

    let url: URL;

    try {
        url = new URL(target.url);
    } catch {
        return answer(null, 'not an address');
    }

    // bit Fabric only gives public https addresses it checked when the project was added; anything else is a mistake.
    if (url.protocol !== 'https:')
        return answer(null, 'not https');

    const signal = AbortSignal.timeout(TIMEOUT_MS);

    try {
        while (true) {
            if (!target.isServerHealth) {
                url.searchParams.set('bf', crypto.randomUUID().replaceAll('-', ''));
            }

            requests++;
            const started = Date.now();
            const response = await fetch(url, { redirect: 'manual', signal, headers: { 'User-Agent': USER_AGENT } });
            // The clock of a Worker moves with its I/O, so this is when the headers came.
            const responseMs = Date.now() - started;
            const location = response.headers.get('Location');
            const cacheHit = !target.isServerHealth && isCacheHit(response.headers);

            // Only the headers count, as for a visitor's first byte; the body isn't read.
            await response.body?.cancel();

            if (response.status >= 300 && response.status < 400 && location && requests <= MAX_REDIRECTS) {
                const next = new URL(location, url);

                if (next.origin === url.origin) {
                    url = next;
                    continue;
                }
            }

            return answer(responseMs, null, cacheHit);
        }
    } catch (error) {
        const name = error instanceof Error ? error.name : '';
        return answer(null, name === 'TimeoutError' || name === 'AbortError' ? 'timeout' : String(error).slice(0, 200));
    }
}

/** A cache answered a URL it can't have seen before, so it ignores the query string (bitfabric-internal/specs/monitoring.md, Response time). */
function isCacheHit(headers: Headers): boolean {
    return headers.has('Age')
        || /HIT/i.test(headers.get('cf-cache-status') ?? '')
        || /HIT/i.test(headers.get('X-Cache') ?? '');
}

/** What is left of an invocation's outside requests. */
class Budget {
    constructor(private remaining: number) { }

    /** Holds n requests for something that may take up to n, if they are still there. */
    reserve(n: number): boolean {
        if (this.remaining < n)
            return false;

        this.remaining -= n;
        return true;
    }

    /** Gives back what a reservation didn't use. */
    release(n: number): void {
        this.remaining += n;
    }

    /** Takes n requests that are made whatever is left, such as bit Fabric's own. */
    take(n: number): void {
        this.remaining -= n;
    }
}
