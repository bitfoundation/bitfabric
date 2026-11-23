using Bit.Fabric.Server.Api.Features.Tenants;
using Bit.Fabric.Shared.Features.Diagnostic;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Models;

/// <summary>
/// One entry of a project's report in one status, from the round that saw it change until the round that saw the next
/// change: the history pages draw these. Only failing and slow periods and the healthy one in between are worth a row, so
/// a check that stays healthy costs nothing.
/// </summary>
public partial class ProjectCheckPeriod : ITenantAware
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Project? Project { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Required, MaxLength(100)]
    public string? CheckKey { get; set; }

    /// <summary>What the app called the check when the period opened, such as "Database" (See ProjectProbeCheck.Title).</summary>
    [MaxLength(100)]
    public string? Title { get; set; }

    public HealthCheckStatus Status { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Null while it lasts.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>The entry's description in the round that opened the period.</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
}
