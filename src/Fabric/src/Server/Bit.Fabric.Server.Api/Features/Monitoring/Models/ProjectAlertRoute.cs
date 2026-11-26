using Bit.Fabric.Server.Api.Features.Identity.Models;
using Bit.Fabric.Server.Api.Features.Tenants;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Models;

/// <summary>
/// One way an incident reaches someone (bitfabric-internal/specs/monitoring.md, Alerts): a push or an email to a team
/// member, an SMS, or a webhook such as a Slack channel's. Routes of step 0 go out at once; a later step goes out after
/// its delay unless someone pressed "I'm on it". Everyone a step reached hears when it's over.
/// </summary>
public partial class ProjectAlertRoute : ITenantAware
{
    public Guid Id { get; set; }

    public Guid MonitoringId { get; set; }

    public ProjectMonitoring? Monitoring { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public int Step { get; set; }

    /// <summary>Minutes after the incident began; 0 for step 0.</summary>
    public int DelayMinutes { get; set; }

    public ProjectAlertChannel Channel { get; set; }

    /// <summary>The team member a push, an email or an SMS goes to.</summary>
    public Guid? UserId { get; set; }

    public User? User { get; set; }

    /// <summary>A webhook's address; https and public, as every address bit Fabric calls (See PublicAddressGuard).</summary>
    [MaxLength(500)]
    public string? Address { get; set; }

    public ProjectWebhookFormat? WebhookFormat { get; set; }

    /// <summary>The language its messages are written in: its author's, when it was made.</summary>
    [Required, MaxLength(10)]
    public string? Culture { get; set; }

    public long Version { get; set; }
}

/// <summary>An alert bit Fabric sent, or failed to, which the incident's page lists.</summary>
public partial class ProjectAlert : ITenantAware
{
    public Guid Id { get; set; }

    public Guid MonitoringId { get; set; }

    public ProjectMonitoring? Monitoring { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    /// <summary>Null for an alert that isn't about an incident, such as a lost connection or a test.</summary>
    public Guid? IncidentId { get; set; }

    public ProjectIncident? Incident { get; set; }

    public ProjectAlertKind Kind { get; set; }

    public ProjectAlertChannel Channel { get; set; }

    /// <summary>Who or where, for the list: a name, an address with its middle hidden, or a webhook's host.</summary>
    [MaxLength(200)]
    public string? Target { get; set; }

    public DateTimeOffset SentAt { get; set; }

    public bool Succeeded { get; set; }

    public ProjectAlertProblem? Problem { get; set; }

    /// <summary>The HTTP status a webhook answered with, for <see cref="ProjectAlertProblem.WebhookRefused"/>.</summary>
    public int? ProblemStatusCode { get; set; }
}
