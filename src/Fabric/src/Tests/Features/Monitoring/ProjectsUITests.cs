using Bit.Fabric.Tests.Features.Layout;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// The projects page as a visitor meets it (bitfabric-internal/specs/monitoring.md, The projects page): its menu item shows
/// to everyone, and signed out the page says what signing in gives rather than sending them to a sign-in page they didn't
/// ask for.
/// </summary>
[TestClass, TestCategory("UITest"), Retry(2)]
public partial class ProjectsUITests : AppPageTest
{
    [TestMethod]
    public async Task AVisitor_Should_FindProjectsInTheMenu_AndBeToldWhatSigningInGives()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await Page.GotoAsync(server.WebAppAddress.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForBlazorInteractive();

        await AppShellPage.NavPanelItem(Page, "Projects").ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.Projects}$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Know your app works before your customers tell you" })).ToBeVisibleAsync();

        var signIn = Page.GetByRole(AriaRole.Link, new() { Name = "Sign in" }).Last;
        await Expect(signIn).ToHaveAttributeAsync("href", new Regex("return-url=%2Fprojects"));
    }
}
