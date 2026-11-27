using Bit.Fabric.Server.Api.Features.Tenants;
using Bit.Fabric.Shared.Features.Diagnostic;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Models;

/// <summary>One round of checking a project from one region: its report, entry by entry, and how fast it answered.</summary>
public partial class ProjectProbe : ITenantAware
{
    public Guid Id { get; set; }

    public Guid MonitoringId { get; set; }

    public ProjectMonitoring? Monitoring { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public DateTimeOffset At { get; set; }

    /// <summary>Where the round ran from, such as "Germany", or the name of a location of the bitplatform Network.</summary>
    [Required, MaxLength(50)]
    public string? Region { get; set; }

    /// <summary>
    /// The data center a reading of the bitplatform Network really ran in, such as <c>IAD</c>, since a location is a hint
    /// Cloudflare follows as best it can; null for a round of bit Fabric's own server, which reads the
    /// report. A round with a Colo times the front door alone: it has no report, no checks and no build.
    /// </summary>
    [MaxLength(8)]
    public string? Colo { get; set; }

    public ProjectProbeOutcome Outcome { get; set; }

    /// <summary>
    /// The worst status among the report's configured checks, which is the report's own status unless a check only misses
    /// its settings; null when there was no report to read.
    /// </summary>
    public HealthCheckStatus? Status { get; set; }

    /// <summary>How long the home page (or a static front end's server's <c>/health</c>) took, in milliseconds, past any cache.</summary>
    public int? ResponseMs { get; set; }

    /// <summary>
    /// How long the same page took asked for as a visitor asks for it, with no query string of bit Fabric's, when a cache in
    /// front of the app answered it; null when none did, as the two numbers are then the same.
    /// </summary>
    public int? VisitorMs { get; set; }

    /// <summary>How long reading <c>/healthz</c> took, in milliseconds.</summary>
    public int? ReportMs { get; set; }

    /// <summary>The home page came from a cache that ignored its unique query string.</summary>
    public bool HasCacheWarning { get; set; }

    /// <summary>The build the report says runs, the commit after a '+'.</summary>
    [MaxLength(200)]
    public string? ApplicationVersion { get; set; }

    [MaxLength(200)]
    public string? InstanceName { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Why the round failed, in words for the customer, when it did.</summary>
    [MaxLength(500)]
    public string? Problem { get; set; }

    /// <summary>Each report entry, stored as JSON.</summary>
    public List<ProjectProbeCheck> Checks { get; set; } = [];
}

/// <summary>One entry of a round's report.</summary>
public partial class ProjectProbeCheck
{
    [MaxLength(100)]
    public string? Key { get; set; }

    public HealthCheckStatus Status { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int DurationMs { get; set; }

    public int? TimeoutMs { get; set; }

    /// <summary>The kind of dependency, from the report (Platform, Messaging, Identity, AI, Integrations, or the app's own).</summary>
    [MaxLength(50)]
    public string? Category { get; set; }

    public string[] Tags { get; set; } = [];

    /// <summary>What the app calls the check, such as "Database"; the app's own words, as its descriptions are.</summary>
    [MaxLength(100)]
    public string? Title { get; set; }

    /// <summary>The check's <c>BitIconName</c>, from the report.</summary>
    [MaxLength(50)]
    public string? IconName { get; set; }

    /// <summary>What a pass proves, from the report.</summary>
    [MaxLength(300)]
    public string? About { get; set; }

    /// <summary>What the check talks to, such as "Twilio", from the report; never guessed (See ProjectMapPartDto.Provider).</summary>
    [MaxLength(100)]
    public string? Provider { get; set; }

    /// <summary>The host the check reaches, from the report.</summary>
    [MaxLength(255)]
    public string? Host { get; set; }

    /// <summary>The model an AI check uses, from the report.</summary>
    [MaxLength(100)]
    public string? Model { get; set; }

    /// <summary>
    /// The check only misses its settings, which is a setup finding rather than an outage: it reports Degraded, but is
    /// left out of the project's status, its periods, its uptime and its rollups.
    /// </summary>
    public bool IsNotConfigured { get; set; }

    /// <summary>
    /// The app answered with a result it measured before the last round, such as a cached check's, whose time is its
    /// real run's, so <see cref="DurationMs"/> is a time already counted.
    /// </summary>
    public bool IsReused { get; set; }

    /// <summary>When the app last really ran the check, for one whose result it reuses.</summary>
    public DateTimeOffset? VerifiedAt { get; set; }
}

public enum ProjectProbeOutcome
{
    /// <summary>The report was read.</summary>
    Read,
    /// <summary>The server didn't answer, or not in time.</summary>
    Unreachable,
    /// <summary>The token was refused, or couldn't be refreshed: rounds before /health took the report's place.</summary>
    Unauthorized,
    /// <summary>The server answered with something that isn't a health report.</summary>
    BadResponse,
    /// <summary>
    /// A reading of the bitplatform Network got an answer from the app's front door. It reads no report, so it is never
    /// <see cref="Read"/>; one that got no answer is <see cref="Unreachable"/> (See ProjectProbe.Colo).
    /// </summary>
    Reached,
    /// <summary>
    /// The token couldn't read the report, and the server's public <c>/health</c> was read in its place: its status alone,
    /// with no checks and no build (bitfabric-internal/specs/monitoring.md, Monitoring keeps watching when the approval
    /// breaks). A round of bit Fabric's own server that got neither, but whose front door answered, is <see cref="Reached"/>.
    /// </summary>
    PublicHealth
}
