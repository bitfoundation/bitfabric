using Bit.Fabric.Shared.Features.Diagnostic;

namespace Bit.Fabric.Shared.Features.Monitoring.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter<ProjectIncidentKind>))]
public enum ProjectIncidentKind
{
    Incident,
    /// <summary>Something the team tells the app's users, such as a new feature or a change, with no impact.</summary>
    Announcement
}

[JsonConverter(typeof(JsonStringEnumConverter<ProjectIncidentPhase>))]
public enum ProjectIncidentPhase
{
    Investigating,
    Identified,
    Monitoring,
    Resolved
}

[JsonConverter(typeof(JsonStringEnumConverter<ProjectAlertChannel>))]
public enum ProjectAlertChannel
{
    Push,
    Email,
    Sms,
    Webhook
}

/// <summary>The shape a webhook's body takes: Slack's and Discord's incoming webhooks, a Microsoft Teams workflow's Adaptive Card, or plain JSON.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectWebhookFormat>))]
public enum ProjectWebhookFormat
{
    Slack,
    Discord,
    Teams,
    Json
}

/// <summary>Why an alert didn't reach its route; the page words it in the reader's language.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectAlertProblem>))]
public enum ProjectAlertProblem
{
    Failed,
    /// <summary>None of the member's devices allows notifications from bit Fabric.</summary>
    NoDevice,
    NoConfirmedEmail,
    NoConfirmedPhoneNumber,
    InvalidWebhook,
    /// <summary>The webhook answered with an error, its status code in <c>ProblemStatusCode</c>.</summary>
    WebhookRefused
}

[JsonConverter(typeof(JsonStringEnumConverter<ProjectAlertKind>))]
public enum ProjectAlertKind
{
    Opened,
    Escalated,
    Resolved,
    /// <summary>The app stopped accepting bit Fabric's approval, so someone has to reconnect it.</summary>
    ConnectionLost,
    Test
}

/// <summary>An incident as the project's page shows it, its texts in the reader's language.</summary>
public partial class ProjectIncidentDto
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public ProjectIncidentKind Kind { get; set; }

    public ProjectHealth Impact { get; set; }

    public string? Title { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public string[] CheckKeys { get; set; } = [];

    /// <summary>What the app calls those parts, in the same order.</summary>
    public string[] CheckTitles { get; set; } = [];

    public bool IsAutomatic { get; set; }

    public DateTimeOffset? AfterDeployAt { get; set; }

    public bool DuringMaintenance { get; set; }

    public string? AcknowledgedBy { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    /// <summary>Newest first.</summary>
    public ProjectIncidentUpdateDto[] Updates { get; set; } = [];

    /// <summary>Who was told, newest first.</summary>
    public ProjectAlertDto[] Alerts { get; set; } = [];

    /// <summary>Once it's over, a prompt for the customer's AI agent to find the cause in the app's own logs.</summary>
    public string? AgentPrompt { get; set; }

    public long Version { get; set; }
}

public partial class ProjectIncidentUpdateDto
{
    public DateTimeOffset At { get; set; }

    public ProjectIncidentPhase Phase { get; set; }

    public string? Text { get; set; }

    /// <summary>Null for bit Fabric's own.</summary>
    public string? Author { get; set; }
}

public partial class ProjectAlertDto
{
    public DateTimeOffset SentAt { get; set; }

    public ProjectAlertKind Kind { get; set; }

    public ProjectAlertChannel Channel { get; set; }

    public string? Target { get; set; }

    public bool Succeeded { get; set; }

    public ProjectAlertProblem? Problem { get; set; }

    /// <summary>The HTTP status a webhook answered with, for <see cref="ProjectAlertProblem.WebhookRefused"/>.</summary>
    public int? ProblemStatusCode { get; set; }
}

[DtoResourceType(typeof(AppStrings))]
public partial class ProjectIncidentUpdateRequestDto
{
    public Guid IncidentId { get; set; }

    public ProjectIncidentPhase Phase { get; set; }

    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [MaxLength(2000, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    [Display(Name = "Update")]
    public string? Text { get; set; }

    public long Version { get; set; }
}

/// <summary>A span of a project's history, which the page draws in the viewer's time zone.</summary>
public partial class ProjectHistoryRequestDto
{
    public Guid ProjectId { get; set; }

    public DateTimeOffset From { get; set; }

    public DateTimeOffset To { get; set; }
}

public partial class ProjectHistoryDto
{
    /// <summary>Each part's stretches in a state other than its first, overlapping the span; open ones have no end.</summary>
    public ProjectPeriodDto[] Periods { get; set; } = [];

    public ProjectIncidentDto[] Incidents { get; set; } = [];

    public ProjectStartDto[] Starts { get; set; } = [];

    public ProjectMaintenanceWindowDto[] Maintenance { get; set; } = [];

    /// <summary>The time before this has no data: the project wasn't watched yet.</summary>
    public DateTimeOffset WatchedSince { get; set; }

    /// <summary>
    /// The parts History draws a row for, named the way the latest report names them: the latest report's parts, and any
    /// other part that wasn't healthy within the span, but none the latest report says only misses its settings.
    /// </summary>
    public ProjectHistoryPartDto[] Parts { get; set; } = [];
}

/// <summary>A part of the app, and the time within the span the app reported it: its days outside that have no data.</summary>
public partial class ProjectHistoryPartDto
{
    public string? Key { get; set; }

    public string? Title { get; set; }

    /// <summary>The start of its first 15 minutes within the span; null when bit Fabric has no record of it, so it counts from the start.</summary>
    public DateTimeOffset? ReportedFrom { get; set; }

    /// <summary>The end of its last 15 minutes within the span; null while the latest report has it.</summary>
    public DateTimeOffset? ReportedTo { get; set; }
}

public partial class ProjectPeriodDto
{
    public string? CheckKey { get; set; }

    public string? Title { get; set; }

    public HealthCheckStatus Status { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public string? Description { get; set; }
}

[DtoResourceType(typeof(AppStrings))]
public partial class ProjectMaintenanceWindowDto
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [MaxLength(200, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    [Display(Name = "What the work is")]
    public string? Title { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public bool RepeatsWeekly { get; set; }

    public long Version { get; set; }
}

/// <summary>A way an incident reaches someone, as the Alerts tab edits it (See ProjectAlertRoute on the server).</summary>
public partial class ProjectAlertRouteDto
{
    public Guid Id { get; set; }

    public int Step { get; set; }

    public int DelayMinutes { get; set; }

    public ProjectAlertChannel Channel { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>The member's name, to show.</summary>
    public string? UserName { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    public ProjectWebhookFormat? WebhookFormat { get; set; }
}

/// <summary>A member of the project's team, who an alert can reach.</summary>
public partial class ProjectTeamMemberDto
{
    public Guid UserId { get; set; }

    public string? Name { get; set; }

    public bool HasEmail { get; set; }

    public bool HasPhoneNumber { get; set; }
}

public partial class ProjectAlertSettingsDto
{
    public Guid ProjectId { get; set; }

    public ProjectAlertRouteDto[] Routes { get; set; } = [];

    /// <summary>Who routes can name; filled by the server, ignored when saving.</summary>
    public ProjectTeamMemberDto[] Members { get; set; } = [];
}

[DtoResourceType(typeof(AppStrings))]
public partial class ProjectSettingsDto
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [MaxLength(100, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    [Display(Name = "Project name")]
    public string? Name { get; set; }

    /// <summary>The IANA time zone an SLA month follows.</summary>
    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [MaxLength(64, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    public string? TimeZoneId { get; set; }

    /// <summary>Who approved bit Fabric in the app; read only.</summary>
    public string? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedOn { get; set; }

    /// <summary>The one address bit Fabric reads with that approval; read only.</summary>
    public string? Reads { get; set; }

    public long Version { get; set; }
}
