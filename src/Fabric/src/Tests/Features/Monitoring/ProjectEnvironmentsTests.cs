using Bit.Fabric.Client.Core.Components.Pages.Monitoring;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Features.Projects;
using Bit.Fabric.Shared.Features.Projects.Dtos;
using Bunit;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// A project's page, its environments and its people (bitfabric-internal/specs/monitoring.md, Projects, environments and
/// their people), rendered in memory against stand-ins of the projects and monitorings apis.
/// </summary>
[TestClass, TestCategory("UITest")]
public class ProjectEnvironmentsTests
{
    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task AProjectsPage_Should_ShowEachEnvironment_TheOpenOneCurrent_AndAddAnother()
    {
        var projectId = Guid.NewGuid();
        MonitoringDto production = new() { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Orbit", Environment = "Production", Address = "https://orbit.example", Health = ProjectHealth.Operational, ConnectionStatus = ProjectConnectionStatus.Connected };
        MonitoringDto test = new() { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Orbit (Test)", Environment = "Test", Address = "https://test.orbit.example", Health = ProjectHealth.Degraded, ConnectionStatus = ProjectConnectionStatus.Connected };
        ProjectDto orbit = new()
        {
            Id = projectId,
            Name = "Orbit",
            License = new() { Kind = ProjectLicenseKind.Licensed, State = LicenseState.InForce, Until = DateTimeOffset.UtcNow.AddDays(300) },
            Monitorings = [production, test]
        };

        var projects = A.Fake<IProjectsController>();
        A.CallTo(() => projects.GetProject(projectId, A<string?>._, A<CancellationToken>._)).Returns(orbit);
        var monitorings = A.Fake<IMonitoringsController>();
        A.CallTo(() => monitorings.GetMonitoring(test.Id, A<CancellationToken>._)).Returns(new MonitoringDetailsDto { Monitoring = test });

        await using var server = await StartServer(projects, monitorings);
        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<ProjectPage>(parameters => parameters.Add(p => p.ProjectId, projectId).Add(p => p.MonitoringId, test.Id));

        cut.WaitForAssertion(() => Assert.HasCount(2, cut.FindAll("nav.environments .environment")));
        var environments = cut.FindAll("nav.environments .environment");
        Assert.Contains("Production", environments[0].TextContent);
        Assert.AreEqual(PageUrls.ProjectEnvironment(projectId, production.Id), environments[0].GetAttribute("href"), "Another environment is a link to it.");
        Assert.IsNull(environments[0].GetAttribute("aria-current"));
        Assert.Contains("Test", environments[1].TextContent);
        Assert.AreEqual("page", environments[1].GetAttribute("aria-current"), "The open one is current.");
        Assert.IsNotNull(environments[1].QuerySelector(".environment-dot.tone-degraded"), "Each with its health.");
        Assert.Contains(link => link.TextContent.Contains("Add your server") && link.GetAttribute("href") == $"{PageUrls.AddProject}?project={projectId}",
                        cut.FindAll("nav.environments a"), "A new environment joins this project.");
        Assert.Contains("Licensed until", cut.Markup, "The project's license beside its name.");
        Assert.Contains("test.orbit.example", cut.Find(".head-meta").TextContent, "The open environment's address.");

        A.CallTo(() => monitorings.GetMonitoring(test.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => monitorings.GetMonitoring(production.Id, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [TestMethod]
    public async Task AProjectWithoutAnEnvironment_Should_OfferToWatchItsApp()
    {
        var projectId = Guid.NewGuid();
        ProjectDto contoso = new()
        {
            Id = projectId,
            Name = "Contoso back office",
            License = new() { Kind = ProjectLicenseKind.Licensed, State = LicenseState.EndingSoon, Until = DateTimeOffset.UtcNow.AddDays(12) },
            Monitorings = []
        };

        var projects = A.Fake<IProjectsController>();
        A.CallTo(() => projects.GetProject(projectId, A<string?>._, A<CancellationToken>._)).Returns(contoso);
        var monitorings = A.Fake<IMonitoringsController>();

        await using var server = await StartServer(projects, monitorings);
        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<ProjectPage>(parameters => parameters.Add(p => p.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Watch its app", cut.Find(".no-environment").TextContent));
        Assert.IsEmpty(cut.FindAll("nav.environments .environment"));
        Assert.Contains("License ends", cut.Markup);
        Assert.Contains(link => link.GetAttribute("href") == $"{PageUrls.AddProject}?project={projectId}", cut.FindAll(".no-environment a"));
        A.CallTo(() => monitorings.GetMonitoring(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [TestMethod]
    public async Task ItsPeople_Should_ListTheTeam_MembersFirst_AndAddOrTakeOffWithOneButton()
    {
        var projectId = Guid.NewGuid();
        ProjectMemberDto jane = new() { UserId = Guid.NewGuid(), Name = "Jane", IsMember = true, AddedOn = DateTimeOffset.UtcNow, AlertRouteCount = 1 };
        ProjectMemberDto omid = new() { UserId = Guid.NewGuid(), Name = "Omid", IsMember = true, AddedOn = DateTimeOffset.UtcNow };
        ProjectMemberDto sara = new() { UserId = Guid.NewGuid(), Name = "Sara" };

        var projects = A.Fake<IProjectsController>();
        A.CallTo(() => projects.GetMembers(projectId, A<CancellationToken>._)).Returns([jane, omid, sara]);
        A.CallTo(() => projects.AddMember(A<ProjectMemberRequestDto>._, A<CancellationToken>._))
            .Returns([jane, omid, new ProjectMemberDto { UserId = sara.UserId, Name = "Sara", IsMember = true, AddedOn = DateTimeOffset.UtcNow }]);
        A.CallTo(() => projects.RemoveMember(A<ProjectMemberRequestDto>._, A<CancellationToken>._))
            .Returns([omid, new ProjectMemberDto { UserId = jane.UserId, Name = "Jane" }, sara]);

        await using var server = await StartServer(projects, A.Fake<IMonitoringsController>());
        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<ProjectMembers>(parameters => parameters.Add(p => p.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.HasCount(3, cut.FindAll(".person")));
        Assert.Contains("Its people", cut.Markup);
        Assert.Contains("Gets its alerts", Person(cut, "Jane").TextContent, "A member whose alerts reach them says so.");
        Assert.Contains("Works on it", Person(cut, "Omid").TextContent);
        Assert.Contains("In the team", Person(cut, "Sara").TextContent, "Someone of the team who isn't on it.");
        Assert.Contains("not-member", Person(cut, "Sara").GetAttribute("class"));

        Person(cut, "Sara").QuerySelector("button")!.Click();
        cut.WaitForAssertion(() => Assert.Contains("Works on it", Person(cut, "Sara").TextContent));
        A.CallTo(() => projects.AddMember(A<ProjectMemberRequestDto>.That.Matches(request => request.ProjectId == projectId && request.UserId == sara.UserId), A<CancellationToken>._)).MustHaveHappenedOnceExactly();

        Person(cut, "Jane").QuerySelector("button")!.Click();
        cut.WaitForAssertion(() => Assert.Contains("In the team", Person(cut, "Jane").TextContent));
        A.CallTo(() => projects.RemoveMember(A<ProjectMemberRequestDto>.That.Matches(request => request.ProjectId == projectId && request.UserId == jane.UserId), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private static AngleSharp.Dom.IElement Person(IRenderedComponent<ProjectMembers> cut, string name)
        => cut.FindAll(".person").Single(person => person.TextContent.Contains(name));

    private async Task<AppTestServer> StartServer(IProjectsController projects, IMonitoringsController monitorings)
    {
        var server = new AppTestServer();

        await server.Build(configureTestWebAppServices: services =>
        {
            services.Replace(ServiceDescriptor.Singleton(projects));
            services.Replace(ServiceDescriptor.Singleton(monitorings));
        }).Start(TestContext.CancellationToken);

        return server;
    }
}
