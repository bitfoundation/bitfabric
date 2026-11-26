using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Medallion.Threading;
using Medallion.Threading.Redis;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// Runs one scan a ProjectScanScheduler asked for (bitfabric-internal/specs/scanners.md). A scan of the same project and
/// kind never overlaps itself, and at most <see cref="MonitoringOptions.ScanParallelism"/> run at once across every project
/// and every instance of the server, both by Redis, which every instance shares. A scanner that fails ends its own scan as
/// failed and nothing else.
/// </summary>
public partial class ProjectScanJobRunner
{
    /// <summary>How long a scan waits for a free slot, or for the scan of its kind still running, before it gives up.</summary>
    private static readonly TimeSpan waitForTurn = TimeSpan.FromMinutes(15);

    /// <summary>A browser scan's wait: they run one or two at a time and take minutes each, within the hour a scan is kept waiting.</summary>
    private static readonly TimeSpan waitForBrowserTurn = TimeSpan.FromMinutes(30);

    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private IEnumerable<IProjectScanner> scanners = default!;
    [AutoInject] private DistributedLockFactory distributedLockFactory = default!;
    [AutoInject] private IServiceProvider serviceProvider = default!;
    [AutoInject] private ServerApiSettings settings = default!;
    [AutoInject] private TimeProvider timeProvider = default!;
    [AutoInject] private ILogger<ProjectScanJobRunner> logger = default!;

    [AutomaticRetry(Attempts = 0)]
    public async Task Run(Guid scanId, CancellationToken cancellationToken)
    {
        var scan = await dbContext.ProjectScans.IgnoreQueryFilters().FirstOrDefaultAsync(scan => scan.Id == scanId, cancellationToken);

        if (scan is null || scan.StartedAt is not null)
            return; // Its project was removed, or another run of this job took it.

        var scanner = scanners.FirstOrDefault(scanner => scanner.Kind == scan.Kind);
        var usesBrowser = scanner?.UsesBrowser is true;

        // Lighthouse is heavy: the browser scans have slots of their own, fewer, so they never hold up the others.
        var slots = usesBrowser
            ? new RedisDistributedSemaphore("bit-fabric:monitoring-browser-scans", Math.Max(1, settings.Monitoring.BrowserScanParallelism),
                                            serviceProvider.GetRequiredKeyedService<StackExchange.Redis.IConnectionMultiplexer>("redis-persistent").GetDatabase())
            : new RedisDistributedSemaphore("bit-fabric:monitoring-scans", Math.Max(1, settings.Monitoring.ScanParallelism),
                                            serviceProvider.GetRequiredKeyedService<StackExchange.Redis.IConnectionMultiplexer>("redis-persistent").GetDatabase());
        var wait = usesBrowser ? waitForBrowserTurn : waitForTurn;

        await using var slot = await slots.AcquireAsync(wait, cancellationToken);
        await using var turn = await distributedLockFactory($"bit-fabric:monitoring-scan:{scan.MonitoringId}:{scan.Kind}").AcquireAsync(wait, cancellationToken);

        var startedAt = timeProvider.GetUtcNow();

        // Taken once, whoever gets there first: the job, or a test running the queue by hand.
        var claimed = await dbContext.ProjectScans.IgnoreQueryFilters()
            .Where(row => row.Id == scanId && row.StartedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.StartedAt, startedAt), cancellationToken);

        if (claimed is 0)
            return;

        scan.StartedAt = startedAt;

        var project = await dbContext.Monitorings.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(project => project.Id == scan.MonitoringId, cancellationToken);

        if (project is null)
            return;

        var result = await Scan(scanner, project, scan, cancellationToken);

        scan.FinishedAt = timeProvider.GetUtcNow();
        scan.Outcome = result.Outcome;
        scan.Verdict = result.Outcome is ProjectScanOutcome.Completed ? result.Verdict : null;
        scan.Score = result.Score;
        scan.Grade = result.Grade;
        scan.ExpiresAt = result.ExpiresAt;
        scan.Pages = [.. result.Pages.Take(20).Select(page => { page.Path = page.Path?.Length > 500 ? page.Path[..500] : page.Path; return page; })];
        // Added as new rows: a key set by hand would otherwise make them read as rows to update.
        await dbContext.ProjectScanFindings.AddRangeAsync(result.Findings.Select(finding => new ProjectScanFinding
        {
            Id = Guid.CreateSequentialGuid(),
            ScanId = scan.Id,
            TenantId = scan.TenantId,
            Key = finding.Key,
            Severity = finding.Severity,
            Parameters = [.. finding.Parameters.Select(parameter => parameter.Length > 300 ? parameter[..300] : parameter)],
            Where = [.. finding.Where.Take(20).Select(where => where.Length > 500 ? where[..500] : where)]
        }), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Scan {ScanId} ({Kind}) of project {MonitoringId} ended {Outcome} with {Findings} finding(s), {Score} {Grade}.",
                              scan.Id, scan.Kind, scan.MonitoringId, scan.Outcome, result.Findings.Count, scan.Score, scan.Grade);
    }

    private async Task<ProjectScanResult> Scan(IProjectScanner? scanner, ProjectMonitoring project, ProjectScan scan, CancellationToken cancellationToken)
    {
        if (scanner is null || scanner.IsAvailable is false)
        {
            logger.LogError("No scanner runs {Kind} scans here.", scan.Kind);
            return new() { Outcome = ProjectScanOutcome.Failed };
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(scanner.UsesBrowser ? settings.Monitoring.BrowserScanTimeout : settings.Monitoring.ScanTimeout);

        try
        {
            return await scanner.Scan(project, scan, budget.Token);
        }
        catch (Exception exp) when (cancellationToken.IsCancellationRequested is false)
        {
            logger.LogError(exp, "The {Kind} scan of project {MonitoringId} failed.", scan.Kind, project.Id);
            return new() { Outcome = ProjectScanOutcome.Failed };
        }
    }
}

/// <summary>
/// Every hour: the weekly scans of the projects whose hour of the week it is, so they spread over the week rather than all
/// run at once, and the tidying of scans that were asked for and never ran (bitfabric-internal/specs/scanners.md,
/// Decisions, 2026-11-13).
/// </summary>
public partial class ProjectScanWeeklyJobRunner
{
    public const string RecurringJobId = nameof(ProjectScanWeeklyJobRunner);

    private const int HoursAWeek = 7 * 24;

    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private ProjectScanScheduler scheduler = default!;
    [AutoInject] private TimeProvider timeProvider = default!;
    [AutoInject] private ILogger<ProjectScanWeeklyJobRunner> logger = default!;

    /// <summary>The hour of the week, from Sunday 00:00 UTC, a project's weekly scans fall in: the same every week.</summary>
    public static int SlotOf(Guid monitoringId) => (int)((uint)monitoringId.GetHashCode() % HoursAWeek);

    [DisableConcurrentExecution(timeoutInSeconds: 600), AutomaticRetry(Attempts = 0)]
    public async Task ScheduleDue(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var hourOfWeek = (int)now.DayOfWeek * 24 + now.Hour;

        var tidied = await dbContext.ProjectScans.IgnoreQueryFilters()
            .Where(scan => scan.FinishedAt == null && scan.RequestedAt < now - ProjectScanScheduler.StaleAfter)
            .ExecuteDeleteAsync(cancellationToken);

        if (tidied > 0)
        {
            logger.LogWarning("Deleted {Count} scan(s) asked for more than an hour ago that never finished.", tidied);
        }

        // A project without settings scans weekly: that's the default.
        var projects = await dbContext.Monitorings.IgnoreQueryFilters()
            .Where(project => project.ConnectionStatus != ProjectConnectionStatus.ReconnectNeeded && project.ConnectionStatus != ProjectConnectionStatus.Disconnected
                              && dbContext.ProjectScanSettings.IgnoreQueryFilters().Any(row => row.MonitoringId == project.Id && row.WeeklyScans == false) == false)
            .ToListAsync(cancellationToken);

        Guid[] monitoringIds = [.. projects.Select(project => project.Id)];

        var lastScans = await dbContext.ProjectScans.IgnoreQueryFilters()
            .Where(scan => monitoringIds.Contains(scan.MonitoringId))
            .GroupBy(scan => scan.MonitoringId)
            .Select(group => new { MonitoringId = group.Key, Last = group.Max(scan => scan.RequestedAt) })
            .ToDictionaryAsync(row => row.MonitoringId, row => row.Last, cancellationToken);

        foreach (var project in projects)
        {
            var last = lastScans.TryGetValue(project.Id, out var at) ? at : (DateTimeOffset?)null;

            // In its hour, unless a deployment or someone's hand scanned it in the last day; and any hour once a week went by
            // without one, such as when the server was down in its hour.
            var isDue = (SlotOf(project.Id) == hourOfWeek && (last is null || last < now.AddDays(-1)))
                        || last < now.AddDays(-8);

            if (isDue is false)
                continue;

            try
            {
                await scheduler.Request(project, ProjectScanTrigger.Weekly, TimeSpan.Zero, userId: null, applicationVersion: null, cancellationToken);
            }
            catch (Exception exp) when (exp is not OperationCanceledException)
            {
                logger.LogError(exp, "Asking for the weekly scans of project {MonitoringId} failed.", project.Id);
            }
        }
    }
}
