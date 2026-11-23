namespace Bit.Fabric.Workbench.Components.Pages.Monitoring;

/// <summary>
/// Hosts the app's public status page, which the app serves as a plain server-rendered page on the customer's own host name:
/// the project the Project switch picks, in the view's language, and following it by push in the case the Following switch
/// picks. The page's one script is stood in for by the Workbench's (See Scripts/statusFollow.ts), which a press of the
/// page's own buttons brings into that case. Empty is an address with no status page.
/// </summary>
public partial class StatusPageHost
{
    public const string ProjectSwitch = "project";

    public const string FollowSwitch = "follow";

    public const string DefaultProject = "yourapp";

    private string Slug => (World.Find(Scenario.GetSwitch(ProjectSwitch) ?? DefaultProject) ?? World.Find(DefaultProject))!.StatusPage.Slug!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (IsReady is false || isFollowPressed)
            return;

        isFollowPressed = true;

        switch (Scenario.GetSwitch(FollowSwitch))
        {
            case "following" or "denied" or "ios":
                await Press("[data-follow]");
                break;
            case "unfollowed":
                await Press("[data-follow]");
                await Press("[data-unfollow]:not([hidden])");
                break;
        }
    }

    private bool isFollowPressed;
}
