using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Bit.Fabric.Client.Core.Components.Pages.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
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
        static ProjectDto Project(string name, string address, ProjectHealth health = ProjectHealth.Operational, ProjectConnectionStatus connection = ProjectConnectionStatus.Connected)
            => new() { Id = Guid.NewGuid(), Name = name, Address = address, Health = health, ConnectionStatus = connection, ConnectionProblem = connection is ProjectConnectionStatus.ReconnectNeeded ? "The approval was withdrawn." : null };

        List<ProjectDto> projects =
        [
            Project("Corner shop", "https://shop.example", ProjectHealth.Down),
            Project("Team tasks", "https://tasks.example", connection: ProjectConnectionStatus.ReconnectNeeded),
            Project("Invoices", "https://invoices.example"),
            Project("Bookings", "https://bookings.example"),
            Project("Newsletter", "https://letters.example"),
            Project("Customers portal", "https://portal.example"),
            Project("Old landing", "https://landing.example", connection: ProjectConnectionStatus.Disconnected)
        ];

        var controller = A.Fake<IProjectsController>();
        A.CallTo(() => controller.GetProjects(A<string?>._, A<CancellationToken>._)).ReturnsLazily(() => projects);

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

        cut.Find(".search input").Input("customers");
        cut.WaitForAssertion(() => Assert.HasCount(1, cut.FindAll(".project-card")));
        Assert.Contains("Customers portal", cut.Find(".project-card").TextContent);

        cut.Find(".search input").Input("letters.example");
        cut.WaitForAssertion(() => Assert.Contains("Newsletter", cut.Find(".project-card").TextContent));

        cut.Find(".search input").Input("nothing like it");
        cut.WaitForAssertion(() => Assert.Contains("No project matches", cut.Markup));

        cut.FindAll("button").Single(button => button.TextContent.Contains("Show all projects")).Click();
        cut.WaitForAssertion(() => Assert.HasCount(7, cut.FindAll(".project-card")));
    }

    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;
}
