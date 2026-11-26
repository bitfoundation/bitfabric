using Bit.Fabric.Server.Api.Features.Tenants;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Models;

/// <summary>
/// What a project's owner promises their customers and how bit Fabric counts it (bitfabric-internal/specs/monitoring.md,
/// SLA reports): a row of its own rather than columns of <see cref="ProjectMonitoring"/>, whose version every round moves on. A
/// project without one counts with the defaults (See ProjectSlaSettingsDto).
/// </summary>
public partial class ProjectSlaSettings : ITenantAware
{
    public Guid MonitoringId { get; set; }

    public ProjectMonitoring? Monitoring { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    /// <summary>The availability promised a month, such as 99.9.</summary>
    public double TargetPercent { get; set; }

    /// <summary>How much of a minute of partial outage counts as down, 0 to 1.</summary>
    public double PartialWeight { get; set; }

    /// <summary>How much of a minute a part is slow but works counts as down, 0 to 1.</summary>
    public double DegradedWeight { get; set; }

    /// <summary>The credit tiers of the owner's contract, stored as JSON.</summary>
    public List<ProjectSlaCredit> Credits { get; set; } = [];

    public long Version { get; set; }
}

/// <summary>A month below <see cref="BelowPercent"/> gives <see cref="CreditPercent"/> of the month's fee back.</summary>
public partial class ProjectSlaCredit
{
    public double BelowPercent { get; set; }

    public double CreditPercent { get; set; }
}
