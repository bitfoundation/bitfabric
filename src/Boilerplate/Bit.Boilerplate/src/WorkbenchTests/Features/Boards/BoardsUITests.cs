namespace Boilerplate.WorkbenchTests.Features.Boards;

/// <summary>
/// The boards an agent builds instead of a long answer in the chat, through the three that ship as examples: the section's own
/// board lists them by kind, each opens on its own address with every board in the side panel, whose search reads what they
/// say, what the reader changes on one works without the page jumping, a flow of live screens joined by labeled arrows shows
/// them one at a time and goes full screen, a board reads in its own language whatever the Workbench's, and the home page
/// and the header lead to them.
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

    /// <summary>A kind of board on the section's own board, by its title.</summary>
    private ILocator Kind(string title) => Page.Locator("article.board section.block").Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = title, Level = 2 }) });

    /// <summary>The side panel's list of boards.</summary>
    private ILocator SideList => Page.GetByRole(AriaRole.Navigation, new() { Name = WorkbenchStrings.Boards, Exact = true });

    /// <summary>The add-building board's flow, its bar and a screen's live page, by the screen's id.</summary>
    private ILocator Flow => Page.Locator(".board-flow");

    private ILocator FlowBar => Flow.Locator(".flow-bar");

    private IFrameLocator Screen(string screenId) => Page.FrameLocator($"iframe[data-screen-id='{screenId}']");

    private ILocator FlowButton(string name) => FlowBar.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    [TestMethod, EverySurface]
    public async Task TheBoards_Should_ListTheExamplesByKind(WorkbenchSurface surface)
    {
        await OpenBoards();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = WorkbenchStrings.Boards, Level = 1, Exact = true })).ToBeVisibleAsync();

        await Expect(Page.Locator("a.board-card")).ToHaveCountAsync(3);
        await Expect(Page.Locator("article.board section.block h2")).ToContainTextAsync([WorkbenchStrings.HomeCtaPromptLabel, WorkbenchStrings.BoardKindHowItWorks, WorkbenchStrings.BoardKindComparison]);
        await Expect(Page.Locator("article.board .prompts blockquote")).ToHaveTextAsync([WorkbenchStrings.BoardsPromptFlow, WorkbenchStrings.BoardsPromptCompare, WorkbenchStrings.BoardsPromptNumbers]);
        await Expect(Kind(WorkbenchStrings.BoardKindHowItWorks).Locator("a.board-card")).ToHaveCountAsync(2);
        await Expect(Kind(WorkbenchStrings.BoardKindHowItWorks).Locator("a.board-card[href='/boards/add-building']")).ToContainTextAsync(WorkbenchStrings.SampleFlowBuildings);
        await Expect(Kind(WorkbenchStrings.BoardKindComparison).Locator("a.board-card")).ToHaveAttributeAsync("href", "/boards/payments");

        await Press(Kind(WorkbenchStrings.BoardKindHowItWorks).Locator("a.board-card[href='/boards/deploy']"));

        await Expect(Page).ToHaveURLAsync(new Regex("/boards/deploy$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = WorkbenchStrings.DeployBoardTitle, Level = 1, Exact = true })).ToBeVisibleAsync();
    }

    [TestMethod, OnDesktops]
    public async Task AnOpenBoard_Should_ListTheOthersToSwitchTo(WorkbenchSurface surface)
    {
        await OpenBoards("/deploy");

        await Expect(SideList).ToContainTextAsync(WorkbenchStrings.BoardKindHowItWorks);
        await Expect(SideList).ToContainTextAsync(WorkbenchStrings.BoardKindComparison);
        await Expect(SideList.Locator("[aria-current='page']")).ToHaveAttributeAsync("href", "/boards/deploy");

        await Press(SideList.Locator("a[href='/boards/payments']"));

        await Expect(Page).ToHaveURLAsync(new Regex("/boards/payments$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Taking payments", Level = 1, Exact = true })).ToBeVisibleAsync();
        await Expect(SideList.Locator("[aria-current='page']")).ToHaveAttributeAsync("href", "/boards/payments");
    }

    [TestMethod, OnPhones]
    public async Task APhone_Should_OpenTheListOfBoardsOverTheBoardFromTheHeader(WorkbenchSurface surface)
    {
        await OpenBoards("/deploy");

        await Expect(SideList).ToBeHiddenAsync();

        await Press(Page.Locator("header.shell-header .side-toggle"));

        await Expect(SideList).ToBeVisibleAsync();

        await Press(SideList.GetByRole(AriaRole.Link, new() { Name = WorkbenchStrings.AllBoards, Exact = true }));

        await Expect(Page).ToHaveURLAsync(new Regex("/boards$"));
        await Expect(Page.Locator("a.board-card")).ToHaveCountAsync(3);
        await Expect(SideList).ToBeHiddenAsync();
    }

    [TestMethod, OnDesktops]
    public async Task TheSidePanel_Should_FoldAwayAndBeRememberedAcrossSections(WorkbenchSurface surface)
    {
        await OpenBoards("/deploy");

        await Expect(SideList).ToBeVisibleAsync();

        await Press(Page.Locator("header.shell-header .side-toggle"));

        await Expect(SideList).ToBeHiddenAsync();
        await Press(Page.Locator("header.shell-header .section-menu button").First);
        await Press(Page.GetByRole(AriaRole.Menu).Locator($"[role='menuitemradio'][href='{WorkbenchUrls.Progress}']"));

        await Expect(Page).ToHaveURLAsync(new Regex("/progress$"));

        var tasks = Page.GetByRole(AriaRole.Navigation, new() { Name = WorkbenchStrings.ProgressTasks, Exact = true });

        await Expect(tasks).ToBeHiddenAsync();

        await Press(Page.Locator("header.shell-header .side-toggle"));

        await Expect(tasks).ToBeVisibleAsync();
    }

    [TestMethod, OnDesktops]
    public async Task TheSidePanelsSearch_Should_FindABoardByWhatItSays(WorkbenchSurface surface)
    {
        await OpenBoards("/deploy");

        var search = SideList.GetByRole(AriaRole.Searchbox, new() { Name = WorkbenchStrings.SearchBoards });

        // Only the payments board names Mollie, and only in its content, not its title or summary.
        await search.FillAsync("Mollie");

        await Expect(SideList.Locator("a.item")).ToHaveCountAsync(1);
        await Expect(SideList.Locator("a.item")).ToHaveAttributeAsync("href", "/boards/payments");

        await search.FillAsync("no board says this");

        await Expect(SideList.Locator("a.item")).ToHaveCountAsync(0);
        await Expect(SideList.Locator(".no-match")).ToHaveTextAsync(WorkbenchStrings.NoSearchMatches);
    }

    [TestMethod, OnDesktops]
    public async Task AnUnknownBoard_Should_SaySoAndLeadBackToTheBoards(WorkbenchSurface surface)
    {
        await OpenBoards("/nothing-here");

        await Expect(Page.GetByText(WorkbenchStrings.BoardNotFound)).ToBeVisibleAsync();
        await Expect(Page.Locator(".missing a[href='/boards']")).ToBeVisibleAsync();
    }

    [TestMethod, EverySurface]
    public async Task TheAddBuildingBoard_Should_JoinItsScreensWithLabeledArrowsAndShowThemOneByOne(WorkbenchSurface surface)
    {
        await OpenBoards("/add-building");

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = WorkbenchStrings.SampleFlowBuildings, Level = 1, Exact = true })).ToBeVisibleAsync();
        await Flow.ScrollIntoViewIfNeededAsync();

        // The list, then the form and the map it leads to, each numbered under its note, with an arrow to each named by what the user does.
        await Expect(Flow.Locator(".screen-card")).ToHaveCountAsync(3);
        await Expect(Flow.Locator(".screen-number")).ToHaveTextAsync(["1", "2", "3"]);
        await Expect(Flow.Locator(".screen-note")).ToHaveTextAsync([WorkbenchStrings.SampleFlowBuildingsList, WorkbenchStrings.SampleFlowBuildingsForm, WorkbenchStrings.SampleFlowBuildingsMap]);
        await Expect(Flow.Locator(".bit-blow-edge")).ToHaveCountAsync(2);
        await Expect(Flow.Locator(".bit-blow-edge-label")).ToContainTextAsync([WorkbenchStrings.BuildingsAdd, WorkbenchStrings.BuildingsMapView]);

        // It opens on the first screen, usable, and steps to the next.
        await Expect(FlowBar.GetByRole(AriaRole.Status)).ToContainTextAsync(string.Format(CultureInfo.InvariantCulture, WorkbenchStrings.ScreenOf, 1, 3));
        await Expect(FlowButton(WorkbenchStrings.PreviousScreen)).ToBeDisabledAsync();
        await Expect(Flow.Locator(".screen-card.current iframe")).ToHaveAttributeAsync("data-screen-id", "list");

        await Press(FlowButton(WorkbenchStrings.NextScreen));

        await Expect(FlowBar.GetByRole(AriaRole.Status)).ToContainTextAsync(string.Format(CultureInfo.InvariantCulture, WorkbenchStrings.ScreenOf, 2, 3));
        await Expect(FlowBar.GetByRole(AriaRole.Status)).ToContainTextAsync(WorkbenchStrings.SampleFlowBuildingsForm);
        await Expect(Flow.Locator(".screen-card.current iframe")).ToHaveAttributeAsync("data-screen-id", "form");

        await Press(FlowButton(WorkbenchStrings.NextScreen));

        await Expect(Flow.Locator(".screen-card.current iframe")).ToHaveAttributeAsync("data-screen-id", "map");
        await Expect(FlowButton(WorkbenchStrings.NextScreen)).ToBeDisabledAsync();

        // Full screen, from the button next to the zoom ones, fills at least the window, and the button brings the flow back.
        await Press(Flow.GetByRole(AriaRole.Button, new() { Name = WorkbenchStrings.FullScreen, Exact = true }));

        await Expect(Flow).ToHaveCSSAsync("position", "fixed");

        // The browser's full screen comes a round trip after the press, and until then the fixed flow stays inside its block,
        // which the block's reveal leaves transformed; so it is waited for rather than measured at once.
        await Expect(Flow).ToHaveJSPropertyAsync("offsetWidth", Page.ViewportSize!.Width);
        await Expect(Flow).ToHaveJSPropertyAsync("offsetHeight", Page.ViewportSize!.Height);

        await Press(Flow.GetByRole(AriaRole.Button, new() { Name = WorkbenchStrings.ExitFullScreen, Exact = true }));

        await Expect(Flow).Not.ToHaveCSSAsync("position", "fixed");
        await Expect(Flow.GetByRole(AriaRole.Button, new() { Name = WorkbenchStrings.FullScreen, Exact = true })).ToBeVisibleAsync();
    }

    [TestMethod, EverySurface]
    public async Task TheAddBuildingBoardsScreens_Should_ShowThePageAsEachStepLeavesIt(WorkbenchSurface surface)
    {
        await OpenBoards("/add-building");

        await Flow.ScrollIntoViewIfNeededAsync();

        // The form and the map are the page's own state, which reaches each screen's preview.
        await Expect(Screen("list").GetByRole(AriaRole.Heading, new() { Name = WorkbenchStrings.BuildingsHeading })).ToBeVisibleAsync();
        await Expect(Screen("list").GetByText(WorkbenchStrings.BuildingsAddTitle, new() { Exact = true })).ToHaveCountAsync(0);
        await Expect(Screen("form").GetByText(WorkbenchStrings.BuildingsAddTitle, new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Screen("map").GetByRole(AriaRole.Button, new() { Name = WorkbenchStrings.BuildingsMapView, Exact = true })).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Screen("list").GetByRole(AriaRole.Button, new() { Name = WorkbenchStrings.BuildingsMapView, Exact = true })).Not.ToHaveAttributeAsync("aria-pressed", "true");
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
        await Expect(section.Locator(".boards-prompt blockquote")).ToHaveTextAsync([WorkbenchStrings.BoardsPromptFlow, WorkbenchStrings.BoardsPromptCompare, WorkbenchStrings.BoardsPromptNumbers]);
        await Expect(section.Locator("a.board-card").Nth(0)).ToHaveAttributeAsync("href", "/boards/payments");
        await Expect(section.Locator("a.board-card").Nth(1)).ToHaveAttributeAsync("href", "/boards/deploy");
        await Expect(section.Locator("a.board-card").Nth(2)).ToHaveAttributeAsync("href", "/boards/add-building");
        await Expect(section.Locator("a[href='/boards']")).ToBeVisibleAsync();
    }

    [TestMethod, OnDesktops]
    public async Task TheHeader_Should_LinkToTheBoards(WorkbenchSurface surface)
    {
        await Page.GotoAsync(Server.Address.ToString());
        await WaitUntilInteractive();

        await Press(Page.Locator("header.shell-header .section-menu button").First);

        await Expect(Page.GetByRole(AriaRole.Menu).Locator("[role='menuitemradio'][href='/boards']")).ToContainTextAsync(WorkbenchStrings.Boards);
    }
}
