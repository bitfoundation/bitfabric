using Bit.Fabric.Server.Api.Features.Monitoring.Models;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>One part of one round, as its 15 minutes count it (See ProjectRollup).</summary>
/// <param name="MonitoringId">The round's project.</param>
/// <param name="TenantId">The project's tenant.</param>
/// <param name="Region">Where the round ran from.</param>
/// <param name="At">When the round ran, which picks its 15 minutes.</param>
/// <param name="Key">A report entry's key, or ProjectRollup.HomePageKey or ProjectRollup.ReportKey.</param>
/// <param name="Failed">Unhealthy, or no answer.</param>
/// <param name="Degraded">Degraded.</param>
/// <param name="Ms">How long it took; null when it didn't answer, or when the time was counted by an earlier round.</param>
public readonly record struct ProjectRollupPart(Guid MonitoringId, Guid TenantId, string Region, DateTimeOffset At, string Key, bool Failed, bool Degraded, int? Ms);

/// <summary>
/// Adds rounds to their 15 minute rollups (bitfabric-internal/specs/monitoring.md, Built for its volume): bit Fabric's own
/// rounds, a row per part (See ProjectProber), and the bitplatform Network's readings, a home page row each (See
/// MonitoringNetworkController).
/// </summary>
public static class ProjectRollupWriter
{
    /// <summary>
    /// One upsert statement however many parts and projects, safe when two writers add to the same 15 minutes at once,
    /// such as a reconnect's round and the job's.
    /// </summary>
    public static async Task Add(AppDbContext dbContext, IEnumerable<ProjectRollupPart> parts, CancellationToken cancellationToken)
    {
        // ON CONFLICT updates a row once per statement, so parts of the same 15 minutes in one batch are added up here.
        var rows = parts.GroupBy(part => (part.MonitoringId, part.TenantId, Bucket: ProjectRollup.BucketOf(part.At), part.Region, part.Key))
                        .Select(group => (group.Key,
                                          Count: group.Count(),
                                          Failed: group.Count(part => part.Failed),
                                          Degraded: group.Count(part => part.Degraded),
                                          Timed: group.Count(part => part.Ms is not null),
                                          TotalMs: group.Sum(part => (long)(part.Ms ?? 0)),
                                          MaxMs: group.Max(part => part.Ms ?? 0)))
                        .ToArray();

        if (rows.Length is 0)
            return;

        Guid[] monitoringIds = [.. rows.Select(row => row.Key.MonitoringId)];
        Guid[] tenantIds = [.. rows.Select(row => row.Key.TenantId)];
        DateTimeOffset[] buckets = [.. rows.Select(row => row.Key.Bucket)];
        string[] regions = [.. rows.Select(row => row.Key.Region)];
        string[] keys = [.. rows.Select(row => row.Key.Key)];
        int[] counts = [.. rows.Select(row => row.Count)];
        int[] failed = [.. rows.Select(row => row.Failed)];
        int[] degraded = [.. rows.Select(row => row.Degraded)];
        int[] timed = [.. rows.Select(row => row.Timed)];
        long[] totalMs = [.. rows.Select(row => row.TotalMs)];
        int[] maxMs = [.. rows.Select(row => row.MaxMs)];

        await dbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO "ProjectRollups" ("MonitoringId", "BucketStart", "Region", "CheckKey", "TenantId", "Count", "FailedCount", "DegradedCount", "TimedCount", "TotalMs", "MaxMs")
            SELECT part.project_id, part.bucket, part.region, part.key, part.tenant_id, part.count, part.failed, part.degraded, part.timed, part.total_ms, part.max_ms
            FROM unnest({monitoringIds}, {buckets}, {regions}, {keys}, {tenantIds}, {counts}, {failed}, {degraded}, {timed}, {totalMs}, {maxMs})
                AS part(project_id, bucket, region, key, tenant_id, count, failed, degraded, timed, total_ms, max_ms)
            ON CONFLICT ("MonitoringId", "BucketStart", "Region", "CheckKey") DO UPDATE SET
                "Count" = "ProjectRollups"."Count" + EXCLUDED."Count",
                "FailedCount" = "ProjectRollups"."FailedCount" + EXCLUDED."FailedCount",
                "DegradedCount" = "ProjectRollups"."DegradedCount" + EXCLUDED."DegradedCount",
                "TimedCount" = "ProjectRollups"."TimedCount" + EXCLUDED."TimedCount",
                "TotalMs" = "ProjectRollups"."TotalMs" + EXCLUDED."TotalMs",
                "MaxMs" = GREATEST("ProjectRollups"."MaxMs", EXCLUDED."MaxMs")
            """, cancellationToken);
    }
}
