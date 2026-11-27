using Bit.Fabric.Workbench.Components.Pages.ProjectTelemetry.Data;

namespace Bit.Fabric.Workbench.Components.Pages.ProjectTelemetry;

/// <summary>
/// The alerts the customer's AI set up for a project: each rule in words, whether it is quiet, firing or paused, where it
/// is sent and what was asked for. Each can be paused, and deleted after a confirmation; adding one is asking the AI.
/// </summary>
public partial class TelemetryAlerts
{
    [Parameter, EditorRequired] public IReadOnlyList<DashboardAlert> Alerts { get; set; } = [];

    [Parameter] public EventCallback<string> OnTogglePaused { get; set; }

    [Parameter] public EventCallback<string> OnDelete { get; set; }

    private static string StateIcon(DashboardAlertState state) => state switch
    {
        DashboardAlertState.Firing => BitIconName.Ringer,
        DashboardAlertState.Paused => BitIconName.Pause,
        _ => BitIconName.CheckMark
    };

    private static string StateText(DashboardAlertState state) => state switch
    {
        DashboardAlertState.Firing => nameof(WorkbenchStrings.TelemetryAlertFiring),
        DashboardAlertState.Paused => nameof(WorkbenchStrings.TelemetryAlertPaused),
        _ => nameof(WorkbenchStrings.TelemetryAlertQuiet)
    };
}
