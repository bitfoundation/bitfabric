namespace Bit.Fabric.Tests.Features.Home;

/// <summary>
/// bitfabric.co's way in, as a visitor goes through it: the home page and a feature's film, then "Get started" to the new
/// project page, its answers and the one line they make, kept in the page's address so a reload or a shared link opens it
/// the same way (bitfabric-internal/specs/home-page.md, bitfabric-internal/specs/getting-started.md).
/// </summary>
[TestClass, TestCategory("UITest"), Retry(2)]
public partial class HomePageUITests : AppPageTest
{
    /// <summary>These are a first visit's, the onboarding included.</summary>
    protected override bool SkipsHomeOnboarding => false;

    [TestMethod]
    public async Task AVisitor_Should_WatchAFeaturesFilm_ThenGetTheLineThatCreatesTheirProject()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = AppStrings.AppHomeBuildWithAIOwnEverything })).ToBeVisibleAsync();

        // A first visit is offered the onboarding; this visitor skips it
        await Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.TourSkip }).ClickAsync();
        await Expect(TourCard(AppStrings.AppHomeSeeWhatSInItForYou)).ToBeHiddenAsync();

        // A feature's info button plays its film, which comes from the films' own library
        await Page.Locator(".ah-card-info").First.ClickAsync();
        var film = Page.Locator(".ah-film");
        await Expect(film).ToBeVisibleAsync();
        await film.GetByRole(AriaRole.Button, new() { Name = AppStrings.Close }).ClickAsync();
        await Expect(film).ToBeHiddenAsync();

        // The running cost follows what's on the bill
        var total = Page.Locator(".ahc-total-number");
        await Expect(total).ToHaveTextAsync("€11");
        await Page.GetByRole(AriaRole.Switch, new() { Name = "OpenAI" }).ClickAsync();
        await Expect(total).ToHaveTextAsync("€8");

        // The server can't come off the bill
        var linuxVm = Page.GetByRole(AriaRole.Switch, new() { Name = "Linux VM" });
        await linuxVm.ClickAsync();
        await Expect(linuxVm).ToBeCheckedAsync();
        await Expect(total).ToHaveTextAsync("€8");

        // A band's title copies the address that leads to it
        await Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        await Page.Locator("#ah-cost-title a").ClickAsync();
        await Expect(Page.Locator("#ah-cost-title").GetByText(AppStrings.AppHomeLinkCopied)).ToBeVisibleAsync();
        Assert.EndsWith("#cost", await Page.EvaluateAsync<string>("() => navigator.clipboard.readText()"));

        // Everything else every project comes with is on /features, where the three pictures replace their plain lists once
        // the page is interactive; the lists stay for screen readers (bitfabric-internal/specs/home-page.md, 2026-11-18)
        await Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.AppHomeSeeEverythingAProjectStartsWith }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.Features}$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1, Name = AppStrings.FeaturesTitle })).ToBeVisibleAsync();
        await Expect(Page.Locator("#ah-values")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".ahp")).ToHaveCountAsync(3);
        await Expect(Page.Locator(".ah-steps.is-behind")).ToHaveCountAsync(3);

        await Page.Locator(".ah-actions").GetByRole(AriaRole.Link, new() { Name = AppStrings.AppHomeGetStarted }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.NewProject}$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = AppStrings.NewProjectSetUpTitle, Exact = true }).Last).ToBeVisibleAsync();

        // A name with a space can't name a C# project: the page says so, offers a valid one and goes no further without it
        await Page.Locator(".np-split-main input").First.FillAsync("Coffee Shop");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.GetByText(AppStrings.NewProjectNameNotValid)).ToBeVisibleAsync();
        await Page.Locator(".np-stepper-step").Last.ClickAsync();
        await Expect(LineToCopy).ToBeHiddenAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = string.Format(AppStrings.NewProjectNameUseSuggestion, "CoffeeShop") }).ClickAsync();
        await Expect(Page.GetByText(AppStrings.NewProjectNameNotValid)).ToBeHiddenAsync();
        await Page.Locator(".np-stepper-step").Last.ClickAsync();

        await Expect(LineToCopy).ToContainTextAsync("new CoffeeShop");
        await Expect(Page).ToHaveURLAsync(new Regex(@"[?&]name=CoffeeShop.*[?&]step=4"));

        // The address alone brings the answers back
        await Page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();
        await Expect(LineToCopy).ToContainTextAsync("new CoffeeShop");

        // On Windows the line starts PowerShell by name, with the installer and bit new inside its quotes, so it runs as it
        // is in Command Prompt too, where the visitor may paste it rather than in PowerShell
        // Mac first, then Windows: the page starts on the computer the browser runs on, so whatever computer runs the test,
        // at least one of them is a real click
        await Page.Locator(".np-stepper-step").Nth(3).ClickAsync();
        await PickComputer(AppStrings.NewProjectMac);
        await Page.Locator(".np-stepper-step").Last.ClickAsync();
        await Expect(LineToCopy).ToHaveTextAsync(new Regex(@"^curl -fsSL https://bitfabric\.co/install\.sh \| bash -s -- new CoffeeShop .*--yes$"));

        await Page.Locator(".np-stepper-step").Nth(3).ClickAsync();
        await PickComputer(AppStrings.NewProjectWindows);
        await Page.Locator(".np-stepper-step").Last.ClickAsync();
        await Expect(LineToCopy).ToHaveTextAsync(new Regex(@"^powershell -c ""irm https://bitfabric\.co/install\.ps1 \| iex; bit new CoffeeShop [^""]*--yes""$"));
    }

    /// <summary>
    /// Picks a computer on the devices step the way a visitor does, by its label: the choice's label lies over its radio, so
    /// clicking the radio itself is refused as "intercepts pointer events".
    /// </summary>
    private async Task PickComputer(string name)
    {
        var radio = Page.GetByRole(AriaRole.Radio, new() { Name = name, Exact = true });
        await Page.Locator($"label[for='{await radio.GetAttributeAsync("id")}']").ClickAsync();
        await Expect(radio).ToBeCheckedAsync();
    }

    /// <summary>
    /// The line the visitor copies. Its words depend on the computer the wizard takes from the browser: Windows runs the
    /// installer then "bit new CoffeeShop", Linux and macOS hand "new CoffeeShop" to the installer's script, so only that is
    /// the same wherever the test runs (CI runs on Linux).
    /// </summary>
    private ILocator LineToCopy => Page.Locator(".np-howto code.np-line-text").First;

    /// <summary>
    /// The onboarding, on a first visit only: why bit Fabric is worth a look, a film playing, the wizard and
    /// back to the home page. The browser keeps that it was seen, so the next visit doesn't show it again.
    /// </summary>
    [TestMethod]
    public async Task AFirstVisitor_Should_BeShownAround_OnlyOnce()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        // The tour card's own button: the wizard has a Next of its own
        var next = Page.Locator(".guide-next");

        await Expect(TourCard(AppStrings.AppHomeSeeWhatSInItForYou)).ToBeVisibleAsync();
        await next.ClickAsync();

        // The films step plays the website's film
        await Expect(TourCard(AppStrings.AppHomeEveryItemHasAShortVideoLikeThisOne)).ToBeVisibleAsync();
        await Expect(Page.Locator(".ah-film")).ToBeVisibleAsync();

        // The page is left scrolled down, where the app's shell puts it back on the way home
        await Page.EvaluateAsync("() => document.querySelector('.bit-ash-main')?.scrollTo(0, 2000)");
        await next.ClickAsync();

        // Then the wizard, and the film is closed
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.NewProject}$"));
        await Expect(TourCard(AppStrings.AppHomeThenBuildYourProjectEasily)).ToBeVisibleAsync();

        // Back to the films step plays the film again, on the home page
        await Page.Locator(".guide-previous").ClickAsync();
        await Expect(TourCard(AppStrings.AppHomeEveryItemHasAShortVideoLikeThisOne)).ToBeVisibleAsync();
        await Expect(Page.Locator(".ah-film")).ToBeVisibleAsync();
        await next.ClickAsync();
        await Expect(TourCard(AppStrings.AppHomeThenBuildYourProjectEasily)).ToBeVisibleAsync();
        await next.ClickAsync();

        // And back on the home page
        await Expect(TourCard(AppStrings.AppHomeReadyWhenYouAre)).ToBeVisibleAsync();
        await Expect(Page.Locator(".ah-actions")).ToBeInViewportAsync();
        await Expect(Page.Locator(".ah-film")).ToBeHiddenAsync();
        await Expect(next).ToHaveTextAsync(AppStrings.TourDone);
        await next.ClickAsync();
        await Expect(TourCard(AppStrings.AppHomeReadyWhenYouAre)).ToBeHiddenAsync();

        // The next visit gets no tour
        await Page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = AppStrings.AppHomeBuildWithAIOwnEverything })).ToBeVisibleAsync();
        await Expect(TourCard(AppStrings.AppHomeSeeWhatSInItForYou)).ToBeHiddenAsync();
        Assert.IsTrue(await Page.EvaluateAsync<bool>("() => (localStorage.getItem('bit-bonboarding:home-onboarding') ?? '').includes('true')"));
    }

    /// <summary>
    /// A link to one of the page's bands leaves a first visitor on that band: the tour greets them where they are, instead
    /// of taking them to the top of the page to point at the hero (bitfabric-internal/specs/home-page.md, 2026-11-09).
    /// </summary>
    [TestMethod]
    public async Task AFirstVisitor_Should_StayOnTheBandTheirLinkOpened()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(new Uri(server.WebAppAddress, "#cost").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        await Expect(TourCard(AppStrings.AppHomeSeeWhatSInItForYou)).ToBeVisibleAsync();

        // The jump came a moment after the card showed, so this looks once there has been time for it
        await Page.WaitForTimeoutAsync(1500);
        await Expect(Page.Locator("#ah-cost")).ToBeInViewportAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.TourSkip }).ClickAsync();
        await Expect(TourCard(AppStrings.AppHomeSeeWhatSInItForYou)).ToBeHiddenAsync();
        await Expect(Page.Locator("#ah-cost")).ToBeInViewportAsync();
    }

    /// <summary>
    /// On a phone the three pictures are taller than the screen, so a visitor reading down the page has their finger on one
    /// most of the time: a swipe that starts on a picture scrolls the page as one that starts on the text does
    /// (bitfabric-internal/specs/home-page.md, 2026-11-11). Only chromium takes a finger's swipe from a test, through its
    /// DevTools protocol.
    /// </summary>
    [TestMethod]
    public async Task OnAPhone_AVisitor_Should_ScrollThePage_WithTheirFingerOnAPicture()
    {
        if (Microsoft.Playwright.TestAdapter.PlaywrightSettingsProvider.BrowserName is not Microsoft.Playwright.BrowserType.Chromium)
            Assert.Inconclusive("Only chromium takes a finger's swipe from a test.");

        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.SetViewportSizeAsync(390, 844);
        var devTools = await Context.NewCDPSessionAsync(Page);
        await devTools.SendAsync("Emulation.setTouchEmulationEnabled", new() { ["enabled"] = true, ["maxTouchPoints"] = 5 });

        // The pictures are on /features, which has no onboarding (bitfabric-internal/specs/home-page.md, 2026-11-18)
        await Page.GotoAsync(new Uri(server.WebAppAddress, PageUrls.Features.TrimStart('/')).ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        var pictures = Page.Locator(".ahp");
        await Expect(pictures).ToHaveCountAsync(3);

        for (var i = 0; i < 3; i++)
        {
            var picture = pictures.Nth(i);
            await picture.EvaluateAsync("p => p.scrollIntoView({ block: 'center', behavior: 'instant' })");

            // The middle of the screen, which the picture covers
            const int x = 195, y = 422;
            Assert.IsTrue(await picture.EvaluateAsync<bool>($"p => p.contains(document.elementFromPoint({x}, {y}))"), $"Picture {i + 1} is under the finger.");

            var before = await ScrollTop();

            await devTools.SendAsync("Input.dispatchTouchEvent", new() { ["type"] = "touchStart", ["touchPoints"] = new[] { new { x, y } } });
            for (var step = 1; step <= 10; step++)
            {
                await devTools.SendAsync("Input.dispatchTouchEvent", new() { ["type"] = "touchMove", ["touchPoints"] = new[] { new { x, y = y - step * 20 } } });
                await Page.WaitForTimeoutAsync(16);
            }
            await devTools.SendAsync("Input.dispatchTouchEvent", new() { ["type"] = "touchEnd", ["touchPoints"] = Array.Empty<object>() });
            await Page.WaitForTimeoutAsync(800);

            // A 200 pixel swipe, less what the browser takes to tell a swipe from a tap
            Assert.IsGreaterThan(100, await ScrollTop() - before, $"A swipe on picture {i + 1} scrolls the page.");
        }

        // The app's shell scrolls its main part, not the document
        Task<double> ScrollTop() => Page.EvaluateAsync<double>("() => document.querySelector('.bit-ash-main').scrollTop");
    }

    /// <summary>The onboarding's card, by its title: the card's heading, not the screen reader's announcement of it.</summary>
    private ILocator TourCard(string title) => Page.GetByRole(AriaRole.Heading, new() { Name = title, Exact = true });
}
