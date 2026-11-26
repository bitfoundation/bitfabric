using Bit.Fabric.Shared.Features.Attachments;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// One project on the projects page, as the approved "Health cards" design draws it (bitfabric-internal/specs/monitoring.md,
/// The projects page): whether it works and what that means for its users, the last 30 days as a bar a day with their uptime,
/// how fast it answers, and its scans (bitfabric-internal/specs/scanners.md): the main ones with their grade or score and
/// the most urgent finding as their tooltip, the others as "+N more", and a button to run them when none ran yet. The whole
/// card opens the project's page.
/// </summary>
public partial class ProjectCard
{
    /// <summary>The environment the card draws: its project's Production, or its first.</summary>
    [Parameter, EditorRequired] public MonitoringDto Project { get; set; } = default!;

    /// <summary>The project it is an environment of, whose name, license and other environments the card shows.</summary>
    [Parameter] public Bit.Fabric.Shared.Features.Projects.Dtos.ProjectDto? Parent { get; set; }

    private static string EnvironmentTone(MonitoringDto environment) => (environment.ConnectionStatus, environment.Health) switch
    {
        (ProjectConnectionStatus.ReconnectNeeded or ProjectConnectionStatus.Disconnected, _) => "tone-nodata",
        (_, ProjectHealth.Down) => "tone-down",
        (_, ProjectHealth.Degraded) => "tone-degraded",
        (_, ProjectHealth.Operational) => "tone-ok",
        _ => "tone-nodata"
    };

    [AutoInject] private IProjectScansController scansController = default!;

    /// <summary>A project connected before the scanners came has none until someone runs them.</summary>
    private async Task RunScans()
    {
        await scansController.RunNow(new() { MonitoringId = Project.Id }, CurrentCancellationToken);

        foreach (var scan in Project.Scans)
        {
            scan.IsRunning = true;
        }

        SnackBarService.Success(Localizer[nameof(AppStrings.MonitoringScansStarted)]);
    }

    /// <summary>A scan on the card opens the project's Scans tab.</summary>
    private string ScansHref => $"{PageUrls.ProjectEnvironment(Project.ProjectId, Project.Id)}?tab=scans";

    /// <summary>The day bars open the project's History, which draws its 90 days a part at a time.</summary>
    private string HistoryHref => $"{PageUrls.ProjectEnvironment(Project.ProjectId, Project.Id)}?tab=history";

    private static string ScanTone(ProjectScanSummaryDto scan)
        => scan.FinishedAt is null ? (scan.IsRunning ? "tone-maintenance" : "tone-nodata") : ProjectScanWords.Tone(scan.Outcome, scan.Verdict);

    private string ScanValue(ProjectScanSummaryDto scan) => scan.FinishedAt is null
        ? scan.IsRunning ? Localizer[nameof(AppStrings.MonitoringScanRunning)] : Localizer[nameof(AppStrings.MonitoringScanNotRun)]
        : ProjectScanWords.Value(Localizer, scan, DateTimeOffset.UtcNow) ?? "—";

    /// <summary>The most urgent finding, in the viewer's language, or what the scan is.</summary>
    private string ScanTitle(ProjectScanSummaryDto scan) => scan.TopFinding is { } finding
        ? ProjectScanWords.Title(Localizer, finding)
        : ProjectScanWords.KindName(Localizer, scan.Kind);

    private string Host => Uri.TryCreate(Project.Address, UriKind.Absolute, out var uri) ? uri.Authority : Project.Address ?? "";

    /// <summary>The app's own icon, from bit Fabric's server (See ProjectIcons); null when the app's manifest names none.</summary>
    private string? IconUrl => Project.IconId is { } id ? new Uri(AbsoluteServerAddress, $"api/v1/Attachment/GetAttachment/{id}/{AttachmentKind.ProjectIcon}").ToString() : null;

    /// <summary>The first letter of the name, the card's mark when the app has no icon, or its icon fails to load.</summary>
    private string Monogram => (Parent?.Name ?? Project.Name) is { Length: > 0 } name && string.IsNullOrWhiteSpace(name) is false ? name.Trim()[..1].ToUpperInvariant() : "?";

    private string Tone => (Project.ConnectionStatus, Project.Health) switch
    {
        (ProjectConnectionStatus.ReconnectNeeded, _) => "tone-partial",
        (ProjectConnectionStatus.Disconnected, _) => "tone-nodata",
        (_, ProjectHealth.Down) => "tone-down",
        (_, ProjectHealth.Degraded) => "tone-degraded",
        (_, ProjectHealth.Operational) => "tone-ok",
        _ => "tone-nodata"
    };

    private BitColor StateColor => (Project.ConnectionStatus, Project.Health) switch
    {
        (ProjectConnectionStatus.ReconnectNeeded, _) => BitColor.SevereWarning,
        (ProjectConnectionStatus.Disconnected, _) => BitColor.SecondaryBackground,
        (ProjectConnectionStatus.FirstChecks, ProjectHealth.NoData) => BitColor.Info,
        (_, ProjectHealth.Down) => BitColor.Error,
        (_, ProjectHealth.Degraded) => BitColor.Warning,
        (_, ProjectHealth.Operational) => BitColor.Success,
        _ => BitColor.SecondaryBackground
    };

    private string StateIcon => (Project.ConnectionStatus, Project.Health) switch
    {
        (ProjectConnectionStatus.ReconnectNeeded or ProjectConnectionStatus.Disconnected, _) => BitIconName.PlugDisconnected,
        (_, ProjectHealth.Down) => BitIconName.StatusErrorFull,
        (_, ProjectHealth.Degraded) => BitIconName.WarningSolid,
        (_, ProjectHealth.Operational) => BitIconName.CompletedSolid,
        _ => BitIconName.Clock
    };

    private string StateText => (Project.ConnectionStatus, Project.Health) switch
    {
        (ProjectConnectionStatus.ReconnectNeeded, _) => Localizer[nameof(AppStrings.MonitoringNeedsReconnecting)],
        (ProjectConnectionStatus.Disconnected, _) => Localizer[nameof(AppStrings.MonitoringDisconnected)],
        (ProjectConnectionStatus.FirstChecks, ProjectHealth.NoData) => Localizer[nameof(AppStrings.MonitoringFirstChecksRunning)],
        (_, ProjectHealth.Down) => Localizer[nameof(AppStrings.MonitoringDown)],
        (_, ProjectHealth.Degraded) => Localizer[nameof(AppStrings.MonitoringSomeFeaturesDoNotWork)],
        (_, ProjectHealth.Operational) => Localizer[nameof(AppStrings.MonitoringEverythingWorks)],
        _ => Localizer[nameof(AppStrings.MonitoringNoData)]
    };

    /// <summary>As many decimals as it takes to tell 99.9% from 99.99%.</summary>
    public static string FormatPercent(double value) => value switch
    {
        >= 100 => "100%",
        >= 99.99 => $"{Math.Floor(value * 1000) / 1000:0.000}%",
        >= 99 => $"{Math.Floor(value * 100) / 100:0.00}%",
        _ => $"{value:0.0}%"
    };
}
