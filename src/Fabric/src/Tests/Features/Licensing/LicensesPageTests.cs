using System.Security.Claims;
using Bit.Fabric.Client.Core.Components.Pages.Licensing;
using Bit.Fabric.Shared.Features.Licensing;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bit.Fabric.Tests.Features.Licensing;

/// <summary>
/// The Licenses page (bitfabric-internal/specs/product.md, Seeing licenses), rendered in memory against a stand-in of the
/// license api: every license of each team the account is in, under its team, and the prompt that puts one into the
/// project again.
/// </summary>
[TestClass, TestCategory("UITest")]
public class LicensesPageTests
{
    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task EachTeamsLicenses_Should_BeUnderTheirTeam_AndCopyTheirPromptAgain()
    {
        var agency = Guid.NewGuid();
        var studio = Guid.NewGuid();
        LicenseDto orbit = License(agency, "Agency", "Orbit", "bfl1.orbit", LicenseState.InForce, expiresInDays: 300);
        LicenseDto shop = License(agency, "Agency", "Corner shop", "bfl1.shop", LicenseState.EndingSoon, expiresInDays: 12);
        LicenseDto invoices = License(studio, "Studio", "Invoices", "bfl1.invoices", LicenseState.InForce, expiresInDays: 200);

        var licenses = A.Fake<ILicenseController>();
        A.CallTo(() => licenses.GetLicenses(A<CancellationToken>._)).Returns([orbit, shop, invoices]);

        await using var server = await StartServer(licenses);
        await using var ctx = server.CreateBunitContext();

        var cut = Render(ctx);

        cut.WaitForAssertion(() => Assert.HasCount(3, cut.FindAll(".license")));
        Assert.Contains("Your teams' licenses", cut.Markup);

        var teams = cut.FindAll("section.team");
        Assert.HasCount(2, teams, "A section per team.");
        Assert.Contains("Agency", teams[0].QuerySelector(".team-title")!.TextContent);
        Assert.HasCount(2, teams[0].QuerySelectorAll(".license"));
        Assert.Contains("Studio", teams[1].QuerySelector(".team-title")!.TextContent);
        Assert.HasCount(1, teams[1].QuerySelectorAll(".license"));

        var shopCard = cut.FindAll(".license").Single(card => card.TextContent.Contains("Corner shop"));
        Assert.Contains("License ends", shopCard.TextContent, "One that ends within a month says so.");
        Assert.Contains("Jane", shopCard.TextContent, "Who got it.");
        Assert.Contains("0123456789ab", shopCard.QuerySelector(".mono")!.TextContent, "The first commit, shortened.");
        Assert.Contains("Licensed until", cut.FindAll(".license").Single(card => card.TextContent.Contains("Orbit")).TextContent);

        // The prompt again, for a teammate's clone of the project.
        cut.FindAll(".license").Single(card => card.TextContent.Contains("Orbit")).QuerySelector("button")!.Click();

        cut.WaitForAssertion(() => Assert.Contains("Copied", cut.FindAll(".license").Single(card => card.TextContent.Contains("Orbit")).TextContent));
        Assert.DoesNotContain("Copied", cut.FindAll(".license").Single(card => card.TextContent.Contains("Corner shop")).TextContent, "Only the one copied.");
        Assert.Contains(invocation => invocation.Arguments.OfType<string>().Any(text => text.Contains("run `bit license bfl1.orbit`") && text.Contains("Orbit project")),
                        ctx.JSInterop.Invocations, "The prompt for the customer's own AI, not the bare license.");
        Assert.Contains("Your project's name and whether it has a license are all we know about it.", cut.Markup);
    }

    [TestMethod]
    public async Task AnAccountWithoutLicenses_Should_SeeHowToGetOne()
    {
        var licenses = A.Fake<ILicenseController>();
        A.CallTo(() => licenses.GetLicenses(A<CancellationToken>._)).Returns([]);

        await using var server = await StartServer(licenses);
        await using var ctx = server.CreateBunitContext();

        var cut = Render(ctx);

        cut.WaitForAssertion(() => Assert.Contains("No license yet", cut.Markup));
        Assert.Contains("Run bit license in a project's folder for the link that gets it one, free.", cut.Markup);
        Assert.IsEmpty(cut.FindAll(".license"));
    }

    private static LicenseDto License(Guid team, string teamTitle, string project, string license, LicenseState state, int expiresInDays) => new()
    {
        Id = Guid.NewGuid(),
        TeamId = team,
        TeamTitle = teamTitle,
        ProjectId = Guid.NewGuid(),
        ProjectName = project,
        BitProjectId = Guid.NewGuid(),
        Plan = "free",
        FirstCommit = "0123456789abcdef0123456789abcdef01234567",
        IssuedOn = DateTimeOffset.UtcNow.AddDays(expiresInDays - 365),
        ExpiresOn = DateTimeOffset.UtcNow.AddDays(expiresInDays),
        State = state,
        IssuedBy = "Jane",
        License = license
    };

    private async Task<AppTestServer> StartServer(ILicenseController licenses)
    {
        var server = new AppTestServer();

        await server.Build(configureTestWebAppServices: services => services.Replace(ServiceDescriptor.Singleton(licenses))).Start(TestContext.CancellationToken);

        return server;
    }

    private static IRenderedComponent<CascadingValue<Task<AuthenticationState>>> Render(BunitContext ctx)
    {
        ClaimsPrincipal user = new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Jane")], "Test"));

        return ctx.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(p => p.Value, Task.FromResult(new AuthenticationState(user)))
            .AddChildContent<LicensesPage>());
    }
}
