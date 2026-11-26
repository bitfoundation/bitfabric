using Bit.Fabric.Server.Api.Features.Projects.Models;
using Bit.Fabric.Server.Api.Features.Tenants;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Models;

/// <summary>
/// One environment of a project that bit Fabric watches, such as its Production or its Test deployment, by reading its
/// app's <c>/healthz</c> report with a token the app's own admin approved (bitfabric-internal/specs/monitoring.md). It
/// belongs to its project's team, and every table of its history is keyed by it.
/// </summary>
public partial class ProjectMonitoring : ITenantAware
{
    /// <summary>The environment a new monitoring gets unless the team names another: the one alerts and an SLA are for.</summary>
    public const string Production = "Production";

    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public Guid ProjectId { get; set; }

    public Project? Project { get; set; }

    /// <summary>Production, Test, Staging or a name the team gives; one of each in a project.</summary>
    [Required, MaxLength(50)]
    public string? Environment { get; set; }

    /// <summary>
    /// The id in the project's bit.json that the app's last report named (See HealthReportDto.ProjectId), which links it to
    /// its project (See ProjectLinker); null while the app names none, as one on a template from before it did.
    /// </summary>
    public Guid? ReportedProjectId { get; set; }

    /// <summary>The app's address, <c>https://host</c>, where its visitors go and its home page is timed.</summary>
    [Required, MaxLength(300)]
    public string? Address { get; set; }

    /// <summary>Where <c>/healthz</c> is read, <c>https://host</c>: the address itself or its standalone api's.</summary>
    [Required, MaxLength(300)]
    public string? ServerAddress { get; set; }

    /// <summary>
    /// What alerts, status pages, emails, exports and the AI tools call it: its project's name, with the environment after it
    /// unless that is Production (See NameOf). Written whenever either is renamed.
    /// </summary>
    [Required, MaxLength(160)]
    public string? Name { get; set; }

    /// <summary>The name a monitoring of <paramref name="environment"/> in a project named <paramref name="project"/> has.</summary>
    public static string NameOf(string? project, string? environment)
        => string.IsNullOrWhiteSpace(environment) || string.Equals(environment.Trim(), Production, StringComparison.OrdinalIgnoreCase)
            ? project?.Trim() ?? ""
            : $"{project?.Trim()} ({environment.Trim()})";

    /// <summary>The bit template version the app says it's made with; null when it doesn't say, shown as "unknown".</summary>
    [MaxLength(32)]
    public string? TemplateVersion { get; set; }

    /// <summary>The attachment holding the icon the app's web app manifest names; null for none, and the card shows a monogram (See ProjectIcons).</summary>
    public Guid? IconId { get; set; }

    /// <summary>A hash of the icon as the app serves it, so reading the same one again stores nothing.</summary>
    [MaxLength(64)]
    public string? IconSourceHash { get; set; }

    /// <summary>When the manifest was last read for the icon: on the first round, after each deployment and once a day.</summary>
    public DateTimeOffset? IconReadAt { get; set; }

    /// <summary>
    /// The app's <c>/.well-known/bit-app.json</c> says its front end is hosted statically, so timing its home page says
    /// nothing about the app: the server's <c>/health</c> is timed instead.
    /// </summary>
    public bool HasStaticFrontEnd { get; set; }

    /// <summary>
    /// The bitplatform Network's locations that time the app's home page every minute, besides bit Fabric's own server in
    /// Germany: codes of NetworkLocations, the defaults for a new project.
    /// </summary>
    public string[] Locations { get; set; } = [.. NetworkLocations.Defaults];

    public ProjectHealth Health { get; set; }

    public ProjectConnectionStatus ConnectionStatus { get; set; }

    /// <summary>Why the connection was lost: a code of ProjectConnectionProblems, worded in each reader's language when read.</summary>
    [MaxLength(500)]
    public string? ConnectionProblem { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>
    /// The IANA time zone an SLA month follows: first that of whoever added the project, changed in Settings. Pages draw
    /// every other time in the viewer's own zone.
    /// </summary>
    [Required, MaxLength(64)]
    public string? TimeZoneId { get; set; } = "UTC";

    public DateTimeOffset? LastCheckedAt { get; set; }

    public ProjectConnection? Connection { get; set; }

    public long Version { get; set; }
}
