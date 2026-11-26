using Bit.Fabric.Client.Core.Components.Pages.Monitoring;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Features.Projects;
using Bit.Fabric.Shared.Features.Projects.Dtos;
using Bunit;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// Switching from one project's page straight to another's, like GitHub's repository switcher (bitfabric-internal/specs/
/// monitoring.md, A project's page switches to another project directly), rendered in memory.
/// </summary>
[TestClass, TestCategory("UITest")]
public class ProjectSwitcherTests
{
    [TestMethod]
    public async Task TheSwitcher_Should_ListTheTeamsProjects_FindOne_AndOpenItOnTheSameTab()
    {
        static ProjectDto Project(string name, params (string Environment, string Address)[] environments)
        {
            var id = Guid.NewGuid();
            return new()
            {
                Id = id,
                Name = name,
                License = new() { Kind = ProjectLicenseKind.Unknown },
                Monitorings = [.. environments.Select(environment => new MonitoringDto { Id = Guid.NewGuid(), ProjectId = id, Name = name, Environment = environment.Environment, Address = environment.Address })]
            };
        }

        var orbit = Project("Orbit", ("Production", "https://orbit.example"), ("Test", "https://test.orbit.example"));
        List<ProjectDto> projects = [Project("Corner shop", ("Production", "https://shop.example")), orbit, Project("Invoices")];

        var controller = A.Fake<IProjectsController>();
        A.CallTo(() => controller.GetProjects(A<string?>._, A<CancellationToken>._)).ReturnsLazily(() => projects);

        await using var server = new AppTestServer();
        await server.Build(configureTestWebAppServices: services => services.Replace(ServiceDescriptor.Singleton(controller))).Start(TestContext.CancellationToken);
        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<ProjectSwitcher>(parameters => parameters.Add(p => p.Current, orbit).Add(p => p.Tab, "incidents"));

        Assert.Contains("Orbit", cut.Find(".switcher-name").TextContent, "The project's name opens it.");

        cut.Find(".project-switcher button").Click();

        cut.WaitForAssertion(() => Assert.HasCount(3, cut.FindAll(".switcher-item")));
        Assert.Contains("Switch project", cut.Markup);
        var current = cut.FindAll(".switcher-item").Single(item => item.GetAttribute("aria-current") == "page");
        Assert.Contains("Orbit", current.TextContent, "The current one is checked.");
        Assert.Contains("Production and Test", current.TextContent);
        Assert.Contains("No environment yet", cut.FindAll(".switcher-item").Single(item => item.TextContent.Contains("Invoices")).TextContent);

        // A search by name, or by an environment's address.
        // Found and typed into in one go: the search box renders again as the list filters, which gives the input a new handler.
        await cut.InvokeAsync(() => cut.Find(".switcher-search input").Input("shop.example"));
        cut.WaitForAssertion(() => Assert.HasCount(1, cut.FindAll(".switcher-item")));

        var shop = cut.Find(".switcher-item");
        Assert.Contains("Corner shop", shop.TextContent);
        Assert.AreEqual($"{PageUrls.Projects}/{projects[0].Id}?tab=incidents", shop.GetAttribute("href"), "It opens on the same tab.");

        await cut.InvokeAsync(() => cut.Find(".switcher-search input").Input("nothing like it"));
        cut.WaitForAssertion(() => Assert.Contains("No project matches.", cut.Markup));

        A.CallTo(() => controller.GetProjects(A<string?>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;
}
