using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;

/// <summary>
/// Lighthouse's failing audits as bit Fabric's findings (bitfabric-internal/specs/scanners.md, Decisions, PR 11): each
/// audit id maps to one of ScanFindings' keys, several ids to one key where they mean the same to the customer, including
/// the ids Lighthouse 12 used before its insights replaced them, so an upgrade of the service keeps its findings. A
/// finding is given once per key across every page and device, with the pages it was seen on and up to a dozen of the
/// files or elements Lighthouse listed; an audit bit Fabric has no words for yet becomes PerformanceAuditFailed or
/// AccessibilityAuditFailed with its id.
/// The Core Web Vitals give findings of their own, judged by Google's thresholds on the phone runs, which Google ranks by.
/// </summary>
public static class LighthouseFindings
{
    private const int MaxItems = 12;

    private static readonly CultureInfo invariant = CultureInfo.InvariantCulture;

    /// <summary>What a key's parameter is made of, and whether a page's audits under one key add up or the largest counts.</summary>
    private sealed record Rule(string Key, Func<BrowserScanRun, LighthouseAuditFinding, double?>? Value = null, Func<double, string>? Format = null, bool Adds = true);

    private static readonly Rule imagesTooBig = new(ScanFindings.ImagesTooBig, (_, f) => f.SavingsBytes, KiB);
    private static readonly Rule filesNotCached = new(ScanFindings.FilesNotCached, (_, f) => f.ItemCount, Whole);
    private static readonly Rule renderBlocking = new(ScanFindings.RenderBlocking, (_, f) => SavedMs(f), Whole);
    private static readonly Rule unusedCode = new(ScanFindings.UnusedCode, (_, f) => f.SavingsBytes, KiB);
    private static readonly Rule mainThreadBusy = new(ScanFindings.MainThreadBusy, (_, f) => f.NumericUnit is "millisecond" ? f.NumericValue : null, Seconds, Adds: false);
    private static readonly Rule thirdPartyCode = new(ScanFindings.ThirdPartyCode, (_, f) => f.ItemCount, Whole);
    private static readonly Rule pageSlowToArrive = new(ScanFindings.PageSlowToArrive, (run, _) => run.Audit.Metrics.TimeToFirstByteMs, Whole, Adds: false);
    private static readonly Rule lcpImageFoundLate = new(ScanFindings.LcpImageFoundLate);
    private static readonly Rule layoutShiftCauses = new(ScanFindings.LayoutShiftCauses, (_, f) => f.ItemCount, Whole);
    private static readonly Rule fontsHideText = new(ScanFindings.FontsHideText);
    private static readonly Rule domTooLarge = new(ScanFindings.DomTooLarge, (_, f) => f.NumericValue, Whole, Adds: false);
    private static readonly Rule oldHttpProtocol = new(ScanFindings.OldHttpProtocol, (_, f) => f.ItemCount, Whole);
    private static readonly Rule pageTooHeavy = new(ScanFindings.PageTooHeavy, (_, f) => f.NumericUnit is "byte" ? f.NumericValue : null, KiB, Adds: false);
    private static readonly Rule backForwardCacheBlocked = new(ScanFindings.BackForwardCacheBlocked);
    private static readonly Rule viewportNotSet = new(ScanFindings.ViewportNotSet);

    private static readonly Dictionary<string, Rule> performance = new()
    {
        ["image-delivery-insight"] = imagesTooBig,
        ["uses-responsive-images"] = imagesTooBig,
        ["modern-image-formats"] = imagesTooBig,
        ["uses-optimized-images"] = imagesTooBig,
        ["efficient-animated-content"] = imagesTooBig,
        ["cache-insight"] = filesNotCached,
        ["uses-long-cache-ttl"] = filesNotCached,
        ["render-blocking-insight"] = renderBlocking,
        ["render-blocking-resources"] = renderBlocking,
        ["unused-javascript"] = unusedCode,
        ["unused-css-rules"] = unusedCode,
        ["unminified-javascript"] = unusedCode,
        ["unminified-css"] = unusedCode,
        ["duplicated-javascript-insight"] = unusedCode,
        ["duplicated-javascript"] = unusedCode,
        ["legacy-javascript-insight"] = unusedCode,
        ["legacy-javascript"] = unusedCode,
        ["bootup-time"] = mainThreadBusy,
        ["mainthread-work-breakdown"] = mainThreadBusy,
        ["third-parties-insight"] = thirdPartyCode,
        ["third-party-summary"] = thirdPartyCode,
        ["document-latency-insight"] = pageSlowToArrive,
        ["server-response-time"] = pageSlowToArrive,
        ["redirects"] = pageSlowToArrive,
        ["uses-text-compression"] = pageSlowToArrive,
        ["lcp-discovery-insight"] = lcpImageFoundLate,
        ["prioritize-lcp-image"] = lcpImageFoundLate,
        ["lcp-lazy-loaded"] = lcpImageFoundLate,
        ["cls-culprits-insight"] = layoutShiftCauses,
        ["unsized-images"] = layoutShiftCauses,
        ["non-composited-animations"] = layoutShiftCauses,
        ["layout-shifts"] = layoutShiftCauses,
        ["font-display-insight"] = fontsHideText,
        ["font-display"] = fontsHideText,
        ["dom-size-insight"] = domTooLarge,
        ["dom-size"] = domTooLarge,
        ["modern-http-insight"] = oldHttpProtocol,
        ["uses-http2"] = oldHttpProtocol,
        ["total-byte-weight"] = pageTooHeavy,
        ["bf-cache"] = backForwardCacheBlocked,
        ["viewport-insight"] = viewportNotSet,
        ["viewport"] = viewportNotSet
    };

    /// <summary>Insights that explain a metric rather than say what to fix; the metric's own finding says it.</summary>
    private static readonly HashSet<string> performanceLeftOut = ["lcp-breakdown-insight", "lcp-phases-insight", "network-dependency-tree-insight", "critical-request-chains",
                                                                  "inp-breakdown-insight", "interaction-to-next-paint", "user-timings"];

    private static readonly Rule contrastTooLow = new(ScanFindings.ContrastTooLow, (_, f) => Math.Max(1, f.ItemCount), Whole);
    private static readonly Rule buttonsWithoutName = new(ScanFindings.ButtonsWithoutName, (_, f) => Math.Max(1, f.ItemCount), Whole);
    private static readonly Rule imagesWithoutDescription = new(ScanFindings.ImagesWithoutDescription, (_, f) => Math.Max(1, f.ItemCount), Whole);
    private static readonly Rule linksWithoutName = new(ScanFindings.LinksWithoutName, (_, f) => Math.Max(1, f.ItemCount), Whole);
    private static readonly Rule fieldsWithoutLabel = new(ScanFindings.FieldsWithoutLabel, (_, f) => Math.Max(1, f.ItemCount), Whole);
    private static readonly Rule pageLanguageWrong = new(ScanFindings.PageLanguageWrong);
    private static readonly Rule zoomBlocked = new(ScanFindings.ZoomBlocked);
    private static readonly Rule ariaMisused = new(ScanFindings.AriaMisused, (_, f) => Math.Max(1, f.ItemCount), Whole);
    private static readonly Rule tapTargetsTooSmall = new(ScanFindings.TapTargetsTooSmall, (_, f) => Math.Max(1, f.ItemCount), Whole);
    private static readonly Rule structureUnclear = new(ScanFindings.StructureUnclear, (_, f) => Math.Max(1, f.ItemCount), Whole);
    private static readonly Rule keyboardNavigation = new(ScanFindings.KeyboardNavigation, (_, f) => Math.Max(1, f.ItemCount), Whole);

    private static readonly Dictionary<string, Rule> accessibility = new()
    {
        ["color-contrast"] = contrastTooLow,
        ["link-in-text-block"] = contrastTooLow,
        ["button-name"] = buttonsWithoutName,
        ["input-button-name"] = buttonsWithoutName,
        ["aria-command-name"] = buttonsWithoutName,
        ["aria-toggle-field-name"] = buttonsWithoutName,
        ["image-alt"] = imagesWithoutDescription,
        ["input-image-alt"] = imagesWithoutDescription,
        ["object-alt"] = imagesWithoutDescription,
        ["svg-img-alt"] = imagesWithoutDescription,
        ["role-img-alt"] = imagesWithoutDescription,
        ["link-name"] = linksWithoutName,
        ["label"] = fieldsWithoutLabel,
        ["select-name"] = fieldsWithoutLabel,
        ["aria-input-field-name"] = fieldsWithoutLabel,
        ["form-field-multiple-labels"] = fieldsWithoutLabel,
        ["html-has-lang"] = pageLanguageWrong,
        ["html-lang-valid"] = pageLanguageWrong,
        ["valid-lang"] = pageLanguageWrong,
        ["html-xml-lang-mismatch"] = pageLanguageWrong,
        ["meta-viewport"] = zoomBlocked,
        ["aria-allowed-attr"] = ariaMisused,
        ["aria-allowed-role"] = ariaMisused,
        ["aria-conditional-attr"] = ariaMisused,
        ["aria-deprecated-role"] = ariaMisused,
        ["aria-dialog-name"] = ariaMisused,
        ["aria-hidden-body"] = ariaMisused,
        ["aria-hidden-focus"] = ariaMisused,
        ["aria-meter-name"] = ariaMisused,
        ["aria-progressbar-name"] = ariaMisused,
        ["aria-prohibited-attr"] = ariaMisused,
        ["aria-required-attr"] = ariaMisused,
        ["aria-required-children"] = ariaMisused,
        ["aria-required-parent"] = ariaMisused,
        ["aria-roles"] = ariaMisused,
        ["aria-text"] = ariaMisused,
        ["aria-tooltip-name"] = ariaMisused,
        ["aria-treeitem-name"] = ariaMisused,
        ["aria-valid-attr-value"] = ariaMisused,
        ["aria-valid-attr"] = ariaMisused,
        ["duplicate-id-aria"] = ariaMisused,
        ["presentation-role-conflict"] = ariaMisused,
        ["target-size"] = tapTargetsTooSmall,
        ["heading-order"] = structureUnclear,
        ["empty-heading"] = structureUnclear,
        ["list"] = structureUnclear,
        ["listitem"] = structureUnclear,
        ["definition-list"] = structureUnclear,
        ["dlitem"] = structureUnclear,
        ["td-headers-attr"] = structureUnclear,
        ["th-has-data-cells"] = structureUnclear,
        ["table-duplicate-name"] = structureUnclear,
        ["td-has-header"] = structureUnclear,
        ["table-fake-caption"] = structureUnclear,
        ["landmark-one-main"] = structureUnclear,
        ["tabindex"] = keyboardNavigation,
        ["accesskeys"] = keyboardNavigation,
        ["bypass"] = keyboardNavigation,
        ["skip-link"] = keyboardNavigation,
        ["frame-title"] = keyboardNavigation
    };

    /// <summary>The performance findings: the phone runs' Core Web Vitals, then what Lighthouse's audits found on every run.</summary>
    public static List<ScanFindingResult> Performance(IReadOnlyList<BrowserScanRun> runs)
    {
        List<ScanFindingResult> findings = [];

        Vital(findings, runs, ScanFindings.LcpSlow, run => run.Audit.Metrics.LargestContentfulPaintMs, needsImprovementOver: 2500, poorOver: 4000, Seconds);
        Vital(findings, runs, ScanFindings.ClsHigh, run => run.Audit.Metrics.CumulativeLayoutShift, needsImprovementOver: 0.1, poorOver: 0.25, value => value.ToString("0.00", invariant));
        Vital(findings, runs, ScanFindings.TbtHigh, run => run.Audit.Metrics.TotalBlockingTimeMs, needsImprovementOver: 200, poorOver: 600, Whole);
        Vital(findings, runs, ScanFindings.FcpSlow, run => run.Audit.Metrics.FirstContentfulPaintMs, needsImprovementOver: 1800, poorOver: 3000, Seconds);

        findings.AddRange(Audits(runs, "performance", performance, performanceLeftOut, ScanFindings.PerformanceAuditFailed,
            // Worth fixing soon when Lighthouse fails it and fixing it would make a metric better; an improvement otherwise.
            audit => audit.Score < 0.5 && audit.MetricSavings?.Values.Any(saving => saving > 0) is true ? ScanFindingSeverity.Important : ScanFindingSeverity.Improvement));

        return [.. findings.OrderBy(finding => finding.Severity)];
    }

    /// <summary>The accessibility findings: what Lighthouse's axe-core audits found on every run.</summary>
    public static List<ScanFindingResult> Accessibility(IReadOnlyList<BrowserScanRun> runs)
    {
        var findings = Audits(runs, "accessibility", accessibility, [], ScanFindings.AccessibilityAuditFailed,
            // Lighthouse weighs an audit 10 or 7 for axe-core's critical and serious impact, 3 and 1 for moderate and minor.
            audit => audit.Weight >= 7 ? ScanFindingSeverity.Important : ScanFindingSeverity.Improvement,
            // An audit Lighthouse gives no weight is advice of its own, not a failure of the page.
            leaveOutUnknown: audit => audit.Weight <= 0);

        return [.. findings.OrderBy(finding => finding.Severity)];
    }

    /// <summary>A Core Web Vital beyond Google's threshold on the phone runs: its worst value, on the pages it was beyond.</summary>
    private static void Vital(List<ScanFindingResult> findings, IReadOnlyList<BrowserScanRun> runs, string key, Func<BrowserScanRun, double?> metric,
                              double needsImprovementOver, double poorOver, Func<double, string> format)
    {
        var beyond = runs.Where(run => run.Device is ProjectScanDevice.Phone)
                         .Select(run => (Run: run, Value: metric(run)))
                         .Where(item => item.Value > needsImprovementOver)
                         .ToArray();

        if (beyond.Length is 0)
            return;

        var worst = beyond.Max(item => item.Value!.Value);

        findings.Add(new(key, worst > poorOver ? ScanFindingSeverity.Important : ScanFindingSeverity.Improvement, [format(worst)],
                         [.. beyond.Select(item => item.Run.Path).Distinct()]));
    }

    private static List<ScanFindingResult> Audits(IReadOnlyList<BrowserScanRun> runs, string category, Dictionary<string, Rule> rules, HashSet<string> leftOut, string unknownKey,
                                                  Func<LighthouseAuditFinding, ScanFindingSeverity> severityOf, Func<LighthouseAuditFinding, bool>? leaveOutUnknown = null)
    {
        // By key and, for an audit without words of its own, by its id.
        Dictionary<(string Key, string? Id), Found> found = [];

        foreach (var run in runs)
        {
            Dictionary<(string Key, string? Id), double> thisPage = [];

            foreach (var audit in run.Audit.Findings.Where(audit => audit.Category == category && audit.Id is not null))
            {
                var id = audit.Id!;

                if (leftOut.Contains(id))
                    continue;

                var known = rules.TryGetValue(id, out var rule);
                var value = known ? rule!.Value?.Invoke(run, audit) : null;

                // A key whose texts need a number it didn't get reads as the audit itself.
                if (known && rule!.Value is not null && value is null)
                {
                    known = false;
                }

                if (known is false && leaveOutUnknown?.Invoke(audit) is true)
                    continue;

                var group = known ? (rule!.Key, (string?)null) : (unknownKey, id);

                if (found.TryGetValue(group, out var entry) is false)
                {
                    found[group] = entry = new(known ? rule! : null);
                }

                entry.Severity = (ScanFindingSeverity)Math.Min((int)entry.Severity, (int)severityOf(audit));
                if (entry.Pages.Contains(run.Path) is false)
                {
                    entry.Pages.Add(run.Path);
                }
                foreach (var where in audit.Where)
                {
                    if (entry.Items.Count < MaxItems && entry.Items.Contains(where) is false)
                    {
                        entry.Items.Add(where);
                    }
                }

                if (value is { } number)
                {
                    thisPage[group] = thisPage.TryGetValue(group, out var sum) ? (rule!.Adds ? sum + number : Math.Max(sum, number)) : number;
                }
            }

            // The page that has the most of it.
            foreach (var (group, value) in thisPage)
            {
                found[group].Value = Math.Max(found[group].Value ?? 0, value);
            }
        }

        return [.. found.Select(item => new ScanFindingResult(
            item.Key.Key,
            item.Value.Severity,
            item.Key.Id is { } id ? [id] : item.Value.Rule?.Format is { } format && item.Value.Value is { } value ? [format(value)] : [],
            [.. item.Value.Pages, .. item.Value.Items]))];
    }

    private sealed class Found(Rule? rule)
    {
        public Rule? Rule { get; } = rule;

        public ScanFindingSeverity Severity { get; set; } = ScanFindingSeverity.Improvement;

        public double? Value { get; set; }

        public List<string> Pages { get; } = [];

        public List<string> Items { get; } = [];
    }

    /// <summary>The milliseconds an audit would save of the first paints, as Lighthouse 13 or 12 says it.</summary>
    private static double? SavedMs(LighthouseAuditFinding audit)
        => audit.SavingsMs is > 0 ? audit.SavingsMs
           : audit.MetricSavings is { } savings && savings.Where(saving => saving.Key is "FCP" or "LCP").Select(saving => saving.Value).DefaultIfEmpty(0).Max() is > 0 and var most ? most
           : audit.NumericUnit is "millisecond" ? audit.NumericValue : null;

    private static string KiB(double bytes) => Math.Max(1, Math.Round(bytes / 1024)).ToString("0", invariant);

    private static string Whole(double value) => Math.Round(value).ToString("0", invariant);

    private static string Seconds(double milliseconds) => (milliseconds / 1000).ToString("0.0", invariant);
}
