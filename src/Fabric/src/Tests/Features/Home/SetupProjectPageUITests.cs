namespace Bit.Fabric.Tests.Features.Home;

/// <summary>
/// The page for a project that already exists, as a teammate or the visitor's other computer goes through it
/// (bitfabric-internal/specs/getting-started.md, Setting up an existing project): the repository's address, then the line
/// that clones it; an address the page refuses; a project already on this computer; and the wizard's apps, which point
/// there instead of naming a bit CLI line.
/// </summary>
[TestClass, TestCategory("UITest"), Retry(2)]
public partial class SetupProjectPageUITests : AppPageTest
{
    [TestMethod]
    public async Task ATeammate_Should_GetTheLineThatClonesTheRepository_AndNoneForAnAddressAShellWouldRead()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(new Uri(server.WebAppAddress, PageUrls.SetupProject).ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = AppStrings.SetupProjectTitle, Exact = true }).Last).ToBeVisibleAsync();

        // No address, no line
        await Expect(Page.GetByText(AppStrings.SetupProjectWaitingForAddress)).ToBeVisibleAsync();
        await Expect(LineToCopy).ToBeHiddenAsync();

        var address = Page.GetByLabel(AppStrings.SetupProjectRepositoryLabel);

        // The way GitHub's Code button copies it, with a slash after it as a copy from the address bar has
        await address.FillAsync("https://github.com/acme/Bluebird/");
        await Expect(LineToCopy).ToContainTextAsync("setup https://github.com/acme/Bluebird --yes");
        // The repository it clones, named in the warning that the line runs its code
        await Expect(Page.Locator(".sp-trust")).ToContainTextAsync("https://github.com/acme/Bluebird");

        // Why the line is safe is folded behind the green shield until the visitor opens it
        var safe = Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.NewProjectSafeTitle });
        await Expect(safe).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(Page.GetByText(AppStrings.NewProjectSafeSigned)).ToBeHiddenAsync();
        await safe.ClickAsync();
        await Expect(Page.GetByText(AppStrings.NewProjectSafeSigned)).ToBeVisibleAsync();
        await Expect(Page.Locator(".np-safe-command")).ToHaveTextAsync("gh attestation verify <bit.dll> --repo bitfoundation/bitplatform");
        await Expect(Page).ToHaveURLAsync(new Regex(@"[?&]repo=https%3A%2F%2Fgithub\.com%2Facme%2FBluebird"));

        // An app to set up as well goes into the same line
        await Page.GetByRole(AriaRole.Switch, new() { Name = AppStrings.NewProjectAndroid }).ClickAsync();
        await Expect(LineToCopy).ToContainTextAsync("setup https://github.com/acme/Bluebird --platforms android --yes");

        // The address alone brings it back
        await Page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();
        await Expect(LineToCopy).ToContainTextAsync("setup https://github.com/acme/Bluebird --platforms android --yes");

        // A shell would run what follows the semicolon, so the page says why and gives no line
        await address.FillAsync("https://github.com/acme/Bluebird; rm -rf ~");
        // The field's own message; a live region announces the same text to screen readers
        await Expect(Page.GetByText(AppStrings.SetupProjectRepositoryNotValid).First).ToBeVisibleAsync();
        await Expect(LineToCopy).ToBeHiddenAsync();

        // A project already on this computer is set up where the line is pasted, with no address
        await PickChoice(AppStrings.SetupProjectOnThisComputer);
        await Expect(Page.GetByText(AppStrings.SetupProjectOnThisComputerHint)).ToBeVisibleAsync();
        await Expect(LineToCopy).ToContainTextAsync("setup --platforms android --yes");
        await Expect(LineToCopy).Not.ToContainTextAsync("github.com");
    }

    /// <summary>
    /// An app switched on in the wizard says it can be set up later from the page that does that, rather than with a bit
    /// CLI line the visitor doesn't know yet, and the page opens with that app picked.
    /// </summary>
    [TestMethod]
    public async Task AnAppLeftForLater_Should_LeadToTheSetupPage_WithThatAppPicked()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(new Uri(server.WebAppAddress, $"{PageUrls.NewProject}?devices=android&step=3").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        // It opens in a new tab, which its name says too, so the wizard keeps its answers. Like every link on a public page
        // it carries the page's culture, so a crawler that follows it meets no redirect (bitfabric-internal/specs/home-page.md,
        // 2026-11-19)
        var later = Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.NewProjectSetUpExistingLink });
        await Expect(later).ToHaveAttributeAsync("href", $"/en-US{PageUrls.SetupProject}?devices=android");
        await Expect(Page.GetByText("bit setup --platforms")).ToHaveCountAsync(0);

        // Where the link leads, as the new tab opens it
        await Page.GotoAsync(new Uri(server.WebAppAddress, (await later.GetAttributeAsync("href"))!).ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        await Expect(Page.GetByRole(AriaRole.Switch, new() { Name = AppStrings.NewProjectAndroid })).ToBeCheckedAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.NewProjectSetUpExistingLink })).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Picks a choice by its label, the way a visitor does: the choice's label lies over its radio, so clicking the radio
    /// itself is refused as "intercepts pointer events".
    /// </summary>
    private async Task PickChoice(string name)
    {
        var radio = Page.GetByRole(AriaRole.Radio, new() { Name = name, Exact = true });
        await Page.Locator($"label[for='{await radio.GetAttributeAsync("id")}']").ClickAsync();
        await Expect(radio).ToBeCheckedAsync();
    }

    /// <summary>The line the visitor copies, whose start depends on the computer the browser runs on, so only bit's part is checked.</summary>
    private ILocator LineToCopy => Page.Locator(".np-howto code.np-line-text").First;
}
