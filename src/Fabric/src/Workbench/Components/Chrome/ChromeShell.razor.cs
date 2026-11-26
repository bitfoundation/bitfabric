// [mirror] app chrome - nav panel items - keep in sync with:
// - src/Client/Bit.Fabric.Client.Core/Components/Layout/MainLayout.razor.items.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bit.Fabric.Workbench.Components.Chrome;

/// <summary>
/// The app's chrome around a page in a view: nav panel, header with the account menu, and bottom bar. It follows the
/// view's sign-in state, user and tenant without a server.
/// </summary>
public partial class ChromeShell
{
    /// <summary>The view's own address, so the nav items for this page link back to it with its settings.</summary>
    private string currentPageUrl = string.Empty;
    private List<BitNavItem> navPanelItems = [];

    [AutoInject] private ChromeStore chromeStore = default!;
    [AutoInject] private AuthenticationStateProvider authenticationStateProvider = default!;

    [Parameter] public WorkbenchPageInfo Page { get; set; } = default!;

    /// <summary>
    /// Whether the header names the page in an h1, as the app's header does only for a page that gives it a title: bitfabric.co's
    /// home page and /features give none, as their own titles are their h1s (bitfabric-internal/specs/home-page.md, 2026-11-18),
    /// and nor do the home page's directions, each of which is a home page too, or the comparison pages and /cli, whose heroes
    /// hold their h1s the same way.
    /// </summary>
    private bool ShowsTitle => Page.Key is not (Pages.Site.SitePageHost.HomeKey or Pages.Site.SitePageHost.FeaturesKey or Pages.Site.SitePageHost.CliKey or Pages.HomeDirections.HomeDirectionsPage.Key or Pages.Compare.ComparePage.Key);

    [Parameter] public RenderFragment? ChildContent { get; set; }

    public WorkbenchAuthState Auth => Scenario.Auth;

    public string TenantKey => chromeStore.TenantKey;

    public bool IsSignedIn => Auth is not WorkbenchAuthState.SignedOut;

    /// <summary>The demo user the view is signed in as.</summary>
    public WorkbenchDemoUser ActiveUser => WorkbenchDemoUsers.Find(Scenario.User);

    /// <summary>The other demo users, listed as signed-in accounts when the view shows several accounts.</summary>
    public IEnumerable<WorkbenchDemoUser> OtherAccounts => Auth is WorkbenchAuthState.MultiAccount
        ? WorkbenchDemoUsers.All.Where(u => u != ActiveUser)
        : [];

    protected override async Task OnInitializedAsync()
    {
        currentPageUrl = new Uri(NavigationManager.Uri).PathAndQuery;
        Subscribe(chromeStore);
        ScenarioState.Changed += OnScenarioChanged;

        await SetNavPanelItems();
    }

    // The account actions change the view's scenario instead of calling a server.
    public Task SignOut() => ReportScenario(Scenario with { Auth = WorkbenchAuthState.SignedOut, User = 0 });

    // The view reloads, as what it shows belongs to the account it's signed in as.
    public Task SwitchAccount(WorkbenchDemoUser user) => ReportScenario(Scenario with { User = WorkbenchDemoUsers.All.ToList().IndexOf(user) }, isApplied: false);

    public void SwitchTenant(string tenantKey) => chromeStore.SwitchTenant(tenantKey);

    /// <summary>
    /// The header's menu button opens the nav panel expanded, the way the app's AppShell does on OPEN_NAV_PANEL, so a panel
    /// collapsed on a wide screen doesn't open collapsed on a phone.
    /// </summary>
    public void OpenNavPanel()
    {
        chromeStore.ToggleNavPanel(false);
        chromeStore.OpenNavPanel(true);
    }

    /// <summary>Rebuilds the nav items when the view's scenario changes, since the sign-in state and the user decide them.</summary>
    private void OnScenarioChanged()
    {
        _ = InvokeAsync(async () =>
        {
            await SetNavPanelItems();
            StateHasChanged();
        });
    }

    /// <summary>What a nav item opens: a Workbench page in this view's settings, or the fallback anchor when the Workbench has no such page.</summary>
    public string PageUrl(string pageKey, string fallback)
    {
        if (WorkbenchPageCatalog.Find(pageKey) is null)
            return fallback;

        return pageKey == Page.Key ? currentPageUrl : WorkbenchUrls.Preview(pageKey, Scenario, PaneId);
    }

    /// <summary>
    /// A design of one of the app's own pages, rather than a showcase: the app's menus list it themselves (Home and Settings)
    /// or reach it from a page (the project wizard, from the home page's Get started), so the Workbench adds no item for it.
    /// </summary>
    public static bool IsAppPage(WorkbenchPageInfo page) =>
        page.CategoryKey is nameof(WorkbenchStrings.CategoryAppPages) or nameof(WorkbenchStrings.CategoryBitFabric);

    /// <summary>Builds the nav panel's items the way the app does, from the signed-in user's permissions, with the showcases added.</summary>
    private async Task SetNavPanelItems()
    {
        var authUser = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;

        List<BitNavItem> items =
        [
            new()
            {
                Text = AppLocalizer[nameof(AppStrings.Home)],
                IconName = BitIconName.Home,
                Url = PageUrl("app-home", "#home")
            },
            // MainLayout has the same item (PageUrls.Features, bitfabric-internal/specs/home-page.md, 2026-11-18)
            new()
            {
                Text = AppLocalizer[nameof(AppStrings.Features)],
                IconName = BitIconName.Puzzle,
                Url = PageUrl("app-features", "#features")
            },
            // MainLayout has the same item (PageUrls.Pricing, bitfabric-internal/specs/home-page.md, 2026-11-19)
            new()
            {
                Text = AppLocalizer[nameof(AppStrings.Pricing)],
                IconName = BitIconName.Savings,
                Url = PageUrl("app-pricing", "#pricing")
            },
            // MainLayout has the same item (PageUrls.Compare, bitfabric-internal/specs/home-page.md, "The comparison pages",
            // 2026-11-19)
            new()
            {
                Text = AppLocalizer[nameof(AppStrings.Compare)],
                IconName = BitIconName.Table,
                Url = PageUrl("app-compare", "#compare")
            },
            // MainLayout has the same item (PageUrls.For, bitfabric-internal/specs/home-page.md, "The AI agent pages", 2026-11-21)
            new()
            {
                Text = AppLocalizer[nameof(AppStrings.ForHubName)],
                IconName = BitIconName.Robot,
                Url = PageUrl("app-for", "#for")
            }
        ];

        // Shown to everyone, signed in or not: signed out, the page says what signing in gives
        // (bitfabric-internal/specs/monitoring.md, 2026-11-12).
        // MainLayout has the same item (PageUrls.Projects).
        items.Add(new()
        {
            Text = AppLocalizer[nameof(AppStrings.MonitoringProjects)],
            IconName = BitIconName.Health,
            Url = PageUrl("projects", "#projects")
        });

        // The showcases, which the app doesn't have, get a group of their own, without the ones a signed-out visitor can't open.
        items.Add(new()
        {
            Text = Localizer[nameof(WorkbenchStrings.NavShowcases)],
            IconName = BitIconName.Tiles,
            IsExpanded = true,
            ChildItems = [.. WorkbenchPageCatalog.Pages.Where(page => page.HasAppShell && IsAppPage(page) is false && (authUser.IsAuthenticated() || page.NeedsSignIn is false)).Select(page => new BitNavItem
            {
                Text = Localizer[page.TitleKey],
                IconName = page.IconName,
                Url = PageUrl(page.Key, page.Key)
            })]
        });

        var tenantIsSelected = await AuthorizationService.IsAuthorized(authUser, AuthPolicies.TENANT_SELECTED);



        // MainLayout has the same item (PageUrls.Trust, bitfabric-internal/specs/home-page.md, 2026-11-19)
        items.Add(new() { Text = AppLocalizer[nameof(AppStrings.Trust)], IconName = BitIconName.Shield, Url = PageUrl("app-trust", "#trust") });
        items.Add(new() { Text = AppLocalizer[nameof(AppStrings.Terms)], IconName = BitIconName.EntityExtraction, Url = "#terms" });
        items.Add(new() { Text = AppLocalizer[nameof(AppStrings.About)], IconName = BitIconName.Info, Url = "#about" });

        var manageRoles = await AuthorizationService.IsAuthorized(authUser, AppFeatures.Management.Roles_Manage);
        var manageUsers = await AuthorizationService.IsAuthorized(authUser, AppFeatures.Management.Users_Manage);
        var manageOAuthClients = await AuthorizationService.IsAuthorized(authUser, AppFeatures.System.OAuthClients_Manage);

        var manageAiPrompt = await AuthorizationService.IsAuthorized(authUser, AppFeatures.Management.SystemPrompts_Write);

        if (tenantIsSelected is false)
        {
            manageRoles = manageUsers = false;
            manageAiPrompt = false;
        }

        var manageTenantsGlobally = await AuthorizationService.IsAuthorized(authUser, AppFeatures.Management.Tenants_Manage_Global);

        if (manageRoles || manageUsers || manageOAuthClients
            || manageAiPrompt
            || manageTenantsGlobally
            )
        {
            BitNavItem managementItem = new()
            {
                Text = AppLocalizer[nameof(AppStrings.Management)],
                IconName = BitIconName.SettingsSecure,
                ChildItems = []
            };

            items.Add(managementItem);

            if (manageRoles)
            {
                managementItem.ChildItems.Add(new() { Text = AppLocalizer[nameof(AppStrings.UserGroups)], IconName = BitIconName.WorkforceManagement, Url = "#roles" });
            }

            if (manageUsers)
            {
                managementItem.ChildItems.Add(new() { Text = AppLocalizer[nameof(AppStrings.Users)], IconName = BitIconName.SecurityGroup, Url = "#users" });
            }

            if (manageOAuthClients)
            {
                managementItem.ChildItems.Add(new() { Text = AppLocalizer[nameof(AppStrings.OAuthClients)], IconName = BitIconName.PlugConnected, Url = "#oauth-clients" });
            }

            if (manageAiPrompt)
            {
                managementItem.ChildItems.Add(new() { Text = AppLocalizer[nameof(AppStrings.SystemPromptsTitle)], IconName = BitIconName.TextDocumentSettings, Url = "#system-prompts" });
            }

            if (manageTenantsGlobally)
            {
                managementItem.ChildItems.Add(new() { Text = AppLocalizer[nameof(AppStrings.ManageAllTenants)], IconName = BitIconName.Org, Url = "#tenants" });
            }
        }

        if (authUser.IsAuthenticated())
        {
            items.Add(new() { Text = AppLocalizer[nameof(AppStrings.Settings)], IconName = BitIconName.Equalizer, Url = PageUrl("settings", "#settings") });
        }

        // Assigned once it is complete, the way MainLayout does it.
        navPanelItems = items;
    }

    public override async ValueTask DisposeAsync()
    {
        ScenarioState.Changed -= OnScenarioChanged;

        await base.DisposeAsync();
    }
}
