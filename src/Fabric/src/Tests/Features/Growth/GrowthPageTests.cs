using System.Security.Claims;
using Bit.Fabric.Client.Core.Components.Pages.Management.Growth;
using Bit.Fabric.Shared.Features.Growth;
using Bit.Fabric.Shared.Features.Growth.Dtos;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bit.Fabric.Tests.Features.Growth;

/// <summary>
/// The growth page (bitfabric-internal/specs/growth.md) rendered in memory on a stand-in of its api: the figures it reads,
/// another period built from the same counts with no second request, the one line on automated runs, an empty start and
/// a failed load. GrowthPageUITests opens it in a browser.
/// </summary>
[TestClass, TestCategory("UITest")]
public class GrowthPageTests
{
    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;

    private static readonly DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

    [TestMethod]
    public async Task TheGrowthPage_Should_ShowTheFigures_AndBuildAnotherPeriodFromTheSameCounts()
    {
        var growth = A.Fake<IGrowthController>();
        A.CallTo(() => growth.GetData(A<string?>._, A<CancellationToken>._)).Returns(Sample());

        await using var server = await StartServer(growth);
        await using var ctx = server.CreateBunitContext();

        var cut = Render(ctx);

        cut.WaitForAssertion(() => Assert.AreEqual("17", cut.Find(".hero-value").TextContent.Trim()));
        Assert.Contains(AppStrings.GrowthWindowMonth, cut.Markup);
        Assert.Contains(AppStrings.GrowthRealOnly, cut.Markup, "The one line on automated runs.");
        Assert.Contains(AppStrings.GrowthLicensedProjects, cut.Markup);
        Assert.Contains(AppStrings.GrowthTeamsWithLicense, cut.Markup);
        Assert.Contains(AppStrings.GrowthInProduction, cut.Markup);
        Assert.Contains("40%", cut.Find(".flow").TextContent, "4 of the 10 Trials that ended in the month got a License.");
        Assert.Contains("10", cut.Find(".flow").TextContent);

        cut.FindAll("button").Single(button => button.TextContent.Trim() == AppStrings.GrowthAll).Click();

        cut.WaitForAssertion(() => Assert.Contains(string.Format(AppStrings.GrowthWindowAll, ""), cut.Markup));
        A.CallTo(() => growth.GetData(A<string?>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [TestMethod]
    public async Task TheGrowthPage_Should_SayThereIsNoProjectYet_WithNoPeriodToPick()
    {
        var growth = A.Fake<IGrowthController>();
        A.CallTo(() => growth.GetData(A<string?>._, A<CancellationToken>._)).Returns(new GrowthDataDto { Today = today });

        await using var server = await StartServer(growth);
        await using var ctx = server.CreateBunitContext();

        var cut = Render(ctx);

        cut.WaitForAssertion(() => Assert.Contains(AppStrings.GrowthEmptyTitle, cut.Markup));
        Assert.IsEmpty(cut.FindAll(".growth-periods"));
    }

    [TestMethod]
    public async Task TheGrowthPage_Should_SayItCouldntLoad_AndLoadAgainOnTryAgain()
    {
        var growth = A.Fake<IGrowthController>();
        A.CallTo(() => growth.GetData(A<string?>._, A<CancellationToken>._)).Throws(new HttpRequestException("down")).Once().Then.Returns(Sample());

        await using var server = await StartServer(growth);
        await using var ctx = server.CreateBunitContext();

        var cut = Render(ctx);

        cut.WaitForAssertion(() => Assert.Contains(AppStrings.GrowthError, cut.Markup));
        cut.FindAll("button").Single(button => button.TextContent.Trim() == AppStrings.GrowthRetry).Click();

        cut.WaitForAssertion(() => Assert.AreEqual("17", cut.Find(".hero-value").TextContent.Trim()));
    }

    /// <summary>Seventeen projects over two months: in the last 30 days 10 Trials ended, 4 of them licensed.</summary>
    private static GrowthDataDto Sample() => new()
    {
        Today = today,
        Days =
        [
            new() { Day = today.AddDays(-50), ProjectsMade = 5, ProjectsLicensed = 1, TeamsLicensed = 1, AccountsCreated = 3 },
            new() { Day = today.AddDays(-20), ProjectsMade = 10, ProjectsLicensed = 4, TeamsLicensed = 3, AccountsCreated = 6, InProduction = 1, TrialsEnded = 6, TrialsEndedLicensed = 2, TrialsConverted = 2 },
            new() { Day = today.AddDays(-5), TrialsEnded = 4, TrialsEndedLicensed = 2, TrialsConverted = 2 },
            new() { Day = today, ProjectsMade = 2, ProjectsLicensed = 1 },
            new() { Day = today.AddDays(10), TrialsEnded = 5, TrialsEndedLicensed = 1 }
        ],
        LicenseDelays = [new() { Day = today.AddDays(-20), Days = 16, Count = 4 }],
        TeamsByProjects = [3, 1, 0, 0],
        TeamProjects = 5,
        PeopleInLicensedTeams = 6
    };

    private async Task<AppTestServer> StartServer(IGrowthController growth)
    {
        var server = new AppTestServer();

        await server.Build(configureTestWebAppServices: services => services.Replace(ServiceDescriptor.Singleton(growth))).Start(TestContext.CancellationToken);

        return server;
    }

    private static IRenderedComponent<CascadingValue<Task<AuthenticationState>>> Render(BunitContext ctx)
    {
        ClaimsPrincipal admin = new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Admin"), new Claim(ClaimTypes.Role, AppRoles.GlobalAdmin)], "Test"));

        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(PageUrls.Growth);

        return ctx.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(p => p.Value, Task.FromResult(new AuthenticationState(admin)))
            .AddChildContent<GrowthPage>());
    }
}
