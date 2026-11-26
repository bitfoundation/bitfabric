namespace Bit.Fabric.Shared.Features.Monitoring.Dtos;

/// <summary>A project's status page as its owner edits it (bitfabric-internal/specs/monitoring.md, Status page).</summary>
[DtoResourceType(typeof(AppStrings))]
public partial class StatusPageSettingsDto
{
    public Guid MonitoringId { get; set; }

    /// <summary>The page is at <c>/status/{slug}</c>: lowercase letters, digits and hyphens.</summary>
    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [MaxLength(60, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    [Display(Name = nameof(AppStrings.Address))]
    public string? Slug { get; set; }

    public bool IsPublished { get; set; }

    public bool ShowsResponseTime { get; set; }

    /// <summary>The languages the owner writes updates in, the main one first.</summary>
    public string[] Languages { get; set; } = [];

    [MaxLength(200, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    [Display(Name = nameof(AppStrings.PromptKindSupport))]
    public string? SupportAddress { get; set; }

    public StatusComponentDto[] Components { get; set; } = [];

    /// <summary>Where the page is: bit Fabric's address for it, or the customer's own once it's active. Read only.</summary>
    public string? PublicUrl { get; set; }

    /// <summary>Such as <c>status.myapp.com</c>. Read only: connected through <c>ConnectCustomHostname</c>.</summary>
    public string? CustomHostname { get; set; }

    /// <summary>Cloudflare's state of that host name, such as "pending" or "active". Read only.</summary>
    public string? CustomHostnameState { get; set; }

    /// <summary>What the customer's CNAME record points at. Read only; null while custom host names aren't set up.</summary>
    public string? CnameTarget { get; set; }

    /// <summary>How many browsers and devices follow the page. Read only.</summary>
    public int FollowerCount { get; set; }

    /// <summary>The report entries a component can be made of, with what the app calls them. Read only.</summary>
    public StatusCheckDto[] Checks { get; set; } = [];

    public long Version { get; set; }
}

/// <summary>A part of the app as its users know it, its name and description in each language the owner writes.</summary>
public partial class StatusComponentDto
{
    public Guid Id { get; set; }

    public Dictionary<string, string> Name { get; set; } = [];

    public Dictionary<string, string> Description { get; set; } = [];

    public string[] CheckKeys { get; set; } = [];

    public bool IsShown { get; set; } = true;
}

public partial class StatusCheckDto
{
    public string? Key { get; set; }

    public string? Title { get; set; }

    public string? Category { get; set; }
}

[DtoResourceType(typeof(AppStrings))]
public partial class ConnectCustomHostnameRequestDto
{
    public Guid MonitoringId { get; set; }

    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [MaxLength(253, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    [Display(Name = nameof(AppStrings.MonitoringHostName))]
    public string? Hostname { get; set; }
}

/// <summary>An incident, an update to one, or an announcement the team posts (bitfabric-internal/specs/monitoring.md, Posting an update).</summary>
public partial class StatusPostRequestDto
{
    public Guid MonitoringId { get; set; }

    /// <summary>An update to this incident; null for a new one.</summary>
    public Guid? IncidentId { get; set; }

    public ProjectIncidentKind Kind { get; set; }

    /// <summary>Degraded or Down for an incident.</summary>
    public ProjectHealth Impact { get; set; } = ProjectHealth.Degraded;

    public Guid[] ComponentIds { get; set; } = [];

    public ProjectIncidentPhase Phase { get; set; }

    /// <summary>The title in each language, by culture name; a new one needs at least the page's main language.</summary>
    public Dictionary<string, string> Title { get; set; } = [];

    /// <summary>What the team says, in each language.</summary>
    public Dictionary<string, string> Text { get; set; } = [];

    /// <summary>Whether the page's followers get a push about it, in their language.</summary>
    public bool NotifiesFollowers { get; set; } = true;

    public long Version { get; set; }
}

/// <summary>An AI draft of a text in the languages the team hasn't written it in yet, for them to review.</summary>
public partial class DraftTranslationsRequestDto
{
    public string? FromCulture { get; set; }

    public string? Text { get; set; }

    public string[] ToCultures { get; set; } = [];
}

/// <summary>A status page as a visitor sees it, in their language; no report key or description leaves bit Fabric.</summary>
public partial class PublicStatusDto
{
    public string? Slug { get; set; }

    public string? Name { get; set; }

    /// <summary>The app's own address, which the page links back to.</summary>
    public string? AppAddress { get; set; }

    /// <summary>The app's own icon, an attachment of its own (See MonitoringDto.IconId); null for none, and the page shows a monogram.</summary>
    public Guid? IconId { get; set; }

    /// <summary>Operational, Degraded, Down, or NoData when bit Fabric can't read the app right now.</summary>
    public ProjectHealth State { get; set; }

    public bool InMaintenance { get; set; }

    /// <summary>The culture the texts are in.</summary>
    public string? Culture { get; set; }

    /// <summary>The languages the owner writes in, the main one first.</summary>
    public string[] Languages { get; set; } = [];

    public DateTimeOffset? CheckedAt { get; set; }

    public DateTimeOffset WatchedSince { get; set; }

    /// <summary>The home page's median time over the last 24 hours, past any cache: the server's own.</summary>
    public int? MedianResponseMs { get; set; }

    /// <summary>The median time a visitor waited for it, through the cache in front of the app; null when no cache answered.</summary>
    public int? MedianVisitorMs { get; set; }

    public string? SupportAddress { get; set; }

    /// <summary>The Web Push public key a visitor's browser subscribes with; null where push isn't set up.</summary>
    public string? PushPublicKey { get; set; }

    public PublicComponentDto[] Components { get; set; } = [];

    /// <summary>Ongoing incidents, newest first.</summary>
    public PublicIncidentDto[] Active { get; set; } = [];

    /// <summary>Maintenance windows going on or coming in the next 30 days.</summary>
    public PublicMaintenanceDto[] Maintenance { get; set; } = [];

    /// <summary>The incidents and announcements of the last 90 days that are over, newest first.</summary>
    public PublicIncidentDto[] History { get; set; } = [];
}

public partial class PublicComponentDto
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public ProjectHealth State { get; set; }

    /// <summary>Its stretches down or degraded over the last 90 days, from its incidents.</summary>
    public PublicStretchDto[] Stretches { get; set; } = [];
}

public partial class PublicStretchDto
{
    public DateTimeOffset From { get; set; }

    public DateTimeOffset? To { get; set; }

    public ProjectHealth Impact { get; set; }
}

public partial class PublicIncidentDto
{
    public Guid Id { get; set; }

    public ProjectIncidentKind Kind { get; set; }

    public ProjectHealth Impact { get; set; }

    public string? Title { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public Guid[] ComponentIds { get; set; } = [];

    /// <summary>Newest first.</summary>
    public PublicIncidentUpdateDto[] Updates { get; set; } = [];
}

public partial class PublicIncidentUpdateDto
{
    public DateTimeOffset At { get; set; }

    public ProjectIncidentPhase Phase { get; set; }

    public string? Text { get; set; }

    /// <summary>Not written in the visitor's language: shown in the page's main one.</summary>
    public bool IsFallback { get; set; }
}

public partial class PublicMaintenanceDto
{
    public string? Title { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public bool RepeatsWeekly { get; set; }
}

/// <summary>A visitor's browser following a page by push.</summary>
public partial class FollowStatusPageRequestDto
{
    public string? Slug { get; set; }

    public string? Endpoint { get; set; }

    public string? P256dh { get; set; }

    public string? Auth { get; set; }

    public string? Culture { get; set; }

    public Guid[] ComponentIds { get; set; } = [];
}
