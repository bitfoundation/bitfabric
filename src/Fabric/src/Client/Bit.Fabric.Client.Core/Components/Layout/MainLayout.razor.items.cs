// [mirror] app chrome - nav panel items - keep in sync with:
// - src/Workbench/Components/Chrome/ChromeShell.razor.cs
namespace Bit.Fabric.Client.Core.Components.Layout;

public partial class MainLayout
{
    private List<BitNavItem> navPanelItems = [];

    [AutoInject] protected IStringLocalizer<AppStrings> localizer = default!;
    [AutoInject] protected IAuthorizationService authorizationService = default!;

    private async Task SetNavPanelItems(ClaimsPrincipal authUser)
    {
        List<BitNavItem> items =
        [
            new()
            {
                Text = localizer[nameof(AppStrings.Home)],
                IconName = BitIconName.Home,
                Url = PageUrls.Home,
            },
            // Everything every project comes with, moved off the home page
            new()
            {
                Text = localizer[nameof(AppStrings.Features)],
                IconName = BitIconName.Puzzle,
                Url = PageUrls.Features,
            },
            // What bit Fabric costs today and what you pay others
            new()
            {
                Text = localizer[nameof(AppStrings.Pricing)],
                IconName = BitIconName.Savings,
                Url = PageUrls.Pricing,
            },
            // bit Fabric side by side with the other ways, and a page for each
            new()
            {
                Text = localizer[nameof(AppStrings.Compare)],
                IconName = BitIconName.Table,
                Url = PageUrls.Compare,
                AdditionalUrls = [PageUrls.CompareLovable, PageUrls.CompareBolt, PageUrls.CompareBase44, PageUrls.CompareReplit, PageUrls.CompareV0, PageUrls.CompareEmptyProject, PageUrls.CompareStarterKits]
            },
            // The AI agents a project is ready for, and a page for each
            new()
            {
                Text = localizer[nameof(AppStrings.ForHubName)],
                IconName = BitIconName.Robot,
                Url = PageUrls.For,
                AdditionalUrls = [PageUrls.ForGitHubCopilot, PageUrls.ForClaudeCode, PageUrls.ForCodex, PageUrls.ForCursor, PageUrls.ForAntigravity, PageUrls.ForGeminiCli, PageUrls.ForJunie, PageUrls.ForWindsurf, PageUrls.ForLocalModels, PageUrls.ForChineseModels]
            },
            // Shown to everyone: a signed-out visitor who opens it is asked to sign in, and why.
            new()
            {
                Text = localizer[nameof(AppStrings.MonitoringProjects)],
                IconName = BitIconName.Health,
                Url = PageUrls.Projects,
                AdditionalUrls = [PageUrls.AddProject, PageUrls.Licenses]
            }
        ];

        var tenantIsSelected = await authorizationService.IsAuthorized(authUser!, AuthPolicies.TENANT_SELECTED);



        // Who makes bit Fabric and what it can see, next to the terms
        items.Add(new()
        {
            Text = localizer[nameof(AppStrings.Trust)],
            IconName = BitIconName.Shield,
            Url = PageUrls.Trust,
        });

        items.Add(new()
        {
            Text = localizer[nameof(AppStrings.Terms)],
            IconName = BitIconName.EntityExtraction,
            Url = PageUrls.Terms,
        });

        items.Add(new()
        {
            Text = localizer[nameof(AppStrings.About)],
            IconName = BitIconName.Info,
            Url = PageUrls.About,
        });

        var (manageRoles, manageUsers) = await (authorizationService.IsAuthorized(authUser!, AppFeatures.Management.Roles_Manage),
            authorizationService.IsAuthorized(authUser!, AppFeatures.Management.Users_Manage));

        var manageOAuthClients = await authorizationService.IsAuthorized(authUser!, AppFeatures.System.OAuthClients_Manage);

        var viewGrowth = await authorizationService.IsAuthorized(authUser!, AppFeatures.System.Growth_View);

        var manageAiPrompt = await authorizationService.IsAuthorized(authUser!, AppFeatures.Management.SystemPrompts_Write);

        if (tenantIsSelected is false)
        {
            manageRoles = manageUsers = false;
            manageAiPrompt = false;
        }

        var manageTenantsGlobally = await authorizationService.IsAuthorized(authUser!, AppFeatures.Management.Tenants_Manage_Global);

        // Every flag in this condition has to be able to contribute a child item below, or the group renders empty.
        if (manageRoles || manageUsers || manageOAuthClients || viewGrowth
            || manageAiPrompt
            || manageTenantsGlobally
            )
        {
            BitNavItem managementItem = new()
            {
                Text = localizer[nameof(AppStrings.Management)],
                IconName = BitIconName.SettingsSecure,
                ChildItems = []
            };

            items.Add(managementItem);

            if (manageRoles)
            {
                managementItem.ChildItems.Add(new()
                {
                    Text = localizer[nameof(AppStrings.UserGroups)],
                    IconName = BitIconName.WorkforceManagement,
                    Url = PageUrls.Roles,
                });
            }

            if (manageUsers)
            {
                managementItem.ChildItems.Add(new()
                {
                    Text = localizer[nameof(AppStrings.Users)],
                    IconName = BitIconName.SecurityGroup,
                    Url = PageUrls.Users,
                });
            }

            if (manageOAuthClients)
            {
                managementItem.ChildItems.Add(new()
                {
                    Text = localizer[nameof(AppStrings.OAuthClients)],
                    IconName = BitIconName.PlugConnected,
                    Url = PageUrls.OAuthClients,
                });
            }

            if (viewGrowth)
            {
                managementItem.ChildItems.Add(new()
                {
                    Text = localizer[nameof(AppStrings.GrowthTitle)],
                    IconName = BitIconName.AreaChart,
                    Url = PageUrls.Growth,
                });
            }

            if (manageAiPrompt)
            {
                managementItem.ChildItems.Add(new()
                {
                    Text = localizer[nameof(AppStrings.SystemPromptsTitle)],
                    IconName = BitIconName.TextDocumentSettings,
                    Url = PageUrls.SystemPrompts,
                });
            }

            if (manageTenantsGlobally)
            {
                managementItem.ChildItems.Add(new()
                {
                    Text = localizer[nameof(AppStrings.ManageAllTenants)],
                    IconName = BitIconName.Org,
                    Url = PageUrls.ManageAllTenants,
                });
            }
        }

        if (authUser.IsAuthenticated())
        {
            items.Add(new()
            {
                Text = localizer[nameof(AppStrings.Settings)],
                IconName = BitIconName.Equalizer,
                Url = PageUrls.Settings,
                Match = BitNavMatch.Prefix
            });
        }

        AddCultureUrls(items);

        // Assigned once it is complete: a render can happen at any of the awaits above, and AppShell compares the list by
        // reference (See AppShell.GetParametersRenderKey), so a list filled in after it was assigned would stay half shown.
        navPanelItems = items;
    }

    /// <summary>
    /// The items link to the page under the current culture (<c>/en-US/about</c>), the address a full page load serves it
    /// under, so a search engine that follows one meets no redirect. Every page answers to its
    /// culture-less url too (<c>/about</c>), which the app's own code navigates to, and to every other culture's. BitNav
    /// marks the current page by comparing the url with each item's urls, so it is given all of those forms.
    /// </summary>
    private static void AddCultureUrls(List<BitNavItem> items)
    {
        foreach (var item in items)
        {
            if (item.Url is not null)
            {
                string[] urls = [item.Url, .. item.AdditionalUrls ?? []];
                item.AdditionalUrls = [.. urls, .. urls.SelectMany(CultureInfoManager.GetCultureUrls)];
                item.Url = CultureInfoManager.GetCurrentCultureUrl(item.Url);
            }

            AddCultureUrls(item.ChildItems);
        }
    }
}
