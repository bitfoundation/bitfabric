using Bit.Fabric.Server.Api.Features.Tenants;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Models;

/// <summary>
/// A start of the app's process, seen as the report's start time moving on for an instance: with a new build it is a
/// deployment, with the same one a restart (bitfabric-internal/specs/monitoring.md, Deployments and restarts).
/// </summary>
public partial class ProjectStart : ITenantAware
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Project? Project { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    /// <summary>When the process started, as its report says.</summary>
    public DateTimeOffset At { get; set; }

    [Required, MaxLength(200)]
    public string? ApplicationVersion { get; set; }

    [MaxLength(200)]
    public string? InstanceName { get; set; }

    public bool IsDeploy { get; set; }
}
