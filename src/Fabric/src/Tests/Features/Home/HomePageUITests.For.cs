namespace Bit.Fabric.Tests.Features.Home;

public partial class HomePageUITests
{
    /// <summary>
    /// The AI agent pages as a visitor reaches them (bitfabric-internal/specs/home-page.md, "The AI agent pages", 2026-11-21):
    /// from /features' agents band to the hub, whose table names what each agent reads, from the table to an agent's page, from
    /// a fact's number to its source on the same page, on to another agent's page and back to the hub. The hub is in the
    /// navigation panel.
    /// </summary>
    [TestMethod]
    public async Task AVisitor_Should_FindTheirAgent_AndFollowAFactToItsSource()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(new Uri(server.WebAppAddress, $"en-US{PageUrls.Features}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        await Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.ForSeeEveryAgent }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.For}$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1, Name = AppStrings.ForHubTitle })).ToBeVisibleAsync();
        await Page.WaitForBlazorInteractive();

        // The table names what each agent reads, and leads to its page
        await Expect(Page.GetByRole(AriaRole.Rowheader, new() { Name = "Claude Code" })).ToBeVisibleAsync();
        await Page.GetByRole(AriaRole.Rowheader, new() { Name = "Claude Code" }).GetByRole(AriaRole.Link).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.ForClaudeCode}$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1, Name = string.Format(AppStrings.ForTitle, "Claude Code") })).ToBeVisibleAsync();
        await Expect(Page.GetByText("CLAUDE.md", new() { Exact = true }).First).ToBeVisibleAsync();
        await Expect(Page.GetByText("/ai-dlc", new() { Exact = true }).First).ToBeVisibleAsync();

        // A fact's number leads to its source, on this page
        await Page.GetByRole(AriaRole.Link, new() { Name = string.Format(AppStrings.CompareSourceNumber, 1), Exact = true }).First.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.ForClaudeCode}#src-cc-docs$"));
        await Expect(Page.Locator("#src-cc-docs")).ToBeInViewportAsync();

        // On to another agent, whose skills are called its own way, and back to the hub
        await Page.Locator("#ah-others").GetByRole(AriaRole.Link, new() { Name = "Codex" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.ForCodex}$"));
        await Expect(Page.GetByText("$ai-dlc", new() { Exact = true }).First).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.ForAllAgents }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.For}$"));

        // It is in the navigation panel, after Compare, with the culture in its address
        await Expect(Page.Locator($"a[href='/en-US{PageUrls.For}']").Filter(new() { HasText = AppStrings.ForHubName }).First).ToBeAttachedAsync();
    }
}
