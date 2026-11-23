using Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring;

/// <summary>
/// Hosts the app's add page in the case the Case switch picks. The page reads its case from its address (the address to
/// look for, the approval's answer, the project just connected), so the host puts it there; a case that starts with a
/// press, waiting for the approval or the approval declined, gets that press too. Typing an address and going through
/// the approval works as well: the approval page is the sample world's, and answers as the case says, approved by default.
/// </summary>
public partial class ProjectConnectHost
{
    public const string CaseSwitch = WorkbenchApprovalNavigation.CaseSwitch;

    public const string ReconnectCase = "reconnect";

    public const string ConnectedCase = "connected";

    public const string AllInCase = "connected-all";

    public const string BlockedCase = "connected-blocked";

    /// <summary>The project the connected cases show: just connected, its first readings still coming in.</summary>
    private const string ConnectedProject = "parcelpoint";

    [AutoInject] private WorkbenchPushNotificationService pushNotificationService = default!;
    [AutoInject] private NotificationPreferenceService notificationPreferenceService = default!;

    private string? Case => Scenario.GetSwitch(CaseSwitch);

    protected override async Task OnBeforeAppPage()
    {
        switch (Case)
        {
            case AllInCase:
                World.Find(ConnectedProject)!.ReadingsIn = null;
                break;
            case BlockedCase:
                // This device turned notifications on before, and its browser blocks them now.
                pushNotificationService.IsBlocked = true;
                await notificationPreferenceService.SetEnabled(true);
                break;
        }
    }

    /// <summary>What the add page reads from its address for the case: the app's address, the approval's answer or the connected project.</summary>
    protected override IReadOnlyDictionary<string, object?> QueryOfSwitches()
    {
        string? address = null, state = null, code = null, error = null, connected = null;

        switch (Case)
        {
            case "found" or WorkbenchApprovalNavigation.ApprovingCase or WorkbenchApprovalNavigation.DeclinedCase:
                address = "taskbloom.app";
                break;
            case "static":
                address = "shopwave.store";
                break;
            case "unknown-version":
                address = "menuly.app";
                break;
            case "already":
                address = "yourapp.com";
                break;
            case "not-bit":
                address = "myblog.com";
                break;
            case "unreachable":
                address = "staging.taskbloom.app";
                break;
            case ReconnectCase:
                var project = World.Find(Scenario.GetSwitch(ProjectOpsHost.ProjectSwitch) ?? "crewboard") ?? World.Find("crewboard")!;
                address = new Uri(project.Address).Authority;
                break;
            case "callback":
                (state, code) = (MonitoringSampleWorld.SampleApprovalState, "workbench-sample-code");
                break;
            case "callback-declined":
                (state, error) = (MonitoringSampleWorld.SampleApprovalState, "access_denied");
                break;
            case ConnectedCase or AllInCase or BlockedCase:
                connected = (World.Projects.FirstOrDefault(project => project.Id.ToString() == NavigationQuery("connected")) ?? World.Find(ConnectedProject))!.Id.ToString();
                break;
        }

        return new Dictionary<string, object?>
        {
            ["address"] = address,
            ["state"] = state,
            ["code"] = code,
            ["error"] = error,
            ["connected"] = connected
        };
    }

    /// <summary>The page connected a project by itself: the view's case follows, so a reload shows the last step.</summary>
    protected override WorkbenchScenario SwitchesOfQuery(WorkbenchScenario scenario, IReadOnlyDictionary<string, string> query)
        => query.ContainsKey("connected") && Case is not (ConnectedCase or AllInCase or BlockedCase) ? scenario.WithSwitch(CaseSwitch, ConnectedCase) : scenario;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (IsReady is false || isApprovalPressed || Case is not (WorkbenchApprovalNavigation.ApprovingCase or WorkbenchApprovalNavigation.DeclinedCase))
            return;

        // The case begins once the app was found and "Continue to approval" was pressed.
        isApprovalPressed = true;
        await Press(".add-page .inline-action");
    }

    private bool isApprovalPressed;

    private string? NavigationQuery(string key) => WorkbenchUrls.ReadQuery(NavigationManager.Uri, key);
}
