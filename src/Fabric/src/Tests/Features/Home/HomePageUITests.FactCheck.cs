namespace Bit.Fabric.Tests.Features.Home;

/// <summary>
/// The home page's fact-check band (bitfabric-internal/specs/home-page.md, "The AI evaluation"): folded, a line and a button;
/// open, the prompt a visitor copies into their own AI, with this site's addresses in it, and a button that opens the
/// assistant on the evaluation.
/// </summary>
public partial class HomePageUITests
{
    [TestMethod]
    public async Task AVisitor_Should_CopyThePromptForTheirOwnAI_OrAskTheAssistant()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();
        await Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.TourSkip }).ClickAsync();

        var band = Page.Locator(".afc");
        await Expect(band.GetByRole(AriaRole.Heading, new() { Name = AppStrings.AppHomeFactCheckTitle })).ToBeVisibleAsync();
        await Expect(band.Locator(".afc-how")).ToBeHiddenAsync();

        await band.GetByRole(AriaRole.Button, new() { Name = AppStrings.AppHomeFactCheckShowHow }).ClickAsync();

        var prompt = band.Locator(".afc-prompt-text");
        await Expect(prompt).ToContainTextAsync($"{server.ApiAppAddress}evaluate-mcp");
        await Expect(prompt).ToContainTextAsync($"{server.WebAppAddress.ToString().TrimEnd('/')}/llms-full.txt");

        // A common business fills in the prompt's blanks, and picking it again gives them back
        var pos = band.GetByRole(AriaRole.Button, new() { Name = AppStrings.AppHomeFactCheckPos });
        await pos.ClickAsync();
        await Expect(pos).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(prompt).ToContainTextAsync(AppStrings.AppHomeFactCheckPosBusiness);
        await Expect(prompt).ToContainTextAsync(AppStrings.AppHomeFactCheckPosNeeds);

        await pos.ClickAsync();
        await Expect(prompt).Not.ToContainTextAsync(AppStrings.AppHomeFactCheckPosNeeds);
        await Expect(prompt).ToContainTextAsync("[");

        await Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        await band.GetByRole(AriaRole.Button, new() { Name = AppStrings.Copy }).ClickAsync();
        await Expect(band.GetByRole(AriaRole.Button, new() { Name = AppStrings.Copied })).ToBeVisibleAsync();
        Assert.Contains("EvaluateBitFabric", await Page.EvaluateAsync<string>("() => navigator.clipboard.readText()"));

        // The assistant opens with the visitor's first message: an evaluation of bit Fabric for their business
        await band.GetByRole(AriaRole.Button, new() { Name = AppStrings.AppHomeFactCheckAskAssistant }).ClickAsync();
        await Expect(Page.GetByText(AppStrings.AppHomeFactCheckChatPrompt)).ToBeVisibleAsync(new() { Timeout = 30_000 });
    }
}
