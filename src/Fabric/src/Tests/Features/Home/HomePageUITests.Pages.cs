using Bit.Fabric.Client.Core.Components.Pages.Home;

namespace Bit.Fabric.Tests.Features.Home;

public partial class HomePageUITests
{
    /// <summary>
    /// /pricing and /trust as a visitor reaches them (bitfabric-internal/specs/home-page.md, 2026-11-19): from the home page's
    /// questions band, the pricing page's calculator is the home page's own and answers the same way, its questions open, and
    /// the trust page shows who makes bit Fabric and what it sees. Both are in the navigation panel.
    /// </summary>
    [TestMethod]
    public async Task AVisitor_Should_FindWhatItCostsAndWhoMakesIt_FromTheHomePage()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();
        await Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.TourSkip }).ClickAsync();

        await Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.AppHomeSeePricing }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.Pricing}$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1, Name = AppStrings.PricingTitle })).ToBeVisibleAsync();
        await Page.WaitForBlazorInteractive();

        // The home page's calculator, as it is
        var total = Page.Locator(".ahc-total-number");
        await Expect(total).ToHaveTextAsync("€11");
        await Page.GetByRole(AriaRole.Switch, new() { Name = "OpenAI" }).ClickAsync();
        await Expect(total).ToHaveTextAsync("€8");

        // A question opens on its answer
        await Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.PricingQuestionBetaEnds }).ClickAsync();
        await Expect(Page.GetByText(AppStrings.PricingAnswerBetaEnds)).ToBeVisibleAsync();

        await Page.GoBackAsync();
        await Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.AppHomeSeeTrust }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.Trust}$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1, Name = AppStrings.TrustPageTitle })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Rowheader, new() { Name = AppStrings.TrustNeverCode })).ToBeVisibleAsync();
        await Expect(Page.Locator(".ahco-facts").GetByText(Bit.Fabric.Client.Core.Components.FabricSite.KvkNumber)).ToBeVisibleAsync();

        // Both are in the navigation panel, with the culture in their address
        foreach (var (name, url) in new[] { (AppStrings.Pricing, PageUrls.Pricing), (AppStrings.Trust, PageUrls.Trust) })
        {
            await Expect(Page.Locator($"a[href='/en-US{url}']").Filter(new() { HasText = name }).First).ToBeAttachedAsync();
        }
    }

    /// <summary>
    /// The comparisons as a visitor reaches them (bitfabric-internal/specs/home-page.md, "The comparison pages", 2026-11-19):
    /// from the home page's comparison band to the hub, from a card to a subject's page, from a fact's number to its source on
    /// the same page (the site's base address is its root, so a bare #anchor would have led to the home page), and back to the
    /// hub.
    /// </summary>
    [TestMethod]
    public async Task AVisitor_Should_ReachTheComparisons_AndFollowAFactToItsSource()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();
        await Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.TourSkip }).ClickAsync();

        await Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.AppHomeSeeCompare }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.Compare}$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1, Name = AppStrings.CompareHubTitle })).ToBeVisibleAsync();
        await Page.WaitForBlazorInteractive();

        await Page.GetByRole(AriaRole.Listitem).Filter(new() { HasText = "Lovable" }).GetByRole(AriaRole.Link, new() { Name = AppStrings.CompareSeeComparison }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.CompareLovable}$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1, Name = AppStrings.CompareLovableTitle })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Rowheader, new() { Name = AppStrings.CompareTopicDoesWell })).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Link, new() { Name = string.Format(AppStrings.CompareSourceNumber, 1), Exact = true }).First.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.CompareLovable}#src-lov-1$"));
        await Expect(Page.Locator("#src-lov-1")).ToBeInViewportAsync();

        await Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.CompareAllComparisons }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.Compare}$"));

        // It is in the navigation panel, after Pricing, with the culture in its address
        await Expect(Page.Locator($"a[href='/en-US{PageUrls.Compare}']").Filter(new() { HasText = AppStrings.Compare }).First).ToBeAttachedAsync();
    }

    /// <summary>
    /// /cli as a visitor reaches it: from the home page's How it works, the page that says what bit CLI does for them, then the
    /// lines that install it on its own, each copied with its button as it is to be pasted.
    /// </summary>
    [TestMethod]
    public async Task AVisitor_Should_FindWhatBitCliDoes_AndCopyTheLineThatInstallsIt()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();
        await Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.TourSkip }).ClickAsync();

        await Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.AppHomeSeeCli }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.Cli}$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1, Name = AppStrings.CliTitle })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Rowheader, new() { Name = "bit pipeline" })).ToBeVisibleAsync();
        await Page.WaitForBlazorInteractive();

        // The hero's Install bit CLI leads to the lines, on this page
        await Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.CliInstall }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.Cli}#install$"));

        await Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        var windows = Page.Locator(".cli-install").Filter(new() { HasText = AppStrings.NewProjectWindows });
        await windows.GetByRole(AriaRole.Button, new() { Name = AppStrings.NewProjectCopy }).ClickAsync();
        await Expect(windows.GetByRole(AriaRole.Button, new() { Name = AppStrings.NewProjectCopied })).ToBeVisibleAsync();
        Assert.AreEqual(AppCliGuide.WindowsInstallLine, await Page.EvaluateAsync<string>("() => navigator.clipboard.readText()"));
    }
}
