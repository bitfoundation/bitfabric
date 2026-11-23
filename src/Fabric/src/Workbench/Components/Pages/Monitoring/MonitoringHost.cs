using System.Text.RegularExpressions;
using Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.WebUtilities;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring;

/// <summary>
/// Base of the hosts of the app's own monitoring pages (bitfabric-internal/specs/monitoring.md, Decisions, 2026-11-13: the
/// Workbench shows the app's monitoring pages on sample data). A host renders the app's real component from Client.Core, fed
/// by the stand-ins of bit Fabric's controllers in Data, and maps the page's switches to what the app's page reads from its
/// address, such as the open tab, so a switch, a link and a screenshot all show the same state. Clicking one of the app's links to
/// another monitoring page opens that page's view here, with the switches that show what the link named.
/// </summary>
public abstract partial class MonitoringHost : WorkbenchComponentBase
{
    [AutoInject] protected MonitoringSampleWorld World = default!;
    [AutoInject] private WorkbenchAppLinks appLinks = default!;

    private IDisposable? linkHandler;
    private TaskCompletionSource<bool>? addressReady;

    /// <summary>
    /// The app's page renders once the view is interactive, as the app's own pages load only then here, and its address
    /// carries the query the switches ask for; until then the view shows a placeholder.
    /// </summary>
    protected bool IsReady { get; private set; }

    /// <summary>The query the app's page reads ([SupplyParameterFromQuery]), as the switches say; a null value leaves the key out.</summary>
    protected virtual IReadOnlyDictionary<string, object?> QueryOfSwitches() => new Dictionary<string, object?>();

    /// <summary>The switches that bring back a query the app's page changed itself, such as the tab it opened.</summary>
    protected virtual WorkbenchScenario SwitchesOfQuery(WorkbenchScenario scenario, IReadOnlyDictionary<string, string> query) => scenario;

    /// <summary>Runs once the view is interactive and before the app's page renders, such as setting up the device a case needs.</summary>
    protected virtual Task OnBeforeAppPage() => Task.CompletedTask;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (IsPrerendering)
            return;

        linkHandler = appLinks.Add(TranslateAppLink);
        NavigationManager.LocationChanged += OnLocationChanged;

        await OnBeforeAppPage();

        var wanted = NavigationManager.GetUriWithQueryParameters(QueryOfSwitches());

        if (new Uri(wanted).Query == new Uri(NavigationManager.Uri).Query)
        {
            IsReady = true;
            return;
        }

        addressReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        NavigationManager.NavigateTo(wanted, replace: true);

        // Counted as a load, so a screenshot waits for the address too; a navigation that never comes back still ends.
        await ScenarioState.Track(Task.WhenAny(addressReady.Task, Task.Delay(3000, CurrentCancellationToken)));
        IsReady = true;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (IsReady is false)
        {
            addressReady?.TrySetResult(true);
            return;
        }

        // The app's page changed its own query, such as the tab it opened: the view's switches follow, so a reload and a screenshot keep it.
        var query = QueryHelpers.ParseQuery(new Uri(args.Location).Query).ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
        var scenario = SwitchesOfQuery(Scenario, query);

        if (scenario != Scenario)
        {
            _ = InvokeAsync(() => ReportScenario(scenario));
        }
    }

    /// <summary>What a link of the app means here: a monitoring page in the view's settings, the sign-in note, or another language.</summary>
    private Func<Task>? TranslateAppLink(Uri target)
    {
        var path = CultureSegmentRegex().Replace(target.AbsolutePath, "/").TrimEnd('/');
        var query = QueryHelpers.ParseQuery(target.Query).ToDictionary(pair => pair.Key, pair => pair.Value.ToString());

        if (path is PageUrls.SignIn or PageUrls.SignUp)
            return ShowSignInNote;

        if (StatusLinkRegex().Match(target.AbsolutePath) is { Success: true } statusLink)
            return () => ReportScenario(Scenario with { Culture = statusLink.Groups["culture"].Value }, isApplied: false);

        if (path == PageUrls.Projects)
            return Go("projects", [], []);

        if (path is PageUrls.AddProject or PageUrls.AddProjectCallback)
        {
            if (query.TryGetValue("connected", out var connected))
                return Go("project-connect", [(ProjectConnectHost.CaseSwitch, ProjectConnectHost.ConnectedCase)], [("connected", connected)]);

            if (query.TryGetValue("address", out var address) && MonitoringSamples.HostOf(address) is { } host
                && World.Projects.FirstOrDefault(project => new Uri(project.Address).Authority == host) is { } watched)
                return Go("project-connect", [(ProjectConnectHost.CaseSwitch, ProjectConnectHost.ReconnectCase), (ProjectOpsHost.ProjectSwitch, watched.Key)], [("address", host)]);

            return Go("project-connect", [], []);
        }

        if (path.StartsWith($"{PageUrls.Projects}/", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(path[(PageUrls.Projects.Length + 1)..], out var id)
            && World.Projects.FirstOrDefault(project => project.Id == id) is { } opened)
        {
            var tab = query.GetValueOrDefault("tab");
            return Go("project-ops", [(ProjectOpsHost.ProjectSwitch, opened.Key), .. tab is null ? [] : new[] { (ProjectOpsHost.TabSwitch, tab) }], tab is null ? [] : [("tab", tab)]);
        }

        return null;
    }

    /// <summary>Opens a monitoring page in this view, with the switches and the query of the app's page that show what the link named.</summary>
    private Func<Task> Go(string pageKey, (string Key, string Value)[] switches, (string Key, string? Value)[] query) => async () =>
    {
        var scenario = switches.Aggregate(Scenario with { Variant = null, Switches = null }, (current, item) => current.WithSwitch(item.Key, item.Value));
        var url = QueryHelpers.AddQueryString(WorkbenchUrls.Preview(pageKey, scenario, PaneId), query.Where(item => item.Value is not null).Select(item => new KeyValuePair<string, string?>(item.Key, item.Value)));
        var isSamePage = string.Equals(new Uri(NavigationManager.Uri).Segments[^1].Trim('/'), pageKey, StringComparison.OrdinalIgnoreCase);

        // The page that opens reads its switches from the scenario, which a navigation in the same circuit doesn't parse again.
        ScenarioState.Set(scenario);
        NavigationManager.NavigateTo(url, replace: true);

        // Another page tells the shell itself (See PreviewPage); on the same page, the shell hears of the new switches from here.
        if (isSamePage)
        {
            await ReportScenario(scenario);
        }
    };

    /// <summary>Presses a control of the app's page once it shows, as a case that starts from a press needs; a screenshot waits for it.</summary>
    protected Task Press(string selector, int timeoutMs = 8000)
        => ScenarioState.Track(JSRuntime.InvokeAsync<bool>("workbench.pressWhenShown", CurrentCancellationToken, selector, timeoutMs).AsTask());

    public override async ValueTask DisposeAsync()
    {
        linkHandler?.Dispose();
        NavigationManager.LocationChanged -= OnLocationChanged;

        await base.DisposeAsync();
    }

    /// <summary>The culture segment the app's routes may start with, such as /fa-IR/projects.</summary>
    [GeneratedRegex("^/[a-z]{2}-[A-Z]{2}(?=/)")]
    private static partial Regex CultureSegmentRegex();

    /// <summary>The public status page in another language, as its language links name it.</summary>
    [GeneratedRegex("^/(?<culture>[a-z]{2}-[A-Z]{2})/status/[^/]+$")]
    private static partial Regex StatusLinkRegex();
}
