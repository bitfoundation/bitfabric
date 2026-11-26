using System.Text.RegularExpressions;
using System.Net.Http.Json;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Bit.Fabric.Tests.Features.Identity;
using Bit.Fabric.Tests.Features.Layout;

namespace Bit.Fabric.Tests.Features.Growth;

/// <summary>The growth page in a browser, as the global admin reaches it (bitfabric-internal/specs/growth.md).</summary>
[TestClass, TestCategory("UITest"), Retry(2)]
public class GrowthPageUITests : AppPageTest
{
    /// <summary>The seeded user is the global admin: the page is in its navigation, and opens with its figures.</summary>
    [TestMethod]
    public async Task TheGlobalAdmin_Should_OpenTheGrowthPage_FromTheNavigation()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        // A project made today, asked for as bit new asks, so the page has a figure whatever else the shared database holds.
        using (var api = server.CreateRawHttpClient())
        {
            using var trial = await api.PostAsJsonAsync("api/v1/License/Trial", new TrialLicenseRequestDto { Project = Guid.NewGuid(), Name = "Contoso", Commit = "0123456789abcdef0123456789abcdef01234567" }, TestContext.CancellationToken);
            trial.EnsureSuccessStatusCode();
        }

        await SignInPanelUtils.SignIn(Page, server, TestData.DefaultTestEmail, TestData.DefaultTestPassword, TestData.DefaultTestFullName);

        await AppShellPage.NavPanelGroup(Page, AppStrings.Management).ClickAsync();
        await AppShellPage.NavPanelItem(Page, AppStrings.GrowthTitle).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex($"{PageUrls.Growth}$"));
        await Expect(AppShellPage.PageHeading(Page)).ToHaveTextAsync(AppStrings.GrowthTitle);
        await Expect(Page.Locator(".hero-value")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Radio, new() { Name = AppStrings.GrowthYear, Exact = true })).ToBeVisibleAsync();
    }
}
