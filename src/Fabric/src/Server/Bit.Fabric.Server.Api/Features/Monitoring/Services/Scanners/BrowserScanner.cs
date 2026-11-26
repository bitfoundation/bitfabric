using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;

/// <summary>
/// The scans that load the app's pages in a browser (bitfabric-internal/specs/scanners.md, Decisions, PR 11): Performance
/// and Accessibility, both read from the same Lighthouse runs of bit Fabric's own service. The home page is loaded on a
/// phone and on a computer, then up to two pages of the sitemap on a phone while the scan's time allows. Both kinds of one
/// request name their runs by that request, so the service runs each page once for the two of them. The address is
/// checked against the public-address guard first, as every request to a customer's app is; inside the service, a guard
/// of its own keeps Chrome and everything a page loads on the public internet.
/// </summary>
public abstract partial class BrowserScanner(LighthouseClient lighthouse, SitemapReader sitemapReader, ServerApiSettings settings, TimeProvider timeProvider, ILogger logger) : IProjectScanner
{
    /// <summary>The pages of the sitemap loaded besides the home page, on a phone.</summary>
    public const int SitemapPages = 2;

    /// <summary>Another page is loaded only while this much of the scan's time is left: its turn and its run.</summary>
    private static readonly TimeSpan timeForAnotherPage = TimeSpan.FromMinutes(2);

    public abstract ProjectScanKind Kind { get; }

    public bool IsAvailable => string.IsNullOrWhiteSpace(settings.Monitoring.LighthouseUrl) is false;

    public bool UsesBrowser => true;

    public abstract Task<ProjectScanResult> Scan(ProjectMonitoring project, ProjectScan scan, CancellationToken cancellationToken);

    /// <summary>The runs of the project's pages; null when the app couldn't be reached or its home page couldn't be loaded.</summary>
    protected async Task<List<BrowserScanRun>?> Measure(ProjectMonitoring project, ProjectScan scan, CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        var home = new Uri($"{project.Address}/");

        if (settings.Monitoring.AllowLocalAddresses is false && await PublicAddressGuard.IsPublicHost(home.Host, cancellationToken) is false)
        {
            logger.LogInformation("{Kind} scan of project {MonitoringId}: {Host} doesn't resolve to a public address.", Kind, project.Id, home.Host);
            return null;
        }

        // The request both kinds were asked for in: the second reads the first one's runs, and a later request never does.
        var reuseKey = $"{scan.MonitoringId:N}:{scan.RequestedAt.UtcTicks}";

        var phone = await lighthouse.Audit(home.ToString(), ProjectScanDevice.Phone, reuseKey, cancellationToken);

        if (phone is null || phone.RuntimeError is not null)
        {
            logger.LogInformation("{Kind} scan of project {MonitoringId}: Lighthouse couldn't load {Url} ({RuntimeError}).", Kind, project.Id, home, phone?.RuntimeError ?? "not public");
            return null;
        }

        List<BrowserScanRun> runs = [new(PathOf(phone, home), ProjectScanDevice.Phone, phone)];

        if (await lighthouse.Audit(home.ToString(), ProjectScanDevice.Computer, reuseKey, cancellationToken) is { RuntimeError: null } computer)
        {
            runs.Add(new(PathOf(computer, home), ProjectScanDevice.Computer, computer));
        }

        foreach (var url in await SitemapPagesOf(home, runs[0].Path, cancellationToken))
        {
            if (timeProvider.GetUtcNow() - startedAt > settings.Monitoring.BrowserScanTimeout - timeForAnotherPage)
                break;

            if (await lighthouse.Audit(url.ToString(), ProjectScanDevice.Phone, reuseKey, cancellationToken) is { RuntimeError: null } page
                && runs.Any(run => run.Path == PathOf(page, url)) is false)
            {
                runs.Add(new(PathOf(page, url), ProjectScanDevice.Phone, page));
            }
        }

        if (runs.Sum(run => run.Audit.RefusedRequests) is var refused and > 0)
        {
            logger.LogWarning("{Kind} scan of project {MonitoringId}: the Lighthouse service refused {Count} request(s) its pages made to addresses that aren't public.", Kind, project.Id, refused);
        }

        logger.LogInformation("{Kind} scan of project {MonitoringId}: {Runs} run(s) of Lighthouse {Version}, benchmark index {BenchmarkIndex}.",
                              Kind, project.Id, runs.Count, phone.LighthouseVersion, phone.BenchmarkIndex);

        return runs;
    }

    /// <summary>
    /// A couple of the sitemap's pages: in the home page's culture first, as a bit app lists every page in every culture,
    /// never the home page again, and the same ones for both kinds, so they share their runs.
    /// </summary>
    private async Task<Uri[]> SitemapPagesOf(Uri home, string homePath, CancellationToken cancellationToken)
    {
        List<string> listed;

        try
        {
            listed = await sitemapReader.Pages(home.GetLeftPart(UriPartial.Authority), maxPages: 50, cancellationToken);
        }
        catch (Exception exp) when (exp is not OperationCanceledException)
        {
            logger.LogWarning(exp, "Reading the sitemap of {Address} for a browser scan failed.", home);
            return [];
        }

        var culture = homePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is { } first && first.Contains('-', StringComparison.Ordinal) ? $"/{first}/" : null;

        return [.. listed
            .Select(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null)
            .OfType<Uri>()
            .Where(uri => uri.PathAndQuery != homePath && uri.PathAndQuery != "/")
            .DistinctBy(uri => uri.PathAndQuery)
            .OrderBy(uri => culture is not null && uri.AbsolutePath.StartsWith(culture, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Take(SitemapPages)];
    }

    private static string PathOf(LighthouseAudit audit, Uri asked)
        => Uri.TryCreate(audit.FinalUrl, UriKind.Absolute, out var final) ? final.PathAndQuery : asked.PathAndQuery;

    /// <summary>Lighthouse's 0 to 1 as bit Fabric's 0 to 100.</summary>
    protected static int? Percent(double? score) => score is { } value ? (int)Math.Round(value * 100) : null;

    protected static int? Milliseconds(double? value) => value is { } ms ? (int)Math.Round(ms) : null;
}

/// <summary>One page loaded on one device, as Lighthouse measured it.</summary>
public sealed record BrowserScanRun(string Path, ProjectScanDevice Device, LighthouseAudit Audit);

/// <summary>
/// The performance scan: Lighthouse's score of each page and device, the lowest of them the scan's, its Core Web Vitals,
/// and what its audits found (See LighthouseFindings).
/// </summary>
public partial class PerformanceScanner(LighthouseClient lighthouse, SitemapReader sitemapReader, ServerApiSettings settings, TimeProvider timeProvider, ILogger<PerformanceScanner> logger)
    : BrowserScanner(lighthouse, sitemapReader, settings, timeProvider, logger)
{
    public override ProjectScanKind Kind => ProjectScanKind.Performance;

    public override async Task<ProjectScanResult> Scan(ProjectMonitoring project, ProjectScan scan, CancellationToken cancellationToken)
    {
        if (await Measure(project, scan, cancellationToken) is not { } runs)
            return ProjectScanResult.Unreachable();

        ProjectScanResult result = new();

        result.Pages.AddRange(runs.Select(run => new ProjectScanPage
        {
            Path = run.Path,
            Device = run.Device,
            Score = Percent(run.Audit.PerformanceScore),
            LargestContentfulPaintMs = Milliseconds(run.Audit.Metrics.LargestContentfulPaintMs),
            CumulativeLayoutShift = run.Audit.Metrics.CumulativeLayoutShift is { } cls ? Math.Round(cls, 3) : null,
            TotalBlockingTimeMs = Milliseconds(run.Audit.Metrics.TotalBlockingTimeMs),
            FirstContentfulPaintMs = Milliseconds(run.Audit.Metrics.FirstContentfulPaintMs),
            SpeedIndexMs = Milliseconds(run.Audit.Metrics.SpeedIndexMs),
            TimeToFirstByteMs = Milliseconds(run.Audit.Metrics.TimeToFirstByteMs)
        }));
        result.Score = result.Pages.Min(page => page.Score);
        result.Findings.AddRange(LighthouseFindings.Performance(runs));
        // Lighthouse's own bands: 90 and up is good, under 50 poor.
        result.Judge(goodFrom: 90, poorBelow: 50);

        return result;
    }
}

/// <summary>
/// The accessibility scan: Lighthouse's accessibility audits, which are axe-core's, on the same runs as Performance, the
/// lowest score of them the scan's.
/// </summary>
public partial class AccessibilityScanner(LighthouseClient lighthouse, SitemapReader sitemapReader, ServerApiSettings settings, TimeProvider timeProvider, ILogger<AccessibilityScanner> logger)
    : BrowserScanner(lighthouse, sitemapReader, settings, timeProvider, logger)
{
    public override ProjectScanKind Kind => ProjectScanKind.Accessibility;

    public override async Task<ProjectScanResult> Scan(ProjectMonitoring project, ProjectScan scan, CancellationToken cancellationToken)
    {
        if (await Measure(project, scan, cancellationToken) is not { } runs)
            return ProjectScanResult.Unreachable();

        ProjectScanResult result = new();

        result.Pages.AddRange(runs.Select(run => new ProjectScanPage { Path = run.Path, Device = run.Device, Score = Percent(run.Audit.AccessibilityScore) }));
        result.Score = result.Pages.Min(page => page.Score);
        result.Findings.AddRange(LighthouseFindings.Accessibility(runs));
        result.Judge(goodFrom: 90, poorBelow: 50);

        return result;
    }
}
