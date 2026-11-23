// [mirror] which addresses count as the public internet - keep in sync with:
// - src/Server/Bit.Fabric.Server.Api/Features/Monitoring/Services/PublicAddressGuard.cs (IsPublic)

import { createServer, request as httpRequest, type IncomingMessage, type ServerResponse } from 'node:http';
import { connect, isIP, type Socket } from 'node:net';
import { lookup } from 'node:dns/promises';
import type { Duplex } from 'node:stream';

/**
 * Chrome's only way out (bitfabric-internal/specs/scanners.md, Isolation). Every Chrome this service starts is told to
 * send all its traffic, loopback included, through this proxy on 127.0.0.1, which resolves each host itself, keeps only
 * the public addresses, and connects to that very address, so a name that resolves to a public address for one check and
 * to a private one for the connection can't slip past it, and neither can a redirect, a sub-resource, a fetch or a
 * WebSocket a page makes.
 * Plain http is forwarded, everything else is a CONNECT tunnel; anything that resolves only to loopback, private,
 * link-local (cloud metadata), shared or reserved addresses is refused with 403.
 */
export interface Guard {
    readonly port: number;

    /** How many requests it refused since the last call: a page that reaches for a private address shows here. */
    takeRefusals(): number;

    close(): Promise<void>;
}

const connectTimeoutMs = 10_000;

export async function startGuard(): Promise<Guard> {
    let refusals = 0;

    const server = createServer((request, response) => {
        forward(request, response).catch(error => {
            if (error instanceof Refused) {
                refusals++;
            }
            if (response.headersSent === false) {
                response.writeHead(error instanceof Refused ? 403 : 502, { 'content-type': 'text/plain' });
            }
            response.end();
        });
    });

    server.on('connect', (request: IncomingMessage, client: Duplex, head: Buffer) => {
        tunnel(request, client, head).catch(error => {
            if (error instanceof Refused) {
                refusals++;
            }
            if (client.writable) {
                client.end(`HTTP/1.1 ${error instanceof Refused ? '403 Forbidden' : '502 Bad Gateway'}\r\n\r\n`);
            }
            client.destroy();
        });
    });

    // Chrome keeps its connections to the proxy open between requests; they go when the run's Chrome does.
    server.keepAliveTimeout = 5_000;

    await new Promise<void>(resolve => server.listen(0, '127.0.0.1', resolve));

    const address = server.address();
    if (address === null || typeof address === 'string') {
        throw new Error('The guard has no port.');
    }

    return {
        port: address.port,
        takeRefusals() {
            const count = refusals;
            refusals = 0;
            return count;
        },
        close: () => new Promise<void>(resolve => {
            server.closeAllConnections();
            server.close(() => resolve());
        })
    };
}

/** A request the guard refused: its host isn't on the public internet. */
class Refused extends Error {
}

/** A CONNECT tunnel: https, wss and anything else Chrome sends through a proxy. */
async function tunnel(request: IncomingMessage, client: Duplex, head: Buffer): Promise<void> {
    const { host, port } = parseAuthority(request.url ?? '');

    client.on('error', () => client.destroy());

    const upstream = await connectPublic(host, port);

    upstream.on('error', () => client.destroy());
    client.on('close', () => upstream.destroy());
    upstream.on('close', () => client.destroy());

    client.write('HTTP/1.1 200 Connection Established\r\n\r\n');
    if (head.length > 0) {
        upstream.write(head);
    }
    upstream.pipe(client);
    client.pipe(upstream);
}

/** Plain http: Chrome sends the absolute address, which is requested from the address the guard approved. */
async function forward(request: IncomingMessage, response: ServerResponse): Promise<void> {
    const url = new URL(request.url ?? '');

    if (url.protocol !== 'http:') {
        throw new Refused(`${url.protocol} isn't forwarded.`);
    }

    // An IPv6 address keeps its brackets in a URL's hostname.
    const upstream = await connectPublic(url.hostname.replace(/^\[|\]$/g, ''), Number(url.port || 80));

    const headers = { ...request.headers };
    for (const hopByHop of ['proxy-connection', 'proxy-authorization', 'connection', 'keep-alive', 'te', 'trailer', 'upgrade']) {
        delete headers[hopByHop];
    }

    await new Promise<void>((resolve, reject) => {
        const outgoing = httpRequest({
            createConnection: () => upstream,
            method: request.method,
            path: `${url.pathname}${url.search}`,
            headers: { ...headers, host: url.host, connection: 'close' }
        }, incoming => {
            response.writeHead(incoming.statusCode ?? 502, incoming.statusMessage, incoming.rawHeaders);
            incoming.pipe(response);
            incoming.on('end', resolve);
            incoming.on('error', reject);
        });

        outgoing.on('error', reject);
        request.pipe(outgoing);
    });
}

/** host:port of a CONNECT request, an IPv6 address in brackets included. */
function parseAuthority(authority: string): { host: string; port: number } {
    const match = /^\[?([^\]]+?)\]?:(\d{1,5})$/.exec(authority);
    const port = Number(match?.[2]);

    if (match === null || match[1] === undefined || port < 1 || port > 65535) {
        throw new Refused(`${authority} isn't host:port.`);
    }

    return { host: match[1], port };
}

/**
 * Resolves the host, keeps its public addresses and connects to the first that answers. The address connected to is the
 * one judged, so nothing resolves the name a second time between the check and the connection.
 */
async function connectPublic(host: string, port: number): Promise<Socket> {
    const addresses = isIP(host) !== 0
        ? [host]
        : (await lookup(host, { all: true, order: 'ipv4first' }).catch(() => [])).map(entry => entry.address);

    const allowed = addresses.filter(isPublic);

    if (allowed.length === 0) {
        throw new Refused(`${host} doesn't resolve to a public address.`);
    }

    let lastError: unknown;

    for (const address of allowed) {
        try {
            return await new Promise<Socket>((resolve, reject) => {
                const socket = connect({ host: address, port, noDelay: true });
                const timer = setTimeout(() => socket.destroy(new Error(`${address}:${port} didn't answer.`)), connectTimeoutMs);
                socket.once('connect', () => {
                    clearTimeout(timer);
                    resolve(socket);
                });
                socket.once('error', error => {
                    clearTimeout(timer);
                    reject(error);
                });
            });
        } catch (error) {
            lastError = error;
        }
    }

    throw lastError;
}

/** A unicast address anyone on the internet could reach: none of the private, shared or reserved ranges. */
export function isPublic(address: string): boolean {
    const family = isIP(address.split('%')[0] ?? '');

    if (family === 4) {
        return isPublicV4(address.split('.').map(Number));
    }

    if (family !== 6) {
        return false;
    }

    const bytes = ipv6Bytes(address.split('%')[0] ?? '');

    // ::ffff:0:0/96 carries an IPv4 address; judge that one.
    if (bytes.slice(0, 10).every(b => b === 0) && bytes[10] === 0xff && bytes[11] === 0xff) {
        return isPublicV4(bytes.slice(12));
    }

    const first = bytes[0] ?? 0;
    const second = bytes[1] ?? 0;

    if (bytes.every((b, i) => i === 15 ? b <= 1 : b === 0)    // :: and ::1
        || (first === 0xfe && (second & 0xc0) === 0x80)       // link-local fe80::/10
        || (first === 0xfe && (second & 0xc0) === 0xc0)       // site-local fec0::/10
        || (first & 0xfe) === 0xfc                            // unique local fc00::/7
        || first === 0xff) {                                  // multicast
        return false;
    }

    // 64:ff9b::/96 (NAT64) carries an IPv4 address in its last four bytes; judge that one.
    if (first === 0x00 && second === 0x64 && bytes[2] === 0xff && bytes[3] === 0x9b && bytes.slice(4, 12).every(b => b === 0)) {
        return isPublicV4(bytes.slice(12));
    }

    // 2001:db8::/32 is for documentation only.
    return (first === 0x20 && second === 0x01 && bytes[2] === 0x0d && bytes[3] === 0xb8) === false;
}

function isPublicV4(o: number[]): boolean {
    const [a = 0, b = 0, c = 0] = o;

    return (a === 0 || a === 10 || a === 127                       // "this network", private, loopback
        || (a === 100 && b >= 64 && b <= 127)                     // shared address space (carrier-grade NAT)
        || (a === 169 && b === 254)                               // link-local, where cloud metadata services live
        || (a === 172 && b >= 16 && b <= 31)                      // private
        || (a === 192 && b === 0 && (c === 0 || c === 2))         // IETF protocol assignments, documentation
        || (a === 192 && b === 168)                               // private
        || (a === 198 && (b === 18 || b === 19))                  // benchmarking
        || (a === 198 && b === 51 && c === 100)                   // documentation
        || (a === 203 && b === 0 && c === 113)                    // documentation
        || a >= 224) === false;                                   // multicast, reserved, broadcast
}

/** The 16 bytes of an IPv6 address, :: and a trailing IPv4 part expanded. */
function ipv6Bytes(address: string): number[] {
    let text = address;
    let tail: number[] = [];

    const v4 = /(\d+\.\d+\.\d+\.\d+)$/.exec(text);
    if (v4?.[1] !== undefined) {
        tail = v4[1].split('.').map(Number);
        text = `${text.slice(0, -v4[1].length)}0:0`;
    }

    const [head = '', rest] = text.split('::');
    const left = head === '' ? [] : head.split(':');
    const right = rest === undefined || rest === '' ? [] : rest.split(':');
    const groups = rest === undefined ? left : [...left, ...Array<string>(8 - left.length - right.length).fill('0'), ...right];

    const bytes = groups.flatMap(group => {
        const value = parseInt(group, 16);
        return [(value >> 8) & 0xff, value & 0xff];
    });

    if (tail.length === 4) {
        bytes.splice(12, 4, ...tail);
    }

    return bytes;
}
