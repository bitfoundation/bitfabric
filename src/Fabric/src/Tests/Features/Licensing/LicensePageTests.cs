using System.Security.Claims;
using Bit.Fabric.Client.Core.Components.Pages.Licensing;
using Bit.Fabric.Shared.Features.Licensing;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bit.Fabric.Tests.Features.Licensing;

/// <summary>The page the link bit CLI prints opens, rendered in memory against a stand-in of the license api.</summary>
[TestClass, TestCategory("UITest")]
public class LicensePageTests
{
    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;

    private static readonly Guid project = Guid.NewGuid();

    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    [TestMethod]
    public async Task ASignedInAccount_Should_GetTheLicense_WithTheLineThatSavesIt()
    {
        var licenses = A.Fake<ILicenseController>();
        A.CallTo(() => licenses.Issue(A<IssueLicenseRequestDto>._, A<CancellationToken>._)).Returns(new IssuedLicenseDto
        {
            License = "bfl1.payload.signature",
            ProjectName = "Contoso",
            Plan = "free",
            Holder = "Jane Doe",
            IssuedOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(365)
        });

        await using var server = await StartServer(licenses);
        await using var ctx = server.CreateBunitContext();

        var cut = Render(ctx, signedIn: true, code: "abcdefghijklmnopqrstuvwx", commit: Commit);

        cut.WaitForAssertion(() => Assert.Contains("A project is one system with its own data: a till and its back office on one server are one project.", cut.Markup));
        Assert.Contains(Commit, cut.Markup);

        cut.FindAll("button").Single(button => button.TextContent.Contains("Get the free license")).Click();

        cut.WaitForAssertion(() => Assert.Contains("bit license bfl1.payload.signature", cut.Markup));
        Assert.Contains("If bit is waiting in your terminal, it picks the license up by itself and carries on.", cut.Markup);
        Assert.Contains("Jane Doe", cut.Markup);

        A.CallTo(() => licenses.Issue(A<IssueLicenseRequestDto>.That.Matches(request => request.Project == project && request.Name == "Contoso" && request.Commit == Commit && request.Code == "abcdefghijklmnopqrstuvwx"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [TestMethod]
    public async Task ASignedOutVisitor_Should_SignInFirst_AndComeBack()
    {
        await using var server = await StartServer(A.Fake<ILicenseController>());
        await using var ctx = server.CreateBunitContext();
        var navigationManager = ctx.Services.GetRequiredService<NavigationManager>();

        var cut = Render(ctx, signedIn: false, code: null, commit: Commit);

        cut.WaitForAssertion(() => Assert.StartsWith(navigationManager.ToAbsoluteUri($"{PageUrls.SignIn}?return-url=").ToString(), navigationManager.Uri));
        Assert.Contains($"license?project=Contoso&id={project:D}&commit={Commit}", Uri.UnescapeDataString(navigationManager.Uri), "It comes back to the same link.");
    }

    [TestMethod]
    public async Task ALinkWithoutAFirstCommit_Should_SayHowToGetOne()
    {
        await using var server = await StartServer(A.Fake<ILicenseController>());
        await using var ctx = server.CreateBunitContext();

        var cut = Render(ctx, signedIn: true, code: null, commit: null);

        cut.WaitForAssertion(() => Assert.Contains("Commit the project to git, then run bit license in its folder for a new link.", cut.Markup));
        Assert.IsEmpty(cut.FindAll("button").Where(button => button.TextContent.Contains("Get the free license")));
    }

    private async Task<AppTestServer> StartServer(ILicenseController licenses)
    {
        var server = new AppTestServer();

        await server.Build(configureTestWebAppServices: services => services.Replace(ServiceDescriptor.Singleton(licenses))).Start(TestContext.CancellationToken);

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
