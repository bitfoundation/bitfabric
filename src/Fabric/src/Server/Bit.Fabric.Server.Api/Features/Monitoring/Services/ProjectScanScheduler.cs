using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// Asks for a project's scans (bitfabric-internal/specs/scanners.md, Decisions, 2026-11-13): after a deployment the health
/// check shows, when the owner allows it; every week; by hand; and once right after the project is connected. A scan is
/// written down when it is asked for and run by a Hangfire job of its own, so a kind already waiting for its turn is
/// joined rather than asked for twice. It runs in background jobs as well as requests, so every query ignores the tenant
/// filter and names its rows' tenant.
/// </summary>
public partial class ProjectScanScheduler
{
    /// <summary>A scan asked for longer ago than this and still not started never will be: a new request doesn't wait for it.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(1);

    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private IBackgroundJobClient backgroundJobClient = default!;
    [AutoInject] private TimeProvider timeProvider = default!;
    [AutoInject] private ServerApiSettings settings = default!;
    [AutoInject] private DistributedLockFactory distributedLockFactory = default!;
    [AutoInject] private IEnumerable<IProjectScanner> scanners = default!;
    [AutoInject] private ILogger<ProjectScanScheduler> logger = default!;

    /// <summary>The kinds this server runs, in the order the pages show them: the browser scans need the Lighthouse service.</summary>
    public ProjectScanKind[] Kinds => [.. ScanFindings.AvailableKinds.Where(kind => scanners.Any(scanner => scanner.Kind == kind && scanner.IsAvailable))];

    /// <summary>Every kind bit Fabric runs, after <paramref name="delay"/>; returns the scans written, not the ones joined.</summary>
    public async Task<List<ProjectScan>> Request(ProjectMonitoring project, ProjectScanTrigger trigger, TimeSpan delay, Guid? userId, string? applicationVersion, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var staleBefore = now - StaleAfter;

        var waiting = await dbContext.ProjectScans.IgnoreQueryFilters()
            .Where(scan => scan.MonitoringId == project.Id && scan.FinishedAt == null && scan.StartedAt == null && scan.RequestedAt >= staleBefore)
            .Select(scan => scan.Kind)
            .ToListAsync(cancellationToken);

        List<ProjectScan> scans = [.. Kinds.Where(kind => waiting.Contains(kind) is false).Select(kind => new ProjectScan
        {
            Id = Guid.CreateSequentialGuid(),
            MonitoringId = project.Id,
            TenantId = project.TenantId,
            Kind = kind,
            Trigger = trigger,
            RequestedByUserId = userId,
            ApplicationVersion = applicationVersion?.Length > 200 ? applicationVersion[..200] : applicationVersion,
            RequestedAt = now
        })];

        if (scans.Count is 0)
            return scans;

        await dbContext.ProjectScans.AddRangeAsync(scans, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // After the rows are saved, so a job never looks for a scan that isn't there yet.
        foreach (var scan in scans)
        {
            var scanId = scan.Id;

            if (delay <= TimeSpan.Zero)
            {
                backgroundJobClient.Enqueue<ProjectScanJobRunner>(runner => runner.Run(scanId, default));
            }
            else
            {
                backgroundJobClient.Schedule<ProjectScanJobRunner>(runner => runner.Run(scanId, default), delay);
            }
        }

        logger.LogInformation("Asked for {Count} scan(s) of project {MonitoringId} ({Trigger}), starting in {Delay}.", scans.Count, project.Id, trigger, delay);

        return scans;
    }

    /// <summary>
    /// A deployment the health check showed (See ProjectProber.RecordStart): its scans a couple of minutes later, so the new
    /// build is up everywhere, when the owner allows scans after deployments. Off until they do.
    /// </summary>
    public async Task AfterDeployment(ProjectMonitoring project, string? applicationVersion, CancellationToken cancellationToken)
    {
        var allowed = await dbContext.ProjectScanSettings.IgnoreQueryFilters()
            .Where(row => row.MonitoringId == project.Id)
            .Select(row => (bool?)row.ScanAfterDeployments)
            .FirstOrDefaultAsync(cancellationToken) ?? false;

        if (allowed is false)
            return;

        await Request(project, ProjectScanTrigger.Deployment, settings.Monitoring.ScanAfterDeploymentDelay, userId: null, applicationVersion, cancellationToken);
    }

    /// <summary>
    /// "Run scans now", from the Scans tab or an AI agent on /mcp: every kind at once, at most once every 10 minutes a
    /// project. Returns how long until it may be asked for again when it's too soon, null when the scans were asked for.
    /// </summary>
    public async Task<TimeSpan?> RunByHand(ProjectMonitoring project, Guid userId, CancellationToken cancellationToken)
    {
        // Two presses at once must not both pass the check.
        await using (await distributedLockFactory($"bit-fabric:monitoring-scan-run-now:{project.Id}").AcquireAsync(TimeSpan.FromSeconds(10), cancellationToken))
        {
            var now = timeProvider.GetUtcNow();

            var lastByHand = await dbContext.ProjectScans.IgnoreQueryFilters()
                .Where(scan => scan.MonitoringId == project.Id && scan.Trigger == ProjectScanTrigger.Manual)
                .MaxAsync(scan => (DateTimeOffset?)scan.RequestedAt, cancellationToken);

            if (lastByHand is { } last && last + ProjectScanReader.RunNowInterval > now)
                return last + ProjectScanReader.RunNowInterval - now;

            await Request(project, ProjectScanTrigger.Manual, TimeSpan.Zero, userId, applicationVersion: null, cancellationToken);

            return null;
        }
    }

    /// <summary>The first scans of a project just connected; a project reconnected keeps the history it has.</summary>
    public async Task AfterConnecting(ProjectMonitoring project, Guid userId, CancellationToken cancellationToken)
    {
        if (await dbContext.ProjectScans.IgnoreQueryFilters().AnyAsync(scan => scan.MonitoringId == project.Id, cancellationToken))
            return;

        await Request(project, ProjectScanTrigger.Connected, TimeSpan.Zero, userId, applicationVersion: null, cancellationToken);
    }
}
