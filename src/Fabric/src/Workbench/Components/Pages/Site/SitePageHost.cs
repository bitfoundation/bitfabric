using System.Text.RegularExpressions;

namespace Bit.Fabric.Workbench.Components.Pages.Site;

/// <summary>
/// Base of the hosts of bitfabric.co's own pages, the home page and the new project wizard. A host renders the app's real
/// component from Client.Core, the way the monitoring pages' hosts do, so the Workbench shows what ships rather than a copy
/// of it that drifts. A link of one of these pages to the other, such as Get started, opens that page here, in the view's
/// settings, and one to sign-in says what the Workbench offers instead.
/// </summary>
public abstract partial class SitePageHost : WorkbenchComponentBase
{
    public const string HomeKey = "app-home";

    public const string FeaturesKey = "app-features";

    public const string CostKey = "app-cost";

    public const string NewProjectKey = "new-project";

    public const string SetupProjectKey = "setup-project";

    [AutoInject] private WorkbenchAppLinks appLinks = default!;

    private IDisposable? linkHandler;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (IsPrerendering)
            return;

        linkHandler = appLinks.Add(TranslateAppLink);
    }

    /// <summary>
    /// A page of the site in a view's settings, without the page's own switches, as the Workbench drops them on its way to
    /// another page: the address the view is then given is the one it is on already, so it follows the view there without
    /// loading it again, and a tour going on in the view, such as the home page's onboarding, carries on.
    /// </summary>
    public static string PreviewOf(string pageKey, WorkbenchScenario scenario, string? paneId)
        => WorkbenchUrls.Preview(pageKey, scenario with { Variant = null, Switches = null }, paneId);

    /// <summary>What a link of the app means here: the home page, the features page, the cost page, the wizard, the setup page or the sign-in note; null leaves it to others.</summary>
    private Func<Task>? TranslateAppLink(Uri target)
    {
        var path = CultureSegmentRegex().Replace(target.AbsolutePath, string.Empty).TrimEnd('/');

        if (path is PageUrls.SignIn or PageUrls.SignUp)
            return ShowSignInNote;

        if (path.Length is 0)
            return Go(HomeKey);

        if (string.Equals(path, PageUrls.Features, StringComparison.OrdinalIgnoreCase))
            return Go(FeaturesKey);

        if (string.Equals(path, PageUrls.Cost, StringComparison.OrdinalIgnoreCase))
            return Go(CostKey);

        if (string.Equals(path, PageUrls.NewProject, StringComparison.OrdinalIgnoreCase))
            return Go(NewProjectKey);

        if (string.Equals(path, PageUrls.SetupProject, StringComparison.OrdinalIgnoreCase))
            return Go(SetupProjectKey);

        return null;
    }

    private Func<Task> Go(string pageKey) => async () =>
    {
        var scenario = Scenario with { Variant = null, Switches = null };
        var isSamePage = string.Equals(new Uri(NavigationManager.Uri).Segments[^1].Trim('/'), pageKey, StringComparison.OrdinalIgnoreCase);

        // The page that opens reads its switches from the scenario, which a navigation in the same circuit doesn't parse again.
        ScenarioState.Set(scenario);
        NavigationManager.NavigateTo(WorkbenchUrls.Preview(pageKey, scenario, PaneId), replace: true);

        // Another page tells the shell itself (See PreviewPage); on the same page, the shell hears of the new switches from here.
        if (isSamePage)
        {
            await ReportScenario(scenario);
        }
    };

    public override async ValueTask DisposeAsync()
    {
        linkHandler?.Dispose();

        await base.DisposeAsync();
    }

    /// <summary>The culture segment the app's routes may start with, such as /fa-IR/new or /fa-IR.</summary>
    [GeneratedRegex("^/[a-z]{2}-[A-Z]{2}(?=/|$)")]
    private static partial Regex CultureSegmentRegex();
}
