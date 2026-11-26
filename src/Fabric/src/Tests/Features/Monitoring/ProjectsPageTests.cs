using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Bit.Fabric.Client.Core.Components.Pages.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Bit.Fabric.Shared.Features.Projects;
using Bit.Fabric.Shared.Features.Projects.Dtos;
using Bunit;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// The projects page as the approved design has it (bitfabric-internal/specs/monitoring.md, The projects page), rendered in
/// memory: an agency's many cards, only the ones that need it, a search by name or address, and Reconnect on the cards that
/// can't be read.
/// </summary>
[TestClass, TestCategory("UITest")]
public class ProjectsPageTests
{
    [TestMethod]
    public async Task AnAgency_Should_SeeWhatNeedsIt_FindAProjectByNameOrAddress_AndReconnectFromTheCard()
    {
        static MonitoringDto Project(string name, string address, ProjectHealth health = ProjectHealth.Operational, ProjectConnectionStatus connection = ProjectConnectionStatus.Connected)
            => new() { Id = Guid.NewGuid(), Name = name, Address = address, Health = health, ConnectionStatus = connection, ConnectionProblem = connection is ProjectConnectionStatus.ReconnectNeeded ? "The approval was withdrawn." : null };

        List<MonitoringDto> projects =
        [
            Project("Corner shop", "https://shop.example", ProjectHealth.Down),
            Project("Team tasks", "https://tasks.example", connection: ProjectConnectionStatus.ReconnectNeeded),
            Project("Invoices", "https://invoices.example"),
            Project("Bookings", "https://bookings.example"),
            Project("Newsletter", "https://letters.example"),
            Project("Customers portal", "https://portal.example"),
            Project("Old landing", "https://landing.example", connection: ProjectConnectionStatus.Disconnected)
        ];

        // Each its own project, as the server gathers environments into their projects.
        var controller = A.Fake<IProjectsController>();
        A.CallTo(() => controller.GetProjects(A<string?>._, A<CancellationToken>._)).ReturnsLazily(() => [.. projects.Select(OfItsOwn)]);

        await using var server = new AppTestServer();
        await server.Build(configureTestWebAppServices: services => services.Replace(ServiceDescriptor.Singleton(controller))).Start(TestContext.CancellationToken);
        await using var ctx = server.CreateBunitContext();

        // Signed in, in a team. The context already shares the server's services, so the user is cascaded rather than registered.
        ClaimsPrincipal user = new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Ada"), new Claim(AppClaimTypes.TENANT_ID, Guid.NewGuid().ToString())], "Test"));

        var cut = ctx.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(p => p.Value, Task.FromResult(new AuthenticationState(user)))
            .AddChildContent<ProjectsPage>());

        cut.WaitForAssertion(() => Assert.HasCount(7, cut.FindAll(".project-card")));
        Assert.Contains("2 of 7 projects need you", cut.Markup);

        // A card that can't be read offers Reconnect, which looks for the app's address again.
        var reconnects = cut.FindAll(".project-card .reconnect a");
        Assert.HasCount(2, reconnects, "Needing reconnecting and disconnected.");
        Assert.AreEqual($"{PageUrls.AddProject}?address=tasks.example", reconnects[0].GetAttribute("href"));

        // Only what needs you.
        cut.FindAll(".filters button[aria-pressed]").Single(tag => tag.TextContent.Contains("Needs attention")).Click();
        cut.WaitForAssertion(() => Assert.HasCount(2, cut.FindAll(".project-card")));
        Assert.Contains("Corner shop", cut.Markup);
        Assert.Contains("Team tasks", cut.Markup);

        // Everything again, then a search by name, by address, and one that finds nothing.
        cut.FindAll(".filters button[aria-pressed]").Single(tag => tag.TextContent.Contains("All")).Click();
        cut.WaitForAssertion(() => Assert.HasCount(7, cut.FindAll(".project-card")));

        // Found and typed into in one go: the search box renders again once its debounce ends, which can replace its handler in between.
        await cut.InvokeAsync(() => cut.Find(".search input").Input("customers"));
        cut.WaitForAssertion(() => Assert.HasCount(1, cut.FindAll(".project-card")));
        Assert.Contains("Customers portal", cut.Find(".project-card").TextContent);

        await cut.InvokeAsync(() => cut.Find(".search input").Input("letters.example"));
        cut.WaitForAssertion(() => Assert.Contains("Newsletter", cut.Find(".project-card").TextContent));

        await cut.InvokeAsync(() => cut.Find(".search input").Input("nothing like it"));
        cut.WaitForAssertion(() => Assert.Contains("No project matches", cut.Markup));

        cut.FindAll("button").Single(button => button.TextContent.Contains("Show all projects")).Click();
        cut.WaitForAssertion(() => Assert.HasCount(7, cut.FindAll(".project-card")));
    }

    /// <summary>
    /// The Licenses tab (bitfabric-internal/specs/monitoring.md, A card per project, its environments on it, and a Licenses
    /// tab): each project's license, the ones that need something first, and the way to every license of the user's teams.
    /// </summary>
    [TestMethod]
    public async Task TheLicensesTab_Should_ShowEachProjectsLicense_TheEndedFirst()
    {
        var now = DateTimeOffset.UtcNow;
        List<ProjectDto> projects =
        [
            WithLicense(OfItsOwn(new() { Id = Guid.NewGuid(), Name = "Bookings", Environment = "Production" }), new() { Kind = ProjectLicenseKind.Licensed, State = LicenseState.InForce, Until = now.AddDays(200) }),
            WithLicense(OfItsOwn(new() { Id = Guid.NewGuid(), Name = "Corner shop", Environment = "Production" }), new() { Kind = ProjectLicenseKind.LicenseEnded, State = LicenseState.Ended, Until = now.AddDays(-40) }),
            WithLicense(OfItsOwn(new() { Id = Guid.NewGuid(), Name = "Invoices", Environment = "Production" }), new() { Kind = ProjectLicenseKind.Trial, Until = now.AddDays(12) }),
            WithLicense(OfItsOwn(new() { Id = Guid.NewGuid(), Name = "Newsletter", Environment = "Production" }), new() { Kind = ProjectLicenseKind.Unknown })
        ];

        var controller = A.Fake<IProjectsController>();
        A.CallTo(() => controller.GetProjects(A<string?>._, A<CancellationToken>._)).ReturnsLazily(() => projects);

        await using var server = new AppTestServer();
        await server.Build(configureTestWebAppServices: services => services.Replace(ServiceDescriptor.Singleton(controller))).Start(TestContext.CancellationToken);
        await using var ctx = server.CreateBunitContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo($"{PageUrls.Projects}?tab=licenses");

        ClaimsPrincipal user = new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Ada"), new Claim(AppClaimTypes.TENANT_ID, Guid.NewGuid().ToString())], "Test"));

        var cut = ctx.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(p => p.Value, Task.FromResult(new AuthenticationState(user)))
            .AddChildContent<ProjectsPage>());

        cut.WaitForAssertion(() => Assert.HasCount(4, cut.FindAll(".licenses-tab .row")));
        var rows = cut.FindAll(".licenses-tab .row").Select(row => row.TextContent).ToArray();
        Assert.Contains("Corner shop", rows[0], "An ended license first.");
        Assert.Contains("License ended", rows[0]);
        Assert.Contains("Trial until", rows[1]);
        Assert.Contains("Licensed until", rows[2]);
        Assert.Contains("License not known yet", rows[3]);
        Assert.AreEqual(PageUrls.Licenses, cut.FindAll(".licenses-tab .toolbar a").Single().GetAttribute("href"));
    }

    private static ProjectDto OfItsOwn(MonitoringDto environment)
    {
        environment.ProjectId = environment.Id;
        environment.ProjectName = environment.Name;
        environment.Environment ??= "Production";

        return new() { Id = environment.Id, Name = environment.Name, Monitorings = [environment], License = new() { Kind = ProjectLicenseKind.Unknown } };
    }

    private static ProjectDto WithLicense(ProjectDto project, ProjectLicenseDto license)
    {
        project.License = license;
        return project;
    }

    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;
}
