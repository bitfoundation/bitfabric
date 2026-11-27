using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// Every minute, one round of every project from this server (bitfabric-internal/specs/monitoring.md, Checks), one that
/// needs reconnecting included, since its public /health is still read; only one the team disconnected is left alone.
/// </summary>
public partial class ProjectProbeJobRunner
{
    public const string RecurringJobId = nameof(ProjectProbeJobRunner);

    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private IServiceScopeFactory serviceScopeFactory = default!;
    [AutoInject] private ServerApiSettings settings = default!;
    [AutoInject] private TimeProvider timeProvider = default!;
    [AutoInject] private ILogger<ProjectProbeJobRunner> logger = default!;

    /// <summary>A run that takes longer than a minute must not overlap the next, which would check each project twice.</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 50), AutomaticRetry(Attempts = 0)]
    public async Task ProbeAll(CancellationToken cancellationToken)
    {
        var monitoringIds = await dbContext.Monitorings.IgnoreQueryFilters()
                                                 .Where(project => project.ConnectionStatus != ProjectConnectionStatus.Disconnected)
                                                 .Select(project => project.Id)
                                                 .ToListAsync(cancellationToken);

        using var slots = new SemaphoreSlim(settings.Monitoring.Parallelism);

        await Task.WhenAll(monitoringIds.Select(async monitoringId =>
        {
            // Each project at its own moment of the minute; the slot is taken only then, so a project waiting for its
            // moment doesn't hold one.
            await Task.Delay(OffsetOf(monitoringId, settings.Monitoring.RoundSpread), timeProvider, cancellationToken);
            await slots.WaitAsync(cancellationToken);

            try
            {
                // A DbContext per project: they run side by side, and one project's failure stays its own.
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ProjectProber>().ProbeUnlessUnderway(monitoringId, cancellationToken);
            }
            catch (Exception exp) when (exp is not OperationCanceledException)
            {
                logger.LogError(exp, "Checking project {MonitoringId} failed.", monitoringId);
            }
            finally
            {
                slots.Release();
            }
        }));
    }

    /// <summary>
    /// When in the minute the project's round starts, within <paramref name="spread"/>: from its id, so it is the same moment
    /// every minute and on every instance, and the projects of one host, or of Test and Production, fall apart. A Guid's
    /// hash is made from its bytes, the same in every process.
    /// </summary>
    public static TimeSpan OffsetOf(Guid monitoringId, TimeSpan spread)
        => spread <= TimeSpan.Zero ? TimeSpan.Zero : TimeSpan.FromMilliseconds((uint)monitoringId.GetHashCode() % (ulong)spread.TotalMilliseconds);
}

/// <summary>
/// Keeps a project's raw rounds for 30 days and its periods, starts, 15 minute rollups and scans for 13 months
/// (bitfabric-internal/specs/monitoring.md, Built for its volume): what the pages draw beyond the last 30 days comes from
/// those.
/// </summary>
public partial class ProjectProbesRetentionJobRunner
{
    public const string RecurringJobId = nameof(ProjectProbesRetentionJobRunner);

    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private TimeProvider timeProvider = default!;
    [AutoInject] private ILogger<ProjectProbesRetentionJobRunner> logger = default!;

    public async Task EnforceRetention(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var roundsBefore = now.AddDays(-30);
        var historyBefore = now.AddDays(-395);

        var rounds = await dbContext.ProjectProbes.IgnoreQueryFilters()
                                                  .Where(probe => probe.At < roundsBefore)
                                                  .ExecuteDeleteAsync(cancellationToken);

        var periods = await dbContext.ProjectCheckPeriods.IgnoreQueryFilters()
                                                         .Where(period => period.EndedAt < historyBefore)
                                                         .ExecuteDeleteAsync(cancellationToken);

        var starts = await dbContext.ProjectStarts.IgnoreQueryFilters()
                                                  .Where(start => start.At < historyBefore)
                                                  .ExecuteDeleteAsync(cancellationToken);

        var rollups = await dbContext.ProjectRollups.IgnoreQueryFilters()
                                                    .Where(rollup => rollup.BucketStart < historyBefore)
                                                    .ExecuteDeleteAsync(cancellationToken);

        // Their findings go with them; the reports of a wrong finding stay, a guard metric (See ProjectScanFindingReport).
        var scans = await dbContext.ProjectScans.IgnoreQueryFilters()
                                                .Where(scan => scan.RequestedAt < historyBefore)
                                                .ExecuteDeleteAsync(cancellationToken);

        if (rounds + periods + starts + rollups + scans > 0)
        {
            logger.LogInformation("Deleted {Rounds} round(s), {Periods} period(s), {Starts} start(s), {Rollups} rollup(s) and {Scans} scan(s) past their retention.", rounds, periods, starts, rollups, scans);
        }
    }
}
