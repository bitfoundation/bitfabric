namespace Bit.Fabric.Tests.Features.Layout;

/// <summary>
/// The parts of the app every page sits in - the nav panel, the header with the page's title and the account menu, the
/// bottom bar and the AI chat launcher - found the way a user finds them, so a scenario reads as what the user does.
/// <para>
/// The nav panel and the account menu's callout are found by the classes the app gives them (See AppShell.razor and
/// AppMenu.razor), not by bit BlazorUI's internal ones, which change without notice.
/// </para>
/// </summary>
public static class AppShellPage
{
    public static ILocator NavPanel(IPage page) => page.Locator(".nav-panel");

    /// <summary>
    /// A page in the nav panel's menu. Its navigation landmark only: the panel's logo above it links home as well, and
    /// the sign in links are in its footer (See <see cref="NavPanelLink"/>).
    /// </summary>
    public static ILocator NavPanelItem(IPage page, string text) => NavPanel(page).GetByRole(AriaRole.Navigation).GetByRole(AriaRole.Link, new() { Name = text, Exact = true });

    public static ILocator NavPanelGroup(IPage page, string text) => NavPanel(page).GetByRole(AriaRole.Navigation).GetByRole(AriaRole.Button, new() { Name = text, Exact = true });

    /// <summary>A link anywhere in the nav panel whose name no other link there has, such as the footer's sign in.</summary>
    public static ILocator NavPanelLink(IPage page, string text) => NavPanel(page).GetByRole(AriaRole.Link, new() { Name = text, Exact = true });

    /// <summary>The tenant the user is signed into, shown at the bottom of the nav panel.</summary>
    public static ILocator CurrentTenant(IPage page) => NavPanel(page).Locator(".current-tenant");

    /// <summary>The page's title, as the header shows it.</summary>
    public static ILocator PageHeading(IPage page) => page.Locator("header").GetByRole(AriaRole.Heading, new() { Level = 1 });

    /// <summary>
    /// The button that opens the account menu. Its name is the signed in user's display name, or
    /// <see cref="AppStrings.AccountMenuTitle"/> for a visitor.
    /// </summary>
    public static ILocator AccountMenuButton(IPage page, string name) => page.Locator("header").GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    public static ILocator AccountMenu(IPage page) => page.Locator(".app-menu-callout");

    public static async Task OpenAccountMenu(IPage page, string accountMenuButtonName)
    {
        await AccountMenuButton(page, accountMenuButtonName).ClickAsync();
        await Assertions.Expect(AccountMenu(page)).ToBeVisibleAsync();
    }

    public static ILocator AiChatLauncher(IPage page) => page.GetByRole(AriaRole.Button, new() { Name = AppStrings.AiChatPanelTitle, Exact = true });

    public static async Task SignOut(IPage page, string accountMenuButtonName)
    {
        await OpenAccountMenu(page, accountMenuButtonName);
        await AccountMenu(page).GetByRole(AriaRole.Button, new() { Name = AppStrings.SignOut, Exact = true }).ClickAsync();

        var confirmation = page.GetByRole(AriaRole.Alertdialog, new() { Name = AppStrings.SignOut });
        await confirmation.GetByRole(AriaRole.Button, new() { Name = AppStrings.SignOut, Exact = true }).ClickAsync();
    }

    /// <summary>
    /// What a visitor - nobody signed in - sees around any page: the public links only, the ways to sign in and sign up,
    /// and an account menu that belongs to nobody.
    /// </summary>
    public static async Task ExpectAVisitorsShell(IPage page)
    {
        await Assertions.Expect(AccountMenuButton(page, AppStrings.AccountMenuTitle)).ToBeVisibleAsync();

        await Assertions.Expect(NavPanelItem(page, AppStrings.Home)).ToBeVisibleAsync();
        await Assertions.Expect(NavPanelItem(page, AppStrings.Features)).ToBeVisibleAsync();
        await Assertions.Expect(NavPanelItem(page, AppStrings.Pricing)).ToBeVisibleAsync();
        await Assertions.Expect(NavPanelItem(page, AppStrings.Compare)).ToBeVisibleAsync();
        await Assertions.Expect(NavPanelItem(page, AppStrings.Trust)).ToBeVisibleAsync();
        await Assertions.Expect(NavPanelItem(page, AppStrings.Terms)).ToBeVisibleAsync();
        await Assertions.Expect(NavPanelItem(page, AppStrings.About)).ToBeVisibleAsync();
        await Assertions.Expect(NavPanelLink(page, AppStrings.SignIn)).ToBeVisibleAsync();
        await Assertions.Expect(NavPanelLink(page, AppStrings.SignUp)).ToBeVisibleAsync();

        await Assertions.Expect(NavPanelItem(page, AppStrings.Settings)).ToHaveCountAsync(0);
        await Assertions.Expect(NavPanelGroup(page, AppStrings.Management)).ToHaveCountAsync(0);
        await Assertions.Expect(CurrentTenant(page)).ToHaveCountAsync(0);
    }
}
