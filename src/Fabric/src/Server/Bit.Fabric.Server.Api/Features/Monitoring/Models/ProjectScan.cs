using Bit.Fabric.Server.Api.Features.Identity.Models;
using Bit.Fabric.Server.Api.Features.Tenants;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Models;

/// <summary>
/// One run of one kind of scan on a project (bitfabric-internal/specs/scanners.md, Decisions, 2026-11-13). It is written
/// when the scan is asked for, so a scan waiting for its turn shows as one and a second request of the same kind joins it
/// rather than queueing again; the job stamps it as it starts and fills in the result as it ends.
/// </summary>
public partial class ProjectScan : ITenantAware
{
    public Guid Id { get; set; }

    public Guid MonitoringId { get; set; }

    public ProjectMonitoring? Monitoring { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public ProjectScanKind Kind { get; set; }

    public ProjectScanTrigger Trigger { get; set; }

    /// <summary>Who pressed "Run scans now", for a scan run by hand.</summary>
    public Guid? RequestedByUserId { get; set; }

    public User? RequestedBy { get; set; }

    /// <summary>The build a deployment brought, as the health report names it, for a scan after a deployment.</summary>
    [MaxLength(200)]
    public string? ApplicationVersion { get; set; }

    /// <summary>When it was asked for; a deployment's scans start a couple of minutes later.</summary>
    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>Null until it finishes.</summary>
    public ProjectScanOutcome? Outcome { get; set; }

    /// <summary>Set when it completed: from its score and its most urgent finding.</summary>
    public ProjectScanVerdict? Verdict { get; set; }

    /// <summary>
    /// Security's points, Mozilla Observatory's way (the grade comes from them), and the 0 to 100 of SEO, Performance and
    /// Accessibility, the last two the lowest of their pages and devices.
    /// </summary>
    public int? Score { get; set; }

    /// <summary>Security's grade, A+ to F.</summary>
    [MaxLength(2)]
    public string? Grade { get; set; }

    /// <summary>Domain and certificate: when the first of the certificates and domain registrations it read ends.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Performance and Accessibility: each page and device Lighthouse measured, with its score and vitals (JSON).</summary>
    public List<ProjectScanPage> Pages { get; set; } = [];

    public List<ProjectScanFinding> Findings { get; set; } = [];
}

/// <summary>
/// One page a browser scan loaded on one device (bitfabric-internal/specs/scanners.md, Decisions, PR 11): its score of the
/// scan's kind and, for Performance, the Core Web Vitals as Lighthouse measured them in the lab, kept as numbers so the
/// tab and /mcp show them.
/// </summary>
public partial class ProjectScanPage
{
    /// <summary>The path the page answered at, after its redirects.</summary>
    [MaxLength(500)]
    public string? Path { get; set; }

    public ProjectScanDevice Device { get; set; }

    public int? Score { get; set; }

    public int? LargestContentfulPaintMs { get; set; }

    public double? CumulativeLayoutShift { get; set; }

    public int? TotalBlockingTimeMs { get; set; }

    public int? FirstContentfulPaintMs { get; set; }

    public int? SpeedIndexMs { get; set; }

    public int? TimeToFirstByteMs { get; set; }
}

/// <summary>
/// What a scan found: a stable key with its parameters, its severity and where it was seen, never text, so each viewer reads
/// it in their own language (See ScanFindings).
/// </summary>
public partial class ProjectScanFinding : ITenantAware
{
    public Guid Id { get; set; }

    public Guid ScanId { get; set; }

    public ProjectScan? Scan { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    /// <summary>One of ScanFindings' keys.</summary>
    [Required, MaxLength(64)]
    public string? Key { get; set; }

    public ScanFindingSeverity Severity { get; set; }

    /// <summary>The values its texts are formatted with, in order.</summary>
    public string[] Parameters { get; set; } = [];

    /// <summary>Page paths, host names or addresses.</summary>
    public string[] Where { get; set; } = [];
}

/// <summary>
/// When bit Fabric scans a project: a row of its own rather than columns of <see cref="ProjectMonitoring"/>, whose version every round
/// moves on. A project without one scans with the defaults: weekly, and not after deployments until the owner allows it.
/// </summary>
public partial class ProjectScanSettings : ITenantAware
{
    public Guid MonitoringId { get; set; }

    public ProjectMonitoring? Monitoring { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public bool ScanAfterDeployments { get; set; }

    public bool WeeklyScans { get; set; } = true;

    /// <summary>In the weekly scans email to the team's members (See ProjectScansEmailJobRunner); on by default.</summary>
    public bool WeeklyEmail { get; set; } = true;

    public long Version { get; set; }
}

/// <summary>
/// A team member said a finding is wrong (bitfabric-internal/specs/scanners.md, A wrong finding can be reported): who,
/// when and which finding, with its key copied, so the count per key, a guard metric, outlives the scans past their
/// retention.
/// </summary>
public partial class ProjectScanFindingReport : ITenantAware
{
    public Guid Id { get; set; }

    public Guid MonitoringId { get; set; }

    public ProjectMonitoring? Monitoring { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    /// <summary>The finding reported. Not a foreign key, so the report stays once its scan is deleted.</summary>
    public Guid FindingId { get; set; }

    public ProjectScanKind Kind { get; set; }

    [Required, MaxLength(64)]
    public string? FindingKey { get; set; }

    public string[] Where { get; set; } = [];

    /// <summary>Null once that member's account is gone.</summary>
    public Guid? UserId { get; set; }

    public User? User { get; set; }

    public DateTimeOffset ReportedAt { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }
}

/// <summary>
/// The weekly scans email a member of a team got for a week (bitfabric-internal/specs/scanners.md, Decisions): written
/// before it is sent, its key the team, the member and the week's Monday, so however often the job runs, on however many
/// servers, a member gets one a week a team. Sign-in codes go out through the same email service, which must never look
/// like a flood.
/// </summary>
public partial class ProjectScansEmail : ITenantAware
{
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    /// <summary>The Monday, in UTC, of the week it was sent in.</summary>
    public DateOnly WeekOf { get; set; }

    public DateTimeOffset SentAt { get; set; }
}
