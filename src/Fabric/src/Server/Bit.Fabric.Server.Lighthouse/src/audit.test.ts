import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { Result } from 'lighthouse';
import { reduce, type AuditFinding } from './audit.ts';

/**
 * The shapes of the audits that failed on a real app made from the template, as Lighthouse 13.6.0 reported them for its
 * home page on a phone: tables and opportunities, insights that are lists of checklists, elements and sections, and
 * document-latency-insight, whose details are a checklist on their own, items keyed by check rather than an array.
 */
function report(): Result {
    return {
        lighthouseVersion: '13.6.0',
        fetchTime: '2026-11-13T06:51:00.000Z',
        finalDisplayedUrl: 'https://app.example/en-US/',
        environment: { benchmarkIndex: 1500 },
        categories: {
            performance: {
                score: 0.65,
                auditRefs: [
                    { id: 'first-contentful-paint', weight: 10, group: 'metrics' },
                    { id: 'document-latency-insight', weight: 0, group: 'insights' },
                    { id: 'lcp-discovery-insight', weight: 0, group: 'insights' },
                    { id: 'network-dependency-tree-insight', weight: 0, group: 'insights' },
                    { id: 'cache-insight', weight: 0, group: 'insights' },
                    { id: 'unused-javascript', weight: 0, group: 'diagnostics' },
                    { id: 'font-display-insight', weight: 0, group: 'insights' }
                ]
            },
            accessibility: {
                score: 0.99,
                auditRefs: [
                    { id: 'heading-order', weight: 3, group: 'a11y-navigation' }
                ]
            }
        },
        audits: {
            'first-contentful-paint': { id: 'first-contentful-paint', score: 0.5, scoreDisplayMode: 'numeric', numericValue: 2950, numericUnit: 'millisecond' },
            'document-latency-insight': {
                id: 'document-latency-insight',
                score: 0,
                scoreDisplayMode: 'metricSavings',
                numericValue: 137,
                numericUnit: 'millisecond',
                metricSavings: { FCP: 150, LCP: 150 },
                details: {
                    type: 'checklist',
                    items: {
                        noRedirects: { label: 'Had redirects (1 redirects, +137 ms)', value: false },
                        serverResponseIsFast: { label: 'Server responds quickly (observed 42 ms)', value: true },
                        usesCompression: { label: 'Applies text compression', value: true }
                    },
                    debugData: { type: 'debugdata', redirectDuration: 137, serverResponseTime: 42 }
                }
            },
            'lcp-discovery-insight': {
                id: 'lcp-discovery-insight',
                score: 0,
                scoreDisplayMode: 'numeric',
                details: {
                    type: 'list',
                    items: [
                        {
                            type: 'checklist',
                            items: {
                                priorityHinted: { label: 'fetchpriority=high should be applied', value: false },
                                requestDiscoverable: { label: 'Request is discoverable in initial document', value: true },
                                eagerlyLoaded: { label: 'LCP resources shouldn’t use loading=lazy', value: true }
                            }
                        },
                        { type: 'node', selector: 'section > img', snippet: '<img src="/images/hero.webp" alt="">' }
                    ]
                }
            },
            'network-dependency-tree-insight': {
                id: 'network-dependency-tree-insight',
                score: 0,
                scoreDisplayMode: 'numeric',
                details: {
                    type: 'list',
                    items: [
                        { type: 'list-section', value: { type: 'network-tree', chains: {}, longestChain: { duration: 1552 } } },
                        {
                            type: 'list-section',
                            title: 'Preconnected origins',
                            value: {
                                type: 'table',
                                headings: [],
                                items: [
                                    { origin: 'https://js.monitor.azure.com/', source: { type: 'node', selector: 'head > link', snippet: '<link href="https://js.monitor.azure.com" rel="preconnect" crossorigin="">' } },
                                    { origin: 'https://www.google.com/', source: { type: 'node', selector: 'head > link', snippet: '<link href="https://www.google.com" rel="preconnect">' } }
                                ]
                            }
                        },
                        { type: 'list-section', value: { type: 'text', value: 'no origins were preconnected' } }
                    ]
                }
            },
            'cache-insight': {
                id: 'cache-insight',
                score: 0,
                scoreDisplayMode: 'metricSavings',
                numericValue: 250945,
                numericUnit: 'byte',
                metricSavings: { FCP: 150, LCP: 150 },
                details: {
                    type: 'table',
                    headings: [],
                    items: [
                        { url: 'https://js.monitor.azure.com/scripts/b/ai.3.gbl.min.js', cacheLifetimeMs: 1800000, totalBytes: 72113, wastedBytes: 62197 },
                        { url: 'https://app.example/_content/Bit.BlazorUI/scripts/bit.blazorui.js', cacheLifetimeMs: 0, totalBytes: 40112, wastedBytes: 40112 }
                    ]
                }
            },
            'unused-javascript': {
                id: 'unused-javascript',
                score: 0,
                scoreDisplayMode: 'metricSavings',
                details: {
                    type: 'opportunity',
                    headings: [],
                    overallSavingsMs: 450,
                    overallSavingsBytes: 120000,
                    items: [{ url: 'https://app.example/_framework/dotnet.native.js', wastedBytes: 120000 }]
                }
            },
            'font-display-insight': {
                id: 'font-display-insight',
                score: 1,
                scoreDisplayMode: 'metricSavings',
                details: { type: 'table', headings: [], items: [] }
            },
            'heading-order': {
                id: 'heading-order',
                score: 0,
                scoreDisplayMode: 'binary',
                details: {
                    type: 'table',
                    headings: [],
                    items: [{ node: { type: 'node', selector: 'section > h4', snippet: '<h4 class="bit-txt-h4 bit-txt">' } }]
                }
            },
            'metrics': { id: 'metrics', score: null, scoreDisplayMode: 'informative', details: { type: 'debugdata', items: [{ timeToFirstByte: 179 }] } }
        }
    } as unknown as Result;
}

function finding(findings: AuditFinding[], id: string): AuditFinding {
    const found = findings.find(f => f.id === id);
    assert.ok(found, `${id} is among the findings`);
    return found;
}

test('Every shape of an audit\'s details is read, a checklist whose items are keyed by check included', () => {
    const result = reduce('https://app.example/', 'mobile', report());

    assert.equal(result.runtimeError, null);
    assert.equal(result.performanceScore, 0.65);
    assert.equal(result.accessibilityScore, 0.99);
    assert.equal(result.metrics.timeToFirstByteMs, 179);
    assert.deepEqual(result.findings.map(f => f.id).sort(), [
        'cache-insight', 'document-latency-insight', 'heading-order', 'lcp-discovery-insight', 'network-dependency-tree-insight', 'unused-javascript'
    ], 'only the failing audits outside the metrics');

    const latency = finding(result.findings, 'document-latency-insight');
    assert.equal(latency.itemCount, 1, 'the one check that failed: the redirect');
    assert.deepEqual(latency.where, []);
    assert.deepEqual(latency.metricSavings, { FCP: 150, LCP: 150 });

    const discovery = finding(result.findings, 'lcp-discovery-insight');
    assert.equal(discovery.itemCount, 2, 'the missing fetchpriority, and the image itself');
    assert.deepEqual(discovery.where, ['<img src="/images/hero.webp" alt="">']);

    const tree = finding(result.findings, 'network-dependency-tree-insight');
    assert.equal(tree.itemCount, 2, 'the rows of the section that is a table; the tree and the text list nothing');
    assert.deepEqual(tree.where, [
        '<link href="https://js.monitor.azure.com" rel="preconnect" crossorigin="">',
        '<link href="https://www.google.com" rel="preconnect">'
    ]);

    const cache = finding(result.findings, 'cache-insight');
    assert.equal(cache.itemCount, 2);
    assert.equal(cache.savingsBytes, 250945);
    assert.deepEqual(cache.where, [
        'https://js.monitor.azure.com/scripts/b/ai.3.gbl.min.js',
        'https://app.example/_content/Bit.BlazorUI/scripts/bit.blazorui.js'
    ]);

    const unused = finding(result.findings, 'unused-javascript');
    assert.equal(unused.savingsMs, 450);
    assert.equal(unused.savingsBytes, 120000);
    assert.deepEqual(unused.where, ['https://app.example/_framework/dotnet.native.js']);

    const headings = finding(result.findings, 'heading-order');
    assert.equal(headings.category, 'accessibility');
    assert.equal(headings.weight, 3);
    assert.deepEqual(headings.where, ['<h4 class="bit-txt-h4 bit-txt">']);
});

test('A page Lighthouse couldn\'t load gives its error and no scores, metrics or findings', () => {
    const failed = { ...report(), runtimeError: { code: 'FAILED_DOCUMENT_REQUEST', message: 'net::ERR_NAME_NOT_RESOLVED' } } as unknown as Result;

    const result = reduce('https://app.example/', 'desktop', failed);

    assert.equal(result.runtimeError, 'FAILED_DOCUMENT_REQUEST');
    assert.equal(result.performanceScore, null);
    assert.equal(result.metrics.largestContentfulPaintMs, null);
    assert.deepEqual(result.findings, []);
});
