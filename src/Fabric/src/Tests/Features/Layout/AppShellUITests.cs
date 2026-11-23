using System.Globalization;

namespace Bit.Fabric.Tests.Features.Layout;

/// <summary>
/// The app's main scenarios around its shell - the nav panel, the header, the account menu and the AI chat launcher -
/// as a user goes through them. They describe how the app works today, so a change to the layout or to how it renders
/// (MainLayout, AppShell, AppMenu, AppAiChatPanel) that breaks one of them breaks something users rely on.
/// </summary>
[TestClass, TestCategory("UITest"), Retry(2)]
public partial class AppShellUITests : AppPageTest
{
    [TestMethod]
    public async Task AVisitor_Should_SeeThePublicPartsOfTheApp_AndTheWaysToSignIn()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Expect(Page).ToHaveTitleAsync(AppStrings.HomePageTitle);
        await ExpectTheHomePagesHeading();
        await AppShellPage.ExpectAVisitorsShell(Page);
        await Expect(AppShellPage.AiChatLauncher(Page)).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task NavigatingThroughTheApp_Should_KeepTheHeaderInStep_WithThePageShown()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        await AppShellPage.NavPanelItem(Page, AppStrings.About).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.About}$"));
        await Expect(Page).ToHaveTitleAsync(AppStrings.AboutBitFabricPageTitle);
        await ExpectTheAboutPagesHeading();
        await Expect(AppShellPage.NavPanelItem(Page, AppStrings.About)).ToHaveAttributeAsync("aria-current", "page");

        await AppShellPage.NavPanelItem(Page, AppStrings.Terms).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.Terms}$"));
        await Expect(Page).ToHaveTitleAsync(AppStrings.TermsPageTitle);
        await Expect(AppShellPage.PageHeading(Page)).ToHaveTextAsync(AppStrings.Terms);
        await Expect(AppShellPage.NavPanelItem(Page, AppStrings.Terms)).ToHaveAttributeAsync("aria-current", "page");
        await Expect(AppShellPage.NavPanelItem(Page, AppStrings.About)).Not.ToHaveAttributeAsync("aria-current", "page");

        await Page.GoBackAsync();

        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.About}$"));
        // The header lets go of the terms' title
        await ExpectTheAboutPagesHeading();

        // The page stayed the same document all along: the app navigated in place, it did not reload.
        await Expect(AppShellPage.NavPanel(Page)).ToBeVisibleAsync();
        await AppShellPage.ExpectAVisitorsShell(Page);
    }

    /// <summary>
    /// On a phone the nav panel is a drawer that WebAssembly leaves out of the app's start until it first opens, and the
    /// acknowledgements, which the About page's last section links, follow the rest of their page
    /// (bitfabric-internal/specs/app-shell.md). The tests run the app on Blazor Server, which renders both at once, so this
    /// checks what a phone user relies on rather than the deferral itself.
    /// </summary>
    [TestMethod]
    public async Task OnAPhone_TheNavPanel_Should_OpenFromTheHeader_AndTakeTheUserToThePagePicked()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.SetViewportSizeAsync(412, 915);
        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        await Page.Locator("header").GetByRole(AriaRole.Button, new() { Name = AppStrings.OpenNavigationMenuTitle, Exact = true }).ClickAsync();

        await Expect(AppShellPage.NavPanelItem(Page, AppStrings.About)).ToBeVisibleAsync();
        await AppShellPage.NavPanelItem(Page, AppStrings.About).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.About}$"));
        await ExpectTheAboutPagesHeading();

        // The license notice is a page of its own, which the About page's last section links
        // (bitfabric-internal/specs/home-page.md, 2026-11-19)
        await Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.AboutLicensesLink }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.OpenSourceLicenses}$"));
        await Expect(AppShellPage.PageHeading(Page)).ToHaveTextAsync(AppStrings.OpenSourceLicensesTitle);
        await Expect(Page.GetByText("Standing on the shoulders of giants", new() { Exact = true })).ToBeVisibleAsync();
        // Its name goes on with the new tab hint every link that opens a tab has (See AppBitParams)
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Hangfire" }).First).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task TheSignInPage_Should_ReplaceTheAppsLayout_UntilTheUserGoesBackHome()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        await AppShellPage.NavPanelLink(Page, AppStrings.SignIn).ClickAsync();

        await Expect(Page).ToHaveTitleAsync(AppStrings.SignInPageTitle);
        await Expect(AppShellPage.NavPanel(Page)).ToHaveCountAsync(0);

        await Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.BackToHome }).ClickAsync();

        await Expect(Page).ToHaveTitleAsync(AppStrings.HomePageTitle);
        await ExpectTheHomePagesHeading();
        await AppShellPage.ExpectAVisitorsShell(Page);
    }

    /// <summary>
    /// The home page's one h1 is its hero's title, so the header, which names every other page in its h1, has no heading
    /// there (bitfabric-internal/specs/home-page.md, 2026-11-18).
    /// </summary>
    private async Task ExpectTheHomePagesHeading(string? title = null)
    {
        await Expect(AppShellPage.PageHeading(Page)).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1, Name = title ?? AppStrings.AppHomeBuildWithAIOwnEverything })).ToBeVisibleAsync();
    }

    /// <summary>The About page is one of the home page's siblings, with its own title as its one h1 (bitfabric-internal/specs/home-page.md, 2026-11-19).</summary>
    private async Task ExpectTheAboutPagesHeading()
    {
        await Expect(AppShellPage.PageHeading(Page)).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1, Name = AppStrings.AboutTitle })).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task SwitchingTheTheme_Should_ApplyAtOnce_AndBeRememberedAfterAReload()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        // The app starts on the device's own color scheme.
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });

        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        var html = Page.Locator("html");
        await Expect(html).ToHaveAttributeAsync("bit-theme", new Regex("dark"));

        await AppShellPage.OpenAccountMenu(Page, AppStrings.AccountMenuTitle);
        await AppShellPage.AccountMenu(Page).GetByRole(AriaRole.Button, new() { Name = AppStrings.Dark, Exact = true }).ClickAsync();

        await Expect(html).ToHaveAttributeAsync("bit-theme", new Regex("light"));
        await Expect(AppShellPage.AccountMenu(Page).GetByRole(AriaRole.Button, new() { Name = AppStrings.Light, Exact = true })).ToBeVisibleAsync();

        await Page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Expect(html).ToHaveAttributeAsync("bit-theme", new Regex("light"));
    }

    [TestMethod]
    public async Task SwitchingTheLanguage_Should_TranslateTheApp_AndTurnItRightToLeft()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        await AppShellPage.OpenAccountMenu(Page, AppStrings.AccountMenuTitle);
        await AppShellPage.AccountMenu(Page).GetByRole(AriaRole.Button, new() { Name = AppStrings.Language }).ClickAsync();
        await AppShellPage.AccountMenu(Page).GetByText("فارسی", new() { Exact = true }).ClickAsync();

        var persian = CultureInfo.GetCultureInfo("fa-IR");
        string InPersian(string key) => AppStrings.ResourceManager.GetString(key, persian)!;

        var html = Page.Locator("html");
        await Expect(html).ToHaveAttributeAsync("lang", "fa-IR");
        await Expect(html).ToHaveAttributeAsync("dir", "rtl");
        await Expect(AppShellPage.NavPanelItem(Page, InPersian(nameof(AppStrings.About)))).ToBeVisibleAsync();
        await ExpectTheHomePagesHeading(InPersian(nameof(AppStrings.AppHomeBuildWithAIOwnEverything)));

        await Page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Expect(html).ToHaveAttributeAsync("lang", "fa-IR");
        await Expect(html).ToHaveAttributeAsync("dir", "rtl");
        await Expect(AppShellPage.NavPanelItem(Page, InPersian(nameof(AppStrings.About)))).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task TheAiChatPanel_Should_OpenWithAGreeting()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        await AppShellPage.AiChatLauncher(Page).ClickAsync();

        var panel = Page.GetByRole(AriaRole.Dialog, new() { Name = AppStrings.AiChatPanelTitle });
        await Expect(panel).ToBeVisibleAsync();
        await Expect(panel).ToContainTextAsync(string.Format(AppStrings.AiChatPanelInitialResponse, string.Empty));
    }

    /// <summary>
    /// The panel used to write its greeting on its first render, which on a fresh load comes before the user's profile
    /// has arrived, so a signed in user who opened the app was greeted without their name.
    /// </summary>
    [TestMethod]
    public async Task TheAiChatPanel_Should_GreetASignedInUserByName_AfterAReload()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await SignInPanelUtils.SignIn(Page, server, TestData.DefaultTestEmail, TestData.DefaultTestPassword, TestData.DefaultTestFullName);

        await Page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        await AppShellPage.AiChatLauncher(Page).ClickAsync();

        var panel = Page.GetByRole(AriaRole.Dialog, new() { Name = AppStrings.AiChatPanelTitle });
        await Expect(panel).ToContainTextAsync(string.Format(AppStrings.AiChatPanelInitialResponse, $" {TestData.DefaultTestFullName}"));
    }
}
