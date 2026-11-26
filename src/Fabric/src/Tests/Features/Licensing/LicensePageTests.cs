using System.Security.Claims;
using Bit.Fabric.Client.Core.Components.Pages.Licensing;
using Bit.Fabric.Shared.Features.Licensing;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Bit.Fabric.Shared.Features.Tenants;
using Bit.Fabric.Shared.Features.Tenants.Dtos;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bit.Fabric.Tests.Features.Licensing;

/// <summary>
/// The page the link bit CLI prints opens (bitfabric-internal/specs/product.md, A License belongs to a Team), rendered in
/// memory against stand-ins of the license and team apis: the team that holds the license, the prompt for the customer's
/// own AI first and the command second, and what bit Fabric knows of the project.
/// </summary>
[TestClass, TestCategory("UITest")]
public class LicensePageTests
{
    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;

    private static readonly Guid project = Guid.NewGuid();

    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private const string PrivacyLine = "Your project's name and whether it has a license are all we know about it. Its source code, prompts and data are never available to us.";

    /// <summary>Someone in one team isn't asked which: the license is that team's.</summary>
    [TestMethod]
    public async Task ASignedInAccountInOneTeam_Should_GetTheTeamsLicense_WithThePromptFirst()
    {
        var team = Guid.NewGuid();
        var licenses = A.Fake<ILicenseController>();
        A.CallTo(() => licenses.GetTeams(A<CancellationToken>._)).Returns([new LicenseTeamDto { Id = team, Title = "Contoso Labs" }]);
        A.CallTo(() => licenses.Issue(A<IssueLicenseRequestDto>._, A<CancellationToken>._)).Returns(Issued("Contoso Labs"));

        await using var server = await StartServer(licenses, A.Fake<ITenantController>());
        await using var ctx = server.CreateBunitContext();

        var cut = Render(ctx, signedIn: true, code: "abcdefghijklmnopqrstuvwx", commit: Commit);

        cut.WaitForAssertion(() => Assert.Contains("Contoso Labs", cut.Find(".team-one").TextContent));
        Assert.Contains("A project is one system with its own data: a till and its back office on one server are one project.", cut.Markup);
        Assert.Contains(Commit, cut.Markup);
        Assert.Contains(PrivacyLine, cut.Markup, "The page says what bit Fabric knows, before the license too.");
        Assert.IsEmpty(cut.FindAll(".team-picker"), "One team: nothing to pick.");

        cut.FindAll("button").Single(button => button.TextContent.Contains("Get the free license")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Paste this into your AI", cut.Markup));
        var boxes = cut.FindAll(".license-box code").Select(box => box.TextContent).ToArray();
        Assert.HasCount(2, boxes, "The prompt and the command; no box with the bare license alone.");
        Assert.Contains("run `bit license bfl1.payload.signature`", boxes[0], "The prompt first.");
        Assert.AreEqual("bit license bfl1.payload.signature", boxes[1].Trim(), "The command second.");
        Assert.Contains("If bit is waiting in your terminal, it picks the license up by itself and carries on.", cut.Markup);
        Assert.Contains("Free, for Contoso Labs", cut.Markup, "The license names the team.");
        Assert.Contains(PrivacyLine, cut.Markup);

        A.CallTo(() => licenses.Issue(A<IssueLicenseRequestDto>.That.Matches(request => request.Project == project && request.Name == "Contoso" && request.Commit == Commit
                                                                                    && request.Code == "abcdefghijklmnopqrstuvwx" && request.TeamId == team), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    /// <summary>Someone in several teams picks one; the one they joined first is picked for them.</summary>
    [TestMethod]
    public async Task ASignedInAccountInSeveralTeams_Should_HaveTheFirstPicked()
    {
        var first = Guid.NewGuid();
        var licenses = A.Fake<ILicenseController>();
        A.CallTo(() => licenses.GetTeams(A<CancellationToken>._)).Returns([new LicenseTeamDto { Id = first, Title = "Agency" }, new LicenseTeamDto { Id = Guid.NewGuid(), Title = "Client" }]);
        A.CallTo(() => licenses.Issue(A<IssueLicenseRequestDto>._, A<CancellationToken>._)).Returns(Issued("Agency"));

        await using var server = await StartServer(licenses, A.Fake<ITenantController>());
        await using var ctx = server.CreateBunitContext();

        var cut = Render(ctx, signedIn: true, code: null, commit: Commit);

        cut.WaitForAssertion(() => Assert.Contains("Agency", cut.Find(".team-picker").TextContent));

        cut.FindAll("button").Single(button => button.TextContent.Contains("Get the free license")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Paste this into your AI", cut.Markup));
        A.CallTo(() => licenses.Issue(A<IssueLicenseRequestDto>.That.Matches(request => request.TeamId == first), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    /// <summary>Someone in no team names one, made on the spot with no elevated session, and its license follows.</summary>
    [TestMethod]
    public async Task ASignedInAccountInNoTeam_Should_NameOne_AndGetItsLicense()
    {
        var made = Guid.NewGuid();
        var licenses = A.Fake<ILicenseController>();
        A.CallTo(() => licenses.GetTeams(A<CancellationToken>._)).Returns([]);
        A.CallTo(() => licenses.Issue(A<IssueLicenseRequestDto>._, A<CancellationToken>._)).Returns(Issued("Contoso Labs"));
        var tenants = A.Fake<ITenantController>();
        A.CallTo(() => tenants.Create(A<TenantDto>._, A<CancellationToken>._)).ReturnsLazily((TenantDto team, CancellationToken _) => new TenantDto { Id = made, Name = team.Name, Title = team.Title });

        await using var server = await StartServer(licenses, tenants);
        await using var ctx = server.CreateBunitContext();

        var cut = Render(ctx, signedIn: true, code: null, commit: Commit);

        cut.WaitForAssertion(() => Assert.HasCount(2, cut.FindAll(".team-form input")));
        var create = cut.FindAll("button").Single(button => button.TextContent.Contains("Create the team and get the license"));
        Assert.IsTrue(create.HasAttribute("disabled") || create.GetAttribute("aria-disabled") is "true", "Not before the team has a name.");

        cut.FindAll(".team-form input")[0].Input("Contoso Labs");
        cut.WaitForAssertion(() => Assert.AreEqual("contoso-labs", cut.FindAll(".team-form input")[1].GetAttribute("value"), "Its short name follows its name."));

        cut.FindAll("button").Single(button => button.TextContent.Contains("Create the team and get the license")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Paste this into your AI", cut.Markup));
        A.CallTo(() => tenants.Create(A<TenantDto>.That.Matches(team => team.Name == "contoso-labs" && team.Title == "Contoso Labs"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => licenses.Issue(A<IssueLicenseRequestDto>.That.Matches(request => request.TeamId == made), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [TestMethod]
    public async Task ASignedOutVisitor_Should_SignInFirst_AndComeBack()
    {
        await using var server = await StartServer(A.Fake<ILicenseController>(), A.Fake<ITenantController>());
        await using var ctx = server.CreateBunitContext();
        var navigationManager = ctx.Services.GetRequiredService<NavigationManager>();

        var cut = Render(ctx, signedIn: false, code: null, commit: Commit);

        cut.WaitForAssertion(() => Assert.StartsWith(navigationManager.ToAbsoluteUri($"{PageUrls.SignIn}?return-url=").ToString(), navigationManager.Uri));
        Assert.Contains($"license?project=Contoso&id={project:D}&commit={Commit}", Uri.UnescapeDataString(navigationManager.Uri), "It comes back to the same link.");
    }

    [TestMethod]
    public async Task ALinkWithoutAFirstCommit_Should_SayHowToGetOne()
    {
        await using var server = await StartServer(A.Fake<ILicenseController>(), A.Fake<ITenantController>());
        await using var ctx = server.CreateBunitContext();

        var cut = Render(ctx, signedIn: true, code: null, commit: null);

        cut.WaitForAssertion(() => Assert.Contains("Commit the project to git, then run bit license in its folder for a new link.", cut.Markup));
        Assert.IsEmpty(cut.FindAll("button").Where(button => button.TextContent.Contains("Get the free license")));
    }

    private static IssuedLicenseDto Issued(string holder) => new()
    {
        License = "bfl1.payload.signature",
        ProjectName = "Contoso",
        Plan = "free",
        Holder = holder,
        IssuedOn = DateTimeOffset.UtcNow,
        ExpiresOn = DateTimeOffset.UtcNow.AddDays(365),
        ProjectId = Guid.NewGuid()
    };

    private async Task<AppTestServer> StartServer(ILicenseController licenses, ITenantController tenants)
    {
        var server = new AppTestServer();

        await server.Build(configureTestWebAppServices: services =>
        {
            services.Replace(ServiceDescriptor.Singleton(licenses));
            services.Replace(ServiceDescriptor.Singleton(tenants));
        }).Start(TestContext.CancellationToken);

        return server;
    }

    /// <summary>The page at the link bit prints, as the query string gives it what it shows.</summary>
    private static IRenderedComponent<CascadingValue<Task<AuthenticationState>>> Render(BunitContext ctx, bool signedIn, string? code, string? commit)
    {
        ClaimsPrincipal user = signedIn ? new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Jane")], "Test")) : new(new ClaimsIdentity());

        var query = $"project=Contoso&id={project:D}{(commit is null ? "" : $"&commit={commit}")}{(code is null ? "" : $"&code={code}")}";
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo($"{PageUrls.License}?{query}");

        return ctx.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(p => p.Value, Task.FromResult(new AuthenticationState(user)))
            .AddChildContent<LicensePage>());
    }
}
