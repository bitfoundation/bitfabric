namespace Bit.Fabric.Tests.Features.Layout;

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
}
