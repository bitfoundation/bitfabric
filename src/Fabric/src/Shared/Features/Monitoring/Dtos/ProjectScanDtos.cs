namespace Bit.Fabric.Shared.Features.Monitoring.Dtos;

/// <summary>
/// The scans bit Fabric runs on a project (bitfabric-internal/specs/scanners.md). The numbers are stored, so a kind keeps
/// its number when others are added.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectScanKind>))]
public enum ProjectScanKind
{
    /// <summary>Headers, cookies, HTTPS and the bit app's own endpoints that must ask for sign-in, graded A+ to F.</summary>
    Security = 0,

    /// <summary>The home page and up to 10 pages of the sitemap, as a search engine reads their server-rendered HTML, 0 to 100.</summary>
    Seo = 1,

    /// <summary>The TLS certificates of the app's hosts and the registration of their domains, by the days left.</summary>
    DomainAndCertificate = 2,

    /// <summary>Lighthouse in a container of bit Fabric's own: the home page and a couple of sitemap pages on a phone and a computer, 0 to 100, with the Core Web Vitals.</summary>
    Performance = 3,

    /// <summary>Lighthouse's accessibility audits, which are axe-core's, on the same pages in the same runs, 0 to 100.</summary>
    Accessibility = 4
}

/// <summary>Why a scan ran (after every deployment, on a schedule and by hand).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectScanTrigger>))]
public enum ProjectScanTrigger
{
    /// <summary>The health check showed a new build, and the owner allows scans after deployments.</summary>
    Deployment = 0,

    Weekly = 1,

    /// <summary>"Run scans now", at most once every 10 minutes a project.</summary>
    Manual = 2,

    /// <summary>The first scans, right after the project was connected.</summary>
    Connected = 3
}

/// <summary>How a scan that finished ended.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectScanOutcome>))]
public enum ProjectScanOutcome
{
    /// <summary>It read the app and gave its verdict.</summary>
    Completed = 0,

    /// <summary>bit Fabric couldn't reach the app, so it says nothing about it.</summary>
    Unreachable = 1,

    /// <summary>bit Fabric's own error: the scan runs again with the next one.</summary>
    Failed = 2
}

[JsonConverter(typeof(JsonStringEnumConverter<ProjectScanVerdict>))]
public enum ProjectScanVerdict
{
    Good = 0,
    NeedsWork = 1,
    Poor = 2
}

/// <summary>What a browser scan loaded a page as: Lighthouse's mobile (a mid-range phone on a slow 4G network) or desktop.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectScanDevice>))]
public enum ProjectScanDevice
{
    Phone = 0,
    Computer = 1
}

/// <summary>
/// One page a browser scan loaded, on one device, with its score and, for Performance, the Core Web Vitals Lighthouse
/// measured (bitfabric-internal/specs/scanners.md, Decisions). Lab numbers: TBT stands in for INP, which needs a real user's interaction.
/// </summary>
public partial class ProjectScanPageDto
{
    /// <summary>The page's path, as it answered after its redirects, such as /en-US/.</summary>
    public string? Path { get; set; }

    public ProjectScanDevice Device { get; set; }

    /// <summary>0 to 100: this kind's Lighthouse score of the page.</summary>
    public int? Score { get; set; }

    public int? LargestContentfulPaintMs { get; set; }

    public double? CumulativeLayoutShift { get; set; }

    public int? TotalBlockingTimeMs { get; set; }

    public int? FirstContentfulPaintMs { get; set; }

    public int? SpeedIndexMs { get; set; }

    public int? TimeToFirstByteMs { get; set; }
}

/// <summary>How soon a finding wants fixing; the report ranks urgent first.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScanFindingSeverity>))]
public enum ScanFindingSeverity
{
    Urgent = 0,
    Important = 1,
    Improvement = 2
}

/// <summary>
/// What a scan found, as a stable key and its parameters rather than text, so each viewer reads it in their own language
/// (See ScanFindings for the texts, and ScanPrompts for the prompt for the customer's AI agent).
/// </summary>
public partial class ProjectScanFindingDto
{
    public Guid Id { get; set; }

    /// <summary>One of ScanFindings' keys, such as <c>CspMissing</c>.</summary>
    public string? Key { get; set; }

    public ScanFindingSeverity Severity { get; set; }

    /// <summary>The values its texts are formatted with, in order, such as a host name and a number of days.</summary>
    public string[] Parameters { get; set; } = [];

    /// <summary>Where bit Fabric saw it: page paths, host names or addresses.</summary>
    public string[] Where { get; set; } = [];

    /// <summary>Someone of the team marked it "this is wrong".</summary>
    public bool IsReportedWrong { get; set; }
}

/// <summary>One run of one kind of scan on a project.</summary>
public partial class ProjectScanDto
{
    public Guid Id { get; set; }

    public Guid MonitoringId { get; set; }

    public ProjectScanKind Kind { get; set; }

    public ProjectScanTrigger Trigger { get; set; }

    /// <summary>The build the deployment brought, for a scan after a deployment.</summary>
    public string? ApplicationVersion { get; set; }

    /// <summary>When it was asked for; a deployment's scans are asked for a couple of minutes after it.</summary>
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>Null while it waits for its turn.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Null while it waits or runs.</summary>
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>Null until it finishes.</summary>
    public ProjectScanOutcome? Outcome { get; set; }

    /// <summary>Set when it completed.</summary>
    public ProjectScanVerdict? Verdict { get; set; }

    /// <summary>Security's points, and the 0 to 100 of SEO, Performance and Accessibility (the lowest of the pages and devices measured).</summary>
    public int? Score { get; set; }

    /// <summary>Security's grade, A+ to F, from <see cref="Score"/>.</summary>
    public string? Grade { get; set; }

    /// <summary>Domain and certificate: when the first of the app's certificates and domain registrations ends.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Performance and Accessibility: each page and device measured, the home page on a phone first.</summary>
    public ProjectScanPageDto[] Pages { get; set; } = [];

    public int UrgentCount { get; set; }

    public int ImportantCount { get; set; }

    public int ImprovementCount { get; set; }

    /// <summary>Most urgent first; filled for the latest runs and for a run asked for by itself.</summary>
    public ProjectScanFindingDto[] Findings { get; set; } = [];
}

/// <summary>When bit Fabric scans a project (bitfabric-internal/specs/scanners.md, Decisions, 2026-11-13).</summary>
public partial class ProjectScanSettingsDto
{
    public Guid MonitoringId { get; set; }

    /// <summary>Off until the owner allows it: bit Fabric sees each deployment in the health check.</summary>
    public bool ScanAfterDeployments { get; set; }

    public bool WeeklyScans { get; set; } = true;

    /// <summary>
    /// The project is in the weekly scans email its team's members get (bitfabric-internal/specs/scanners.md, Decisions):
    /// on until someone turns it off.
    /// </summary>
    public bool WeeklyEmail { get; set; } = true;

    public long Version { get; set; }
}

/// <summary>A project's Scans tab: the latest run of each kind with what it found, and when bit Fabric scans.</summary>
public partial class ProjectScansDto
{
    public Guid MonitoringId { get; set; }

    /// <summary>The project's address, which the prompts for the customer's AI agent name.</summary>
    public string? Address { get; set; }

    /// <summary>The newest finished run of each kind, with its findings, in the order the tab shows them.</summary>
    public ProjectScanDto[] Latest { get; set; } = [];

    /// <summary>The runs waiting for their turn or running, a kind at most once.</summary>
    public ProjectScanDto[] Pending { get; set; } = [];

    public ProjectScanSettingsDto Settings { get; set; } = new();

    /// <summary>When "Run scans now" may be pressed again; null when it may now.</summary>
    public DateTimeOffset? CanRunAgainAt { get; set; }
}

/// <summary>A project's newest scan of one kind, as its card on the projects page shows it.</summary>
public partial class ProjectScanSummaryDto
{
    /// <summary>The newest finished run of this kind; null when none has.</summary>
    public Guid? ScanId { get; set; }

    public ProjectScanKind Kind { get; set; }

    /// <summary>It waits for its turn or runs.</summary>
    public bool IsRunning { get; set; }

    /// <summary>When the newest finished run of this kind finished; null when none has.</summary>
    public DateTimeOffset? FinishedAt { get; set; }

    public ProjectScanOutcome? Outcome { get; set; }

    public ProjectScanVerdict? Verdict { get; set; }

    public int? Score { get; set; }

    public string? Grade { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public int FindingCount { get; set; }

    /// <summary>The most urgent finding, which the card shows as its tooltip.</summary>
    public ProjectScanFindingDto? TopFinding { get; set; }
}

public partial class ProjectScanHistoryRequestDto
{
    public Guid MonitoringId { get; set; }

    /// <summary>One kind's runs, or every kind's when null.</summary>
    public ProjectScanKind? Kind { get; set; }
}

public partial class RunProjectScansRequestDto
{
    public Guid MonitoringId { get; set; }
}

/// <summary>A team member says a finding is wrong (bitfabric-internal/specs/scanners.md, A wrong finding can be reported).</summary>
[DtoResourceType(typeof(AppStrings))]
public partial class ReportScanFindingRequestDto
{
    public Guid FindingId { get; set; }

    /// <summary>What they think is wrong about it, if they say.</summary>
    [MaxLength(1000, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    public string? Note { get; set; }
}
