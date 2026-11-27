using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Bit.Fabric.Workbench.Components.Pages.Site;

/// <summary>
/// Hosts the app's new project wizard, which needs nothing from the server. The wizard keeps its answers and its step in its
/// own address, so a reload or a copied link opens it as it was, and here that address is the view's too. The host keeps the
/// page's part of it in a store, so a reload, a reset and a screenshot of the view show the answers that were on the screen,
/// and puts the view's settings back into it each time the page writes it (See workbench.followPageAddress).
/// </summary>
public partial class NewProjectHost
{
    [AutoInject] private NewProjectAddressStore store = default!;

    private bool isReady;
    private DotNetObjectReference<NewProjectHost>? reference;
    private TaskCompletionSource<bool>? addressReady;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (IsPrerendering)
            return;

        // The view's own settings in the page's address: the scenario and the pane, as the shell gives them.
        var viewQuery = NavigationManager.ToAbsoluteUri(WorkbenchUrls.Preview(NewProjectKey, Scenario, PaneId)).Query;

        // Before the page renders, so its first write of the address keeps the view's settings in it.
        reference = DotNetObjectReference.Create(this);
        await JSRuntime.InvokeVoidAsync("workbench.followPageAddress", reference, viewQuery.TrimStart('?'));

        await WhenStateRestored();

        var wanted = AddressOfStore(QueryHelpers.ParseQuery(viewQuery));

        if (wanted != NavigationManager.Uri)
        {
            addressReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
            NavigationManager.LocationChanged += OnLocationChanged;
            NavigationManager.NavigateTo(wanted, replace: true);

            // Counted as a load, so a screenshot waits for the address too; a navigation that never comes back still ends.
            await ScenarioState.Track(Task.WhenAny(addressReady.Task, Task.Delay(3000, CurrentCancellationToken)));
            NavigationManager.LocationChanged -= OnLocationChanged;
        }

        isReady = true;
    }

    /// <summary>The page wrote its address: its answers and its step, without the view's settings, which the script kept.</summary>
    [JSInvokable]
    public void KeepPageAddress(string query)
    {
        if (isReady)
        {
            store.Keep(query.Length is 0 ? null : query);
        }
    }

    /// <summary>
    /// The address with the answers the store kept and none of another's, such as those of a view reset since: what isn't the
    /// view's setting is the page's. A setting of the view wins over an answer of the same name (the wizard's theme option and
    /// the view's theme), as the view must stay as the shell shows it.
    /// </summary>
    private string AddressOfStore(Dictionary<string, StringValues> viewQuery)
    {
        string[] workbenchKeys = [WorkbenchUrls.PaneQueryKey, WorkbenchUrls.SnapshotQueryKey, WorkbenchUrls.TourQueryKey];
        var current = QueryHelpers.ParseQuery(new Uri(NavigationManager.Uri).Query);
        var kept = QueryHelpers.ParseQuery(store.Query);
        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in current.Keys.Where(key => viewQuery.ContainsKey(key) is false && workbenchKeys.Contains(key, StringComparer.OrdinalIgnoreCase) is false && kept.ContainsKey(key) is false))
        {
            parameters[key] = null;
        }

        foreach (var (key, value) in kept.Where(pair => viewQuery.ContainsKey(pair.Key) is false))
        {
            parameters[key] = value.ToString();
        }

        return parameters.Count is 0 ? NavigationManager.Uri : NavigationManager.GetUriWithQueryParameters(parameters);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args) => addressReady?.TrySetResult(true);

    public override async ValueTask DisposeAsync()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;

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
