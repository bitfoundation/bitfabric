namespace Boilerplate.Tests.Features.Diagnostics;

/// <summary>
/// bit BevTools is the app's in-app dev tools, and the way into it on a phone is the one a support person tells a user
/// about: tap the empty space of the header 7 times. It lands on the app's own tab, with the diagnostic report and the
/// actions a support session asks for (See AppDevTools, AppBevTools and AppDevToolsPanel).
/// </summary>
[TestClass, TestCategory("UITest"), Retry(2)]
public partial class DevToolsUITests : AppPageTest
{
    [TestMethod]
    public async Task TappingTheHeadersEmptySpace7Times_Should_OpenBevTools_OnTheAppsOwnTab()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        var throwTestError = Page.Locator("button[title='Throw test error']");

        // Six taps are not seven: nothing opens yet.
        var spacer = Page.Locator("header .bit-spc").First;
        for (var tap = 0; tap < 6; tap++)
        {
            await spacer.ClickAsync();
        }
        await Expect(throwTestError).ToHaveCountAsync(0);

        await spacer.ClickAsync();

        // BevTools, on the app's own tab.
        await Expect(Page.GetByRole(AriaRole.Tab, new() { Name = "App", Exact = true })).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(throwTestError).ToBeVisibleAsync();
        await Expect(Page.Locator("button[title='Clear app files']")).ToBeVisibleAsync();
        await Expect(Page.Locator("button[title='Update app']")).ToBeVisibleAsync();
        // The diagnostic report, whose server half waits until asked for.
        await Expect(Page.Locator("button[title='Ask over SignalR']")).ToBeVisibleAsync();
    }

    /// <summary>
    /// The App tab opens the Hangfire dashboard for whoever holds System.Jobs_Manage, the feature the dashboard asks
    /// for (HangfireDashboardAuthorizationFilter). It runs in Blazor WebAssembly mode: the dashboard signs the browser
    /// in by the access_token cookie, which only the browser's own call to UpdateSession writes on the app's host.
    /// </summary>
    [TestMethod]
    public async Task TheAppTab_Should_OpenTheHangfireDashboard_ForWhoeverMayOpenIt_AndShowItToNobodyElse()
    {
        await using var server = new AppTestServer(Context);
        await server.Build(configureTestConfiguration: configuration => configuration["WebAppRender:BlazorMode"] = nameof(BlazorWebAppMode.BlazorWebAssembly))
            .Start(TestContext.CancellationToken);

        // A new user holds no feature.
        await MagicLinkSignInUtils.SignInViaMagicLinkOtp(Page, server, MagicLinkSignInUtils.NewTestEmail(), TestContext.CancellationToken);
        await OpenBevTools(Page);

        await Expect(Page.Locator("button[title='Update app']")).ToBeVisibleAsync();
        await Expect(HangfireDashboard(Page)).ToHaveCountAsync(0);

        // The global admin, in a browser of their own.
        await using var adminContext = await NewBrowserContext(server);
        var adminPage = await adminContext.NewPageAsync();

        await adminPage.GotoAsync(new Uri(server.WebAppAddress, PageUrls.SignIn).ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await SignInPanelUtils.FillCredentials(adminPage, TestData.DefaultTestEmail, TestData.DefaultTestPassword);
        await adminPage.GetByRole(AriaRole.Button, new() { Name = AppStrings.Continue, Exact = true }).ClickAsync();
        await Expect(adminPage).ToHaveURLAsync(server.WebAppAddress.ToString());

        await OpenBevTools(adminPage);

        var dashboard = await adminPage.RunAndWaitForPopupAsync(() => HangfireDashboard(adminPage).ClickAsync());
        await dashboard.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        Assert.AreEqual(server.WebAppAddress.Host, new Uri(dashboard.Url).Host, "The dashboard opens where the cookie is.");
        Assert.StartsWith("/hangfire", new Uri(dashboard.Url).AbsolutePath);
        // Hangfire answers a browser it doesn't let in with an empty 401, which has no title.
        await Expect(dashboard).ToHaveTitleAsync(new Regex("Hangfire"));
    }

    private static ILocator HangfireDashboard(IPage page) => page.Locator("button[title='Hangfire dashboard']");

    /// <summary>Taps the empty space of the header 7 times, as a support person tells a user to.</summary>
    private async Task OpenBevTools(IPage page)
    {
        await page.WaitForBlazorInteractive();

        var spacer = page.Locator("header .bit-spc").First;
        for (var tap = 0; tap < 7; tap++)
        {
            await spacer.ClickAsync();
        }

        await Expect(page.GetByRole(AriaRole.Tab, new() { Name = "App", Exact = true })).ToHaveAttributeAsync("aria-selected", "true");
    }
}
