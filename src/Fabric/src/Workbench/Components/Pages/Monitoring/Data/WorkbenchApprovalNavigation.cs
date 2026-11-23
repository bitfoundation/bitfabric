namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// The stand-in for the app's <see cref="IExternalNavigationService"/>. The approval in the customer's own app, which the
/// add page opens beside itself, is the sample world's: it answers the way the add page's Case switch says, approved
/// unless the case waits for it or declines it, and its answer comes back the way external sign-in's does. Any other
/// address opens in a new tab.
/// </summary>
public sealed class WorkbenchApprovalNavigation(MonitoringSampleWorld world, WorkbenchScenarioState scenarioState, PubSubService pubSubService, Bit.Butil.Window window) : IExternalNavigationService
{
    /// <summary>The add page's switch, whose cases say how the approval answers.</summary>
    public const string CaseSwitch = "case";

    public const string ApprovingCase = "approving";

    public const string DeclinedCase = "declined";

    public async Task NavigateTo(string url)
    {
        if (world.ApprovalStateOf(url) is not { } state)
        {
            await window.Open(url, "_blank", new Bit.Butil.WindowFeatures());
            return;
        }

        var answer = scenarioState.Scenario.GetSwitch(CaseSwitch) switch
        {
            // Someone is still approving it in the app.
            ApprovingCase => null,
            DeclinedCase => $"{PageUrls.AddProjectCallback}?state={state}&error=access_denied",
            _ => $"{PageUrls.AddProjectCallback}?state={state}&code=workbench-sample-code"
        };

        if (answer is null)
            return;

        // Counted as a load, so a screenshot waits for the answer.
        _ = scenarioState.Track(Answer(answer));
    }

    private async Task<bool> Answer(string answer)
    {
        // As long as signing in and approving takes in a window beside the page.
        await Task.Delay(1600);
        pubSubService.Publish(ClientAppMessages.EXTERNAL_SIGN_IN_CALLBACK, answer);

        return true;
    }
}
