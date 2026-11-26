using Bit.Fabric.Server.Api.Features.Tenants;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Models;

/// <summary>
/// 15 minutes of one part of a project, from one region: how many rounds saw it, how many failed or slowed, and the total
/// and longest time it took (bitfabric-internal/specs/monitoring.md, Built for its volume). Written with every round and
/// kept 13 months, so anything wider than a few hours (the cards' uptime, Performance zoomed out, History beyond 30 days)
/// reads these instead of the rounds. Every zone's offset is a multiple of 15 minutes, so a day adds up exactly in any
/// of them.
/// </summary>
public partial class ProjectRollup : ITenantAware
{
    public const int BucketMinutes = 15;

    /// <inheritdoc cref="ProjectRollupKeys.HomePage"/>
    public const string HomePageKey = ProjectRollupKeys.HomePage;

    /// <inheritdoc cref="ProjectRollupKeys.HomePageForVisitors"/>
    public const string HomePageForVisitorsKey = ProjectRollupKeys.HomePageForVisitors;

    /// <inheritdoc cref="ProjectRollupKeys.Report"/>
    public const string ReportKey = ProjectRollupKeys.Report;

    public Guid MonitoringId { get; set; }

    public ProjectMonitoring? Monitoring { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    /// <summary>The start of the 15 minutes, in UTC.</summary>
    public DateTimeOffset BucketStart { get; set; }

    [Required, MaxLength(50)]
    public string? Region { get; set; }

    /// <summary>A report entry's key, or <see cref="HomePageKey"/> or <see cref="ReportKey"/>.</summary>
    [Required, MaxLength(100)]
    public string? CheckKey { get; set; }

    /// <summary>The rounds that looked at it.</summary>
    public int Count { get; set; }

    /// <summary>The rounds it was Unhealthy in, or didn't answer.</summary>
    public int FailedCount { get; set; }

    public int DegradedCount { get; set; }

    /// <summary>The rounds that timed it; one that didn't answer has no time.</summary>
    public int TimedCount { get; set; }

    public long TotalMs { get; set; }

    public int MaxMs { get; set; }

    public static DateTimeOffset BucketOf(DateTimeOffset at)
        => new(at.UtcTicks - at.UtcTicks % TimeSpan.FromMinutes(BucketMinutes).Ticks, TimeSpan.Zero);
}
