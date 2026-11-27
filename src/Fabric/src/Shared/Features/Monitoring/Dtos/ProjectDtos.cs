using Bit.Fabric.Shared.Features.Diagnostic;

namespace Bit.Fabric.Shared.Features.Monitoring.Dtos;

/// <summary>How a project is doing, as its card and its page say it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectHealth>))]
public enum ProjectHealth
{
    /// <summary>Nothing read yet, or the connection is lost.</summary>
    NoData,
    Operational,
    /// <summary>A part fails or is slow while the app itself works.</summary>
    Degraded,
    /// <summary>The app doesn't answer, or a part it can't work without fails.</summary>
    Down
}

[JsonConverter(typeof(JsonStringEnumConverter<ProjectConnectionStatus>))]
public enum ProjectConnectionStatus
{
    /// <summary>Connected, the first rounds not in yet.</summary>
    FirstChecks,
    Connected,
    /// <summary>Its token was refused or couldn't be refreshed, so nothing is read until someone reconnects.</summary>
    ReconnectNeeded,
    /// <summary>Someone of the team disconnected it on its Settings tab: bit Fabric forgot its approval and reads nothing, and keeps its history.</summary>
    Disconnected
}

/// <summary>What the add flow found at an address.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AddressProbeOutcome>))]
public enum AddressProbeOutcome
{
    /// <summary>The app says where its server is, and that server serves a health report bit Fabric can read.</summary>
    Found,
    /// <summary>
    /// Something answers, but it doesn't say it's a bit app, or its server serves no health report bit Fabric can read:
    /// another kind of app, or a bit app too old to say. "Unknown", never "old" (bitfabric-internal/specs/monitoring.md).
    /// </summary>
    NotFound,
    /// <summary>Nothing answered, or the address isn't one bit Fabric may open (not https, not public).</summary>
    Unreachable
}

[DtoResourceType(typeof(AppStrings))]
public partial class ProbeAddressRequestDto
{
    /// <summary>The app's address, as the customer typed it: <c>myapp.com</c> or <c>https://myapp.com</c>.</summary>
    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [MaxLength(300, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    [Display(Name = "Your app's address")]
    public string? Address { get; set; }
}

public partial class AddressProbeResultDto
{
    public AddressProbeOutcome Outcome { get; set; }

    /// <summary>The app's address, normalized to <c>https://host</c>.</summary>
    public string? Address { get; set; }

    /// <summary>Where the health report is read, <c>https://host</c>, as the app's <c>/.well-known/bit-app.json</c> names it.</summary>
    public string? ServerAddress { get; set; }

    /// <summary>The app's pages are hosted statically, so its server's <c>/health</c> is what a visitor's wait is timed on.</summary>
    public bool HasStaticFrontEnd { get; set; }

    /// <summary>The bit template version the app says it's made with, or null when it doesn't say.</summary>
    public string? TemplateVersion { get; set; }

    /// <summary>The app's own name, from its web app manifest, to suggest as the project's.</summary>
    public string? AppTitle { get; set; }

    /// <summary>Whether a project of the same address is already watched in this tenant.</summary>
    public bool IsAlreadyAdded { get; set; }
}

[DtoResourceType(typeof(AppStrings))]
public partial class StartConnectRequestDto
{
    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [MaxLength(300, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    public string? Address { get; set; }

    [MaxLength(100, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    [Display(Name = "Project name")]
    public string? Name { get; set; }

    /// <summary>
    /// The project the environment joins, from its page's "Add your server"; null for a new project named
    /// <see cref="Name"/>. Either way, a project of the team with the id the app reports takes it instead.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Production, Test, Staging or a name the team gives; Production when empty.</summary>
    [MaxLength(50, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    [Display(Name = "Environment")]
    public string? Environment { get; set; }

    /// <summary>The native app's local server, which the approval's answer goes back to (See ILocalHttpServer); null on the web.</summary>
    public int? LocalHttpPort { get; set; }

    [MaxLength(100, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    public string? LocalHttpNonce { get; set; }
}

public partial class StartConnectResponseDto
{
    /// <summary>The customer app's sign-in and approval page, which opens beside the add page.</summary>
    public string? AuthorizeUrl { get; set; }
}

public partial class CompleteConnectRequestDto
{
    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    public string? State { get; set; }

    /// <summary>Null when the customer's app answered with an error instead, such as <c>access_denied</c>.</summary>
    public string? Code { get; set; }

    public string? Error { get; set; }

    /// <summary>RFC 9207's issuer, which must name the app the approval was asked of.</summary>
    public string? Iss { get; set; }

    /// <summary>The IANA time zone of whoever adds the project, which its SLA months first follow.</summary>
    [MaxLength(64)]
    public string? TimeZoneId { get; set; }
}

/// <summary>One environment of a project as its card shows it, and as the top of its page does.</summary>
public partial class MonitoringDto
{
    public Guid Id { get; set; }

    /// <summary>What alerts and status pages call it: the project's name, with the environment after it unless that is Production.</summary>
    public string? Name { get; set; }

    /// <summary>The project it is an environment of.</summary>
    public Guid ProjectId { get; set; }

    public string? ProjectName { get; set; }

    /// <summary>Production, Test, Staging or a name the team gave.</summary>
    public string? Environment { get; set; }

    /// <summary><c>https://host</c>.</summary>
    public string? Address { get; set; }

    /// <summary>Null when the report is read on the address itself.</summary>
    public string? ServerAddress { get; set; }

    public string? TemplateVersion { get; set; }

    /// <summary>The app's own icon, served as an attachment (<c>api/v1/Attachment/GetAttachment/{IconId}/ProjectIcon</c>); null for none.</summary>
    public Guid? IconId { get; set; }

    public ProjectHealth Health { get; set; }

    public ProjectConnectionStatus ConnectionStatus { get; set; }

    /// <summary>Why the connection was lost, for one that needs reconnecting, in the reader's language (See ProjectConnectionProblems).</summary>
    public string? ConnectionProblem { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>The share of the last 30 days the app worked, of the time it was watched; null before the first round.</summary>
    public double? Uptime30Days { get; set; }

    /// <summary>The median time the home page took over the last 24 hours, in milliseconds, past any cache: the server's own.</summary>
    public int? MedianMs { get; set; }

    public int? P95Ms { get; set; }

    /// <summary>
    /// The median time a visitor waited for the home page over the last 24 hours, through the cache in front of the app;
    /// null when no cache answered it, which leaves one number, <see cref="MedianMs"/>.
    /// </summary>
    public int? MedianVisitorMs { get; set; }

    /// <summary>The last 24 hours' hourly medians, oldest first, 0 for an hour without a round.</summary>
    public int[] ResponseLast24Hours { get; set; } = [];

    /// <summary>
    /// The last 30 days in the time zone the cards were asked for, oldest first, today last, each colored by how its rounds
    /// went (bitfabric-internal/specs/monitoring.md, The projects page): the card's day bars.
    /// </summary>
    public ProjectDayDto[] Last30Days { get; set; } = [];

    /// <summary>The checks failing or slow right now, by what the app calls them.</summary>
    public string[] FailingChecks { get; set; } = [];

    /// <summary>The open incident, when one is open, which "I'm on it" and the team's updates name.</summary>
    public Guid? OngoingIncidentId { get; set; }

    /// <summary>The open incident's title in the reader's language, when one is open.</summary>
    public string? OngoingIncident { get; set; }

    public DateTimeOffset? OngoingIncidentSince { get; set; }

    /// <summary>Whether the last answer came from a cache that ignored the query string (bitfabric-internal/specs/monitoring.md, Response time).</summary>
    public bool HasCacheWarning { get; set; }

    /// <summary>The bitplatform Network's locations the project is checked from besides Germany, by code (See NetworkLocations).</summary>
    public string[] Locations { get; set; } = [];

    /// <summary>The newest scan of each kind bit Fabric runs, as the card's scans row shows it (bitfabric-internal/specs/scanners.md).</summary>
    public ProjectScanSummaryDto[] Scans { get; set; } = [];

    public long Version { get; set; }
}

/// <summary>
/// A day of a project as its card's bar draws it, from the 15 minute rollups of its report: a round is a minute, so a
/// round the app didn't answer, or answered Unhealthy, is a minute down, and a Degraded one a minute degraded.
/// </summary>
public partial class ProjectDayDto
{
    public DateOnly Date { get; set; }

    /// <summary>Down when a round found it down, Degraded when one found a part failing, NoData without a round.</summary>
    public ProjectDayTone Tone { get; set; }

    /// <summary>The share of the day's rounds the app wasn't down in, as a percentage; null without a round.</summary>
    public double? Uptime { get; set; }

    public int DownMinutes { get; set; }

    public int DegradedMinutes { get; set; }
}

/// <summary>One entry of the project's health report, as its Overview shows it.</summary>
public partial class ProjectCheckDto
{
    /// <summary>The entry's name in the report, such as <c>smtp</c>.</summary>
    public string? Key { get; set; }

    public HealthCheckStatus Status { get; set; }

    public string? Description { get; set; }

    public int DurationMs { get; set; }

    /// <summary>The entry's limit, when the app sets one, to show how close a slow check came.</summary>
    public int? TimeoutMs { get; set; }

    /// <summary>The kind of dependency, from the report: Platform, Messaging, Identity, AI, Integrations or the app's own.</summary>
    public string? Category { get; set; }

    public string[] Tags { get; set; } = [];

    /// <summary>What the app calls the check, such as "Database", in its own words.</summary>
    public string? Title { get; set; }

    /// <summary>The check's <c>BitIconName</c>, from the report.</summary>
    public string? IconName { get; set; }

    /// <summary>What a pass proves, from the report.</summary>
    public string? About { get; set; }

    /// <summary>What the check talks to, such as "Twilio", from the report.</summary>
    public string? Provider { get; set; }

    /// <summary>The host the check reaches, from the report.</summary>
    public string? Host { get; set; }

    /// <summary>The model an AI check uses, from the report.</summary>
    public string? Model { get; set; }

    /// <summary>The check only misses its settings: a setup finding, not an outage.</summary>
    public bool IsNotConfigured { get; set; }

    /// <summary>When the app last really ran a check whose result it reuses; null for one that runs every time.</summary>
    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>Since when the entry has been in this status.</summary>
    public DateTimeOffset? Since { get; set; }

    /// <summary>The entry in the project's last rounds, oldest first.</summary>
    public ProjectCheckRoundDto[] Recent { get; set; } = [];
}

/// <summary>A part of the app in one round.</summary>
public partial class ProjectCheckRoundDto
{
    public DateTimeOffset At { get; set; }

    public HealthCheckStatus Status { get; set; }

    /// <summary>Null when the app reused a result it had measured before.</summary>
    public int? DurationMs { get; set; }
}

/// <summary>A project's page: the project and its last report, entry by entry.</summary>
public partial class MonitoringDetailsDto
{
    public MonitoringDto? Monitoring { get; set; }

    public ProjectCheckDto[] Checks { get; set; } = [];

    /// <summary>When the newest report was read.</summary>
    public DateTimeOffset? CheckedAt { get; set; }

    /// <summary>How long reading the newest report took.</summary>
    public int? ReportMs { get; set; }

    /// <summary>How long the home page took in the round that read the newest report, from bit Fabric's own server.</summary>
    public int? HomePageMs { get; set; }

    /// <summary>How long a visitor waited for it in that round, through the cache in front of the app; null when no cache answered.</summary>
    public int? HomePageVisitorMs { get; set; }

    /// <summary>The build the app runs, from its report (the commit after a '+').</summary>
    public string? ApplicationVersion { get; set; }

    public DateTimeOffset? LastDeployedAt { get; set; }

    /// <summary>When the same build last started again on a new instance (See ProjectStartDto.IsDeploy).</summary>
    public DateTimeOffset? LastRestartedAt { get; set; }

    /// <summary>The regions whose last round counts, such as "Germany".</summary>
    public string[] Regions { get; set; } = [];

    /// <summary>The newest reading of each of the bitplatform Network's locations, with the data center it ran in.</summary>
    public ProjectNetworkReadingDto[] NetworkReadings { get; set; } = [];
}

/// <summary>A reading of the bitplatform Network: how long the home page took from one location, and where it really ran.</summary>
public partial class ProjectNetworkReadingDto
{
    /// <summary>The location's code, such as <c>enam</c>.</summary>
    public string? Location { get; set; }

    /// <summary>The data center the reading ran in, such as <c>IAD</c>: a location is a hint, which Cloudflare follows as best it can.</summary>
    public string? Colo { get; set; }

    public DateTimeOffset At { get; set; }

    /// <summary>Null when the app didn't answer from there.</summary>
    public int? ResponseMs { get; set; }
}

/// <summary>The bitplatform Network's locations a project is checked from, replacing the ones it had.</summary>
[DtoResourceType(typeof(AppStrings))]
public partial class SetProjectLocationsRequestDto
{
    public Guid MonitoringId { get; set; }

    /// <summary>Codes of NetworkLocations; none leaves only bit Fabric's own server in Germany.</summary>
    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    public string[] Locations { get; set; } = [];
}

/// <summary>
/// What the Performance tab asks for: a span of a project's history and how many minutes a bar may cover. The server may
/// make the bars wider, so no row gets more than it can draw.
/// </summary>
public partial class ProjectPerformanceRequestDto
{
    public Guid MonitoringId { get; set; }

    public DateTimeOffset From { get; set; }

    public DateTimeOffset To { get; set; }

    /// <summary>1 for every round, 15, 60, 360 or 1440 from the rollups.</summary>
    public int StepMinutes { get; set; }
}

/// <summary>How long each part of a project took over a span of time, a point per bar (bitfabric-internal/specs/monitoring.md, Performance).</summary>
public partial class ProjectPerformanceDto
{
    /// <summary>How many minutes each point covers, which may be wider than asked.</summary>
    public int StepMinutes { get; set; }

    public ProjectPerformanceSeriesDto[] Series { get; set; } = [];

    /// <summary>The deployments and restarts in the span.</summary>
    public ProjectStartDto[] Starts { get; set; } = [];
}

/// <summary>One part from one region: a report entry's key, <c>$home</c> for the home page or <c>$report</c> for the report.</summary>
public partial class ProjectPerformanceSeriesDto
{
    public string? Key { get; set; }

    public string? Region { get; set; }

    public ProjectPerformancePointDto[] Points { get; set; } = [];
}

public partial class ProjectPerformancePointDto
{
    public DateTimeOffset Start { get; set; }

    /// <summary>The rounds in it.</summary>
    public int Count { get; set; }

    /// <summary>The rounds it was Unhealthy in, or didn't answer.</summary>
    public int Failed { get; set; }

    public int Degraded { get; set; }

    /// <summary>The average time of the rounds that timed it; null when none answered.</summary>
    public int? AverageMs { get; set; }

    public int? MaxMs { get; set; }
}

public partial class ProjectStartDto
{
    public DateTimeOffset At { get; set; }

    public bool IsDeploy { get; set; }

    public string? ApplicationVersion { get; set; }
}

/// <summary>What a project's Troubleshoot button hands to the customer's own AI (See IMonitoringsController.GetTroubleshootPrompt).</summary>
public partial class TroubleshootPromptDto
{
    public TroubleshootPromptKind Kind { get; set; }

    /// <summary>The prompt, in English whatever the viewer's language, as Markdown.</summary>
    public string? Prompt { get; set; }
}

public enum TroubleshootPromptKind
{
    /// <summary>The environment needs reconnecting: why its health report can't be read.</summary>
    Connection,

    /// <summary>Anything else: how the app is doing, and what fails.</summary>
    App
}

/// <summary>The series of a project's performance that aren't report entries.</summary>
public static class ProjectRollupKeys
{
    /// <summary>The home page, or a static front end's server's <c>/health</c>, past any cache: the server's own time.</summary>
    public const string HomePage = "$home";

    /// <summary>The home page as a visitor gets it, through the cache in front of the app, when one answered it.</summary>
    public const string HomePageForVisitors = "$home-visitors";

    /// <summary>The health report as a whole: reading it, and its overall status.</summary>
    public const string Report = "$report";
}
