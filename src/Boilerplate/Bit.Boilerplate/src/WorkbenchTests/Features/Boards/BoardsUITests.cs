namespace Boilerplate.WorkbenchTests.Features.Boards;

/// <summary>
/// The boards an agent builds instead of a long answer in the chat, through the two that ship as examples: the section
/// lists them, each opens on its own address, what the reader changes on one works without the page jumping, a board
/// reads in its own language whatever the Workbench's, and the home page and the header lead to them.
/// </summary>
[TestClass, TestCategory("UITest")]
public class BoardsUITests : WorkbenchPageTest
{
    private ILocator Summary => Page.Locator(".calculator-summary");

    // Waits for the page to answer clicks, which a prerendered page doesn't yet.
    private async Task OpenBoards(string path = "")
    {
        await Page.GotoAsync(new Uri(Server.Address, $"boards{path}").ToString());
        await WaitUntilInteractive();
    }

    private Task UseCulture(string culture) =>
        Context.AddCookiesAsync([new() { Name = ".AspNetCore.Culture", Value = $"c={culture}|uic={culture}", Url = Server.Address.ToString() }]);

    [TestMethod, EverySurface]
    public async Task TheBoards_Should_ListTheExamplesPaymentsFirst(WorkbenchSurface surface)
    {
        await OpenBoards();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = WorkbenchStrings.Boards, Level = 1, Exact = true })).ToBeVisibleAsync();

        var cards = Page.Locator("a.board-card");

        await Expect(cards).ToHaveCountAsync(2);
        await Expect(cards.Nth(0)).ToHaveAttributeAsync("href", "/boards/payments");
        await Expect(cards.Nth(1)).ToHaveAttributeAsync("href", "/boards/deploy");

        await Press(cards.Nth(1));

        await Expect(Page).ToHaveURLAsync(new Regex("/boards/deploy$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = WorkbenchStrings.DeployBoardTitle, Level = 1, Exact = true })).ToBeVisibleAsync();
    }

    [TestMethod, OnDesktops]
    public async Task AnUnknownBoard_Should_SaySoAndLeadBackToTheBoards(WorkbenchSurface surface)
    {
        await OpenBoards("/nothing-here");

        await Expect(Page.GetByText(WorkbenchStrings.BoardNotFound)).ToBeVisibleAsync();
        await Expect(Page.Locator("main.missing a[href='/boards']")).ToBeVisibleAsync();
    }

    [TestMethod, EverySurface]
    public async Task TheDeployBoard_Should_DrawThisProjectsWayToItsUsers(WorkbenchSurface surface)
    {
        await OpenBoards("/deploy");

        // The template's own options: .NET Aspire behind Cloudflare, so six stops from a change to the users.
        var stops = Page.Locator(".pipeline .stop");

        await Expect(stops).ToHaveCountAsync(6);
        await Expect(stops.Nth(0)).ToContainTextAsync(WorkbenchStrings.DeployStopChanges);
        await Expect(stops.Nth(4)).ToContainTextAsync(WorkbenchStrings.DeployStopSafe);
        await Expect(stops.Nth(5)).ToContainTextAsync(WorkbenchStrings.DeployStopUsers);

        await Expect(Page.Locator(".box.app")).Not.ToHaveCountAsync(0);
        await Expect(Page.Locator(".setting").First).ToBeAttachedAsync();
    }

    [TestMethod, EverySurface]
    public async Task ThePaymentsBoard_Should_WorkOutWhatEachOneKeepsFromASale(WorkbenchSurface surface)
    {
        await OpenBoards("/payments");

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Taking payments", Level = 1, Exact = true })).ToBeVisibleAsync();
        await Expect(Summary).ToHaveTextAsync("On a €10.00 sale, Stripe keeps the least, €0.40, and Google Play and the App Store the most, €1.50.");

        // Stripe Billing's 0.7% puts Mollie ahead on a subscription.
        await Press(Page.Locator("#one-sale label", new() { HasText = "Subscription" }));

        await Expect(Summary).ToHaveTextAsync("On a €10.00 subscription payment, Mollie keeps the least, €0.43, and Google Play and the App Store the most, €1.50.");

        // At the lowest price, the fixed fees outweigh the stores' 15%.
        await Press(Page.Locator("#one-sale label", new() { HasText = "One-off" }));
        await Page.Locator("#one-sale [role='slider'], #one-sale input[type='range']").First.FocusAsync();
        await Page.Keyboard.PressAsync("Home");

        await Expect(Summary).ToHaveTextAsync("On a €1.00 sale, Google Play and the App Store keep the least, €0.15, and Paddle the most, €0.55.");
    }

    [TestMethod, EverySurface]
    public async Task ThePaymentsBoardsStoreTerms_Should_ChangeWithTheRegionWithoutChangingSize(WorkbenchSurface surface)
    {
        await OpenBoards("/payments");

        var ways = Page.Locator("#stores .ways");
        var region = (string name) => Page.Locator("#stores .region-pick label", new() { HasTextRegex = new Regex($"^{name}$") });

        var height = () => ways.EvaluateAsync<double>("element => element.getBoundingClientRect().height");

        await Expect(ways).ToContainTextAsync("15–26%");
        var size = await height();

        await Press(region("US"));

        await Expect(ways).ToContainTextAsync("Not allowed");
        Assert.AreEqual(size, await height(), 0.5, "The terms keep their size from one region to the next.");

        await Press(region("Elsewhere"));

        await Expect(ways).ToContainTextAsync("Reader apps");
        Assert.AreEqual(size, await height(), 0.5, "The terms keep their size from one region to the next.");
    }

    [TestMethod, OnDesktops]
    [TestCategory("Localization")]
    public async Task ABoard_Should_ReadInItsOwnLanguageInAPersianWorkbench(WorkbenchSurface surface)
    {
        await UseCulture("fa-IR");

        // Written in English, the payments board stays English and left to right.
        await OpenBoards("/payments");

        await Expect(Page.Locator("article.board")).ToHaveAttributeAsync("lang", "en");
        await Expect(Page.Locator("article.board")).ToHaveCSSAsync("direction", "ltr");

        // The deploy board's words are the Workbench's own, so it reads in Persian, right to left.
        await OpenBoards("/deploy");

        await Expect(Page.Locator("article.board")).ToHaveAttributeAsync("lang", "fa-IR");
        await Expect(Page.Locator("article.board")).ToHaveCSSAsync("direction", "rtl");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = InCulture("fa-IR", nameof(WorkbenchStrings.DeployBoardTitle)), Level = 1 })).ToBeVisibleAsync();
    }

    [TestMethod, EverySurface]
    public async Task TheHomePage_Should_LeadToTheBoards(WorkbenchSurface surface)
    {
        await Page.GotoAsync(Server.Address.ToString());

        var section = Page.Locator("#boards");

        await Expect(section.GetByRole(AriaRole.Heading, new() { Name = WorkbenchStrings.HomeBoardsTitle, Level = 2 })).ToBeVisibleAsync();
        await Expect(section.GetByText(WorkbenchStrings.BoardsPrompt)).ToBeVisibleAsync();
        await Expect(section.Locator("a.board-card").Nth(0)).ToHaveAttributeAsync("href", "/boards/payments");
        await Expect(section.Locator("a.board-card").Nth(1)).ToHaveAttributeAsync("href", "/boards/deploy");
        await Expect(section.Locator("a[href='/boards']")).ToBeVisibleAsync();
    }

    [TestMethod, OnDesktops]
    public async Task TheHeader_Should_LinkToTheBoards(WorkbenchSurface surface)
    {
        await Page.GotoAsync(Server.Address.ToString());

        await Expect(Page.Locator("header.shell-header nav a[href='/boards']")).ToHaveTextAsync(WorkbenchStrings.Boards);
    }
}
