using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// One project of the sample world as bit Fabric's server holds it: what its health report said, its incidents, history,
/// scans, SLA, status page and alerts. The stand-in controllers turn it into the app's DTOs, the way the server does.
/// </summary>
public sealed class SampleProject
{
    public required string Key { get; init; }

    public required Guid Id { get; init; }

    public required string Name { get; set; }

    /// <summary><c>https://host</c>.</summary>
    public required string Address { get; init; }

    public string? ServerAddress { get; init; }

    public bool HasStaticFrontEnd { get; set; }

    public string? TemplateVersion { get; init; }

    public Guid? IconId { get; init; }

    public ProjectConnectionStatus ConnectionStatus { get; set; } = ProjectConnectionStatus.Connected;

    public string? ConnectionProblem { get; set; }

    /// <summary>When bit Fabric started watching it.</summary>
    public required DateTimeOffset CreatedOn { get; init; }

    /// <summary>When its report was last read; null before the first round, or once the approval stopped working.</summary>
    public DateTimeOffset? CheckedAt { get; set; }

    /// <summary>How long its home page usually takes, in milliseconds.</summary>
    public int BaseMs { get; init; } = 180;

    public string[] Locations { get; set; } = [];

    public bool HasCacheWarning { get; set; }

    public string? ApplicationVersion { get; set; }

    /// <summary>How long the home page took in the round that read the newest report, from bit Fabric's own server.</summary>
    public int? HomePageMs { get; set; }

    public required string TimeZoneId { get; set; }

    public string? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedOn { get; set; }

    /// <summary>The last report's entries.</summary>
    public List<ProjectCheckDto> Checks { get; } = [];

    public List<SampleIncident> Incidents { get; } = [];

    /// <summary>Each part's stretches in a state other than healthy.</summary>
    public List<ProjectPeriodDto> Periods { get; } = [];

    public List<ProjectStartDto> Starts { get; } = [];

    public List<ProjectMaintenanceWindowDto> Maintenance { get; } = [];

    /// <summary>A part the report gained or lost: when it first and last had it; a part missing here was always there.</summary>
    public Dictionary<string, (DateTimeOffset? From, DateTimeOffset? To)> PartSpans { get; } = [];

    /// <summary>The newest reading of each location of the bitplatform Network, revealed one a look while the first checks run.</summary>
    public List<ProjectNetworkReadingDto> Readings { get; } = [];

    /// <summary>How many of <see cref="Readings"/> have come in, for a project whose first checks are running; null for all.</summary>
    public int? ReadingsIn { get; set; }

    public List<ProjectAlertRouteDto> Routes { get; } = [];

    public ProjectSlaSettingsDto Sla { get; set; } = new();

    public required StatusPageSettingsDto StatusPage { get; set; }

    public ProjectScanSettingsDto ScanSettings { get; set; } = new();

    /// <summary>Every finished scan, newest first, with its findings.</summary>
    public List<ProjectScanDto> ScanRuns { get; } = [];

    /// <summary>The scans waiting for their turn or running.</summary>
    public List<ProjectScanDto> PendingScans { get; } = [];

    public DateTimeOffset? CanRunScansAgainAt { get; set; }

    /// <summary>What its scans give when they run again.</summary>
    public ScanProfile ScanProfile { get; set; } = new("B", 82, 86, 90, 91, 73);

    /// <summary>How often the Scans tab looked while scans waited or ran: each look after the first moves them on.</summary>
    public int ScanLooks { get; set; }

    /// <summary>The app's own name for a part the latest report has, or the catalog's.</summary>
    public string TitleOf(string key) => Checks.FirstOrDefault(check => check.Key == key)?.Title ?? MonitoringParts.TitleOf(key);

    public long Version { get; set; } = 1;
}

/// <summary>An incident or an announcement, with its texts in each language they were written in.</summary>
public sealed class SampleIncident
{
    public required Guid Id { get; init; }

    public ProjectIncidentKind Kind { get; init; }

    public ProjectHealth Impact { get; set; }

    /// <summary>By culture name; bit Fabric writes its own in every language, the team in the ones it chose.</summary>
    public required Dictionary<string, string> Title { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? EndedAt { get; set; }

    public string[] CheckKeys { get; init; } = [];

    public bool IsAutomatic { get; init; }

    public DateTimeOffset? AfterDeployAt { get; init; }

    public bool DuringMaintenance { get; init; }

    public string? AcknowledgedBy { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    /// <summary>Newest first.</summary>
    public List<SampleUpdate> Updates { get; } = [];

    /// <summary>Newest first.</summary>
    public List<ProjectAlertDto> Alerts { get; } = [];

    public string? AgentPrompt { get; set; }

    /// <summary>The status page's components it names; none names them from its parts.</summary>
    public Guid[] ComponentIds { get; set; } = [];

    public long Version { get; set; } = 1;
}

public sealed class SampleUpdate
{
    public required DateTimeOffset At { get; init; }

    public ProjectIncidentPhase Phase { get; init; }

    /// <summary>By culture name.</summary>
    public required Dictionary<string, string> Text { get; init; }

    /// <summary>Null for bit Fabric's own.</summary>
    public string? Author { get; init; }
}

/// <summary>A member of the sample team, who alerts can reach.</summary>
public sealed record SampleMember(Guid UserId, string Name, string? Email, string? PhoneNumber);
