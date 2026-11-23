namespace Bit.Fabric.Workbench.Components.Pages.Site;

/// <summary>
/// Hosts the app's page that sets up a project that already exists, which needs nothing from the server. Like the wizard, it
/// keeps its answers in its own address, so the view's settings go back into that address each time the page writes it (See
/// workbench.followPageAddress), and a reload of the view still opens it in them. Unlike the wizard's host, it keeps no copy
/// of the answers: a reset or a reload starts it empty, which is the state worth reviewing first.
/// </summary>
public partial class SetupProjectHost
{
    private bool isReady;
    private DotNetObjectReference<SetupProjectHost>? reference;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (IsPrerendering)
            return;

        var viewQuery = NavigationManager.ToAbsoluteUri(WorkbenchUrls.Preview(SetupProjectKey, Scenario, PaneId)).Query;

        // Before the page renders, so its first write of the address keeps the view's settings in it.
        reference = DotNetObjectReference.Create(this);

        try
        {
            await JSRuntime.InvokeVoidAsync("workbench.followPageAddress", reference, viewQuery.TrimStart('?'));
        }
        catch (JSDisconnectedException)
        {
            return;
        }

        isReady = true;
    }

    /// <summary>The page wrote its address; the view's settings are back in it already, and nothing else is kept.</summary>
    [JSInvokable]
    public void KeepPageAddress(string query)
    {
    }

    public override async ValueTask DisposeAsync()
    {
        if (reference is not null)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("workbench.stopFollowingPageAddress");
            }
            catch (JSDisconnectedException)
            {
            }

            reference.Dispose();
        }

        await base.DisposeAsync();
    }
}
