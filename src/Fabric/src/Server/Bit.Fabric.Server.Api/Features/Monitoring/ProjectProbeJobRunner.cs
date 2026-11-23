using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>Every minute, one round of every connected project from this server (bitfabric-internal/specs/monitoring.md, Checks).</summary>
public partial class ProjectProbeJobRunner
{
    public const string RecurringJobId = nameof(ProjectProbeJobRunner);

    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private IServiceScopeFactory serviceScopeFactory = default!;
    [AutoInject] private ServerApiSettings settings = default!;
    [AutoInject] private ILogger<ProjectProbeJobRunner> logger = default!;

    /// <summary>A run that takes longer than a minute must not overlap the next, which would check each project twice.</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 50), AutomaticRetry(Attempts = 0)]
    public async Task ProbeAll(CancellationToken cancellationToken)
    {
        var projectIds = await dbContext.Projects.IgnoreQueryFilters()
                                                 .Where(project => project.ConnectionStatus != ProjectConnectionStatus.ReconnectNeeded && project.ConnectionStatus != ProjectConnectionStatus.Disconnected)
                                                 .Select(project => project.Id)
                                                 .ToListAsync(cancellationToken);

        await Parallel.ForEachAsync(projectIds, new ParallelOptions
        {
            MaxDegreeOfParallelism = settings.Monitoring.Parallelism,
            CancellationToken = cancellationToken
        }, async (projectId, token) =>
        {
            // A DbContext per project: they run side by side, and one project's failure stays its own.
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            try
            {
                await scope.ServiceProvider.GetRequiredService<ProjectProber>().ProbeUnlessUnderway(projectId, token);
            }
            catch (Exception exp) when (exp is not OperationCanceledException)
            {
                logger.LogError(exp, "Checking project {ProjectId} failed.", projectId);
            }
        });
    }
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
