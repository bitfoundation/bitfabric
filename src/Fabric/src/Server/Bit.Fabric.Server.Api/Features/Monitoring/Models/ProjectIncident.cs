using Bit.Fabric.Server.Api.Features.Tenants;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Models;

/// <summary>
/// Something that went wrong in a project's app, from the round that confirmed it until the round that confirmed it was
/// over (bitfabric-internal/specs/monitoring.md, Checks): bit Fabric opens one when a part fails two rounds in a row and
/// closes it after two passing rounds. Its texts are written in every language bit Fabric speaks, so each reader and each
/// alert gets its own.
/// </summary>
public partial class ProjectIncident : ITenantAware
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Project? Project { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    /// <summary>An incident, or an announcement the team posted on the status page, which has no impact.</summary>
    public ProjectIncidentKind Kind { get; set; }

    /// <summary>Down when the app can't be reached or a part it can't work without fails, Degraded otherwise.</summary>
    public ProjectHealth Impact { get; set; }

    /// <summary>The title in each language, such as "Email isn't working".</summary>
    public List<ProjectText> Title { get; set; } = [];

    /// <summary>The first failing round's time, so the incident covers the minute it began rather than the one that confirmed it.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>The first passing round's time; null while it lasts.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>The parts it is about, by their report keys; the app itself is <see cref="ProjectRollup.ReportKey"/>.</summary>
    public string[] CheckKeys { get; set; } = [];

    /// <summary>
    /// The status page components a team's own post is about. An automatic incident names none: its components are the
    /// ones made of its <see cref="CheckKeys"/>, or every one when the app itself doesn't answer.
    /// </summary>
    public Guid[] ComponentIds { get; set; } = [];

    /// <summary>Opened from the checks by bit Fabric, rather than declared by the customer.</summary>
    public bool IsAutomatic { get; set; }

    /// <summary>A deployment within 30 minutes before it began, its likeliest cause.</summary>
    public DateTimeOffset? AfterDeployAt { get; set; }

    /// <summary>It began during a maintenance window, so nobody was alerted and it doesn't count against the SLA.</summary>
    public bool DuringMaintenance { get; set; }

    /// <summary>Who pressed "I'm on it", which stops the escalation.</summary>
    public Guid? AcknowledgedByUserId { get; set; }

    [MaxLength(200)]
    public string? AcknowledgedBy { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    /// <summary>The last escalation step alerted (See ProjectAlertRoute.Step).</summary>
    public int EscalationStep { get; set; }

    /// <summary>When the next step is due, while nobody acknowledged it; null when there is no next step.</summary>
    public DateTimeOffset? NextEscalationAt { get; set; }

    /// <summary>What happened, oldest first.</summary>
    public List<ProjectIncidentUpdate> Updates { get; set; } = [];

    public long Version { get; set; }
}

/// <summary>A text in one language, by its culture name, such as "fa-IR".</summary>
public partial class ProjectText
{
    [MaxLength(10)]
    public string? Culture { get; set; }

    [MaxLength(2000)]
    public string? Text { get; set; }
}

/// <summary>One step of an incident's story: bit Fabric's own, or one its customer's team wrote.</summary>
public partial class ProjectIncidentUpdate
{
    public DateTimeOffset At { get; set; }

    public ProjectIncidentPhase Phase { get; set; }

    public List<ProjectText> Text { get; set; } = [];

    /// <summary>Who wrote it; null for bit Fabric.</summary>
    [MaxLength(200)]
    public string? Author { get; set; }
}
