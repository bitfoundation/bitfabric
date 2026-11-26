using Bit.Fabric.Server.Api.Features.Tenants;
using Bit.Fabric.Shared.Features.Identity;
using Bit.Fabric.Shared.Features.Licensing;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Features.Projects;
using Bit.Fabric.Tests.Features.Identity;
using Bit.Fabric.Tests.Infrastructure.Services;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// A team's projects, their environments and their people (bitfabric-internal/specs/monitoring.md, Projects, environments
/// and their people), and leaving a team ending its alerts (Alerts, Leaving a Team ends its alerts).
/// </summary>
public partial class ProjectMonitoringTests
{
    /// <summary>The app's report names its project's id, which links its environment to the team's project of that id.</summary>
    [TestMethod]
    public async Task AnEnvironment_Should_JoinTheTeamsProjectOfTheIdItsAppReports()
    {
        var bitProjectId = Guid.NewGuid();
        var customerApp = new FakeCustomerApp();
        customerApp.Report.ProjectId = bitProjectId;
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var team = (await client.GetController<ILicenseController>().GetTeams(TestContext.CancellationToken)).Single().Id;

        // The team licensed the codebase first, on the license page: a project named as bit.json names it.
        var license = await client.GetController<ILicenseController>().Issue(new() { Project = bitProjectId, Name = "Contoso", Commit = new string('a', 40), TeamId = team }, TestContext.CancellationToken);

        var environment = await Connect(client.GetController<IMonitoringsController>(), customerApp, "customer.example");

        Assert.AreEqual(license.ProjectId, environment.ProjectId, "The first round moved it to the project of the id its app reports.");

        var project = (await client.GetController<IProjectsController>().GetProjects(null, TestContext.CancellationToken)).Single();
        Assert.AreEqual(license.ProjectId, project.Id, "The project the connect made for it, left empty, is gone.");
        Assert.AreEqual("Customer app", project.Name, "The name the team gave the app beats bit.json's.");
        Assert.AreEqual(bitProjectId, project.BitProjectId);
        Assert.AreEqual(ProjectLicenseKind.Licensed, project.License.Kind);
        Assert.AreEqual("Production", project.Monitorings.Single().Environment);
        Assert.AreEqual(1, project.MemberCount);
    }

    /// <summary>
    /// A project takes the id its app reports, a second environment joins it under its own name, and a project goes with its
    /// last environment while it holds no license.
    /// </summary>
    [TestMethod]
    public async Task AProject_Should_HaveAnEnvironmentEach_AndGoWithItsLast()
    {
        var bitProjectId = Guid.NewGuid();
        var customerApp = new FakeCustomerApp();
        customerApp.Report.ProjectId = bitProjectId;
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var monitorings = client.GetController<IMonitoringsController>();
        var projects = client.GetController<IProjectsController>();

        var production = await Connect(monitorings, customerApp, "customer.example");
        Assert.AreEqual(bitProjectId, (await projects.GetProject(production.ProjectId, null, TestContext.CancellationToken)).BitProjectId, "Its project takes the id its app reports.");

        customerApp.Address = customerApp.Server = "https://test.customer.example";
        var test = await Connect(monitorings, customerApp, "test.customer.example", production.ProjectId, "Test");

        var project = await projects.GetProject(production.ProjectId, null, TestContext.CancellationToken);
        Assert.AreSequenceEqual(new[] { "Production", "Test" }, project.Monitorings.Select(environment => environment.Environment).ToArray());
        Assert.AreSequenceEqual(new[] { "Customer app", "Customer app (Test)" }, project.Monitorings.Select(environment => environment.Name).ToArray(), "Alerts and status pages call each so.");

        await projects.Rename(new() { Id = project.Id, Name = "Contoso" }, TestContext.CancellationToken);
        Assert.AreSequenceEqual(new[] { "Contoso", "Contoso (Test)" }, (await projects.GetProject(project.Id, null, TestContext.CancellationToken)).Monitorings.Select(environment => environment.Name).ToArray());

        customerApp.Address = customerApp.Server = "https://staging.customer.example";
        await Assert.ThrowsExactlyAsync<BadRequestException>(() => monitorings.StartConnect(new() { Address = "https://staging.customer.example", ProjectId = project.Id, Environment = "test" }, TestContext.CancellationToken),
            "One of each environment in a project, whatever its case.");

        await monitorings.Delete(test.Id, TestContext.CancellationToken);
        Assert.HasCount(1, (await projects.GetProject(project.Id, null, TestContext.CancellationToken)).Monitorings);

        await monitorings.Delete(production.Id, TestContext.CancellationToken);
        Assert.IsEmpty(await projects.GetProjects(null, TestContext.CancellationToken), "A project without a license goes with its last environment.");
    }

    /// <summary>
    /// The regression of the data model review of 2026-11-26: someone who left the team kept getting its alerts. Leaving takes
    /// their place on its projects and every route that reaches them; a route left behind from before reaches no one.
    /// </summary>
    [TestMethod]
    public async Task LeavingATeam_Should_EndItsAlerts()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var owner = await SignInWithATeamOfTheirOwn(server);
        var team = (await owner.GetController<ILicenseController>().GetTeams(TestContext.CancellationToken)).Single().Id;
        var environment = await Connect(owner.GetController<IMonitoringsController>(), customerApp, "customer.example");

        var colleagueEmail = await TestUsers.SignUp(server, TestContext.CancellationToken);
        var colleagueId = await JoinTeam(server, colleagueEmail, team);

        // The owner routes the alerts to the colleague too, which makes them a member of the project.
        var incidents = owner.GetController<IProjectIncidentsController>();
        var alertSettings = await incidents.GetAlertSettings(environment.Id, TestContext.CancellationToken);
        var ownersRoutes = alertSettings.Routes.Length;
        alertSettings.Routes = [.. alertSettings.Routes, new() { Id = Guid.NewGuid(), Step = 0, Channel = ProjectAlertChannel.Email, UserId = colleagueId }];
        await incidents.SaveAlertSettings(alertSettings, TestContext.CancellationToken);

        var members = await owner.GetController<IProjectsController>().GetMembers(environment.ProjectId, TestContext.CancellationToken);
        Assert.IsTrue(members.Single(member => member.UserId == colleagueId).IsMember);

        // The colleague leaves the team, as its page does: switched into it, with elevated access.
        await using var colleague = server.CreateAppClient();
        await colleague.AuthManager.SignIn(new() { Email = colleagueEmail, Password = TestUsers.Password }, TestContext.CancellationToken);
        Assert.IsTrue(await colleague.AuthManager.SwitchTenant(team, TestContext.CancellationToken));
        await EnterElevatedAccess(server, colleague, colleagueEmail);
        await colleague.GetController<IUserController>().LeaveTenant(team, TestContext.CancellationToken);

        Assert.DoesNotContain(route => route.UserId == colleagueId, (await incidents.GetAlertSettings(environment.Id, TestContext.CancellationToken)).Routes, "Their routes went with them.");
        Assert.DoesNotContain(member => member.UserId == colleagueId, await owner.GetController<IProjectsController>().GetMembers(environment.ProjectId, TestContext.CancellationToken), "So did their place on the project.");

        // A route left behind, as one from before this change: alerts skip whoever isn't an accepted member.
        await using (var scope = server.ApiApp.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.ProjectAlertRoutes.AddAsync(new() { Id = Guid.NewGuid(), MonitoringId = environment.Id, TenantId = team, Step = 0, Channel = ProjectAlertChannel.Email, UserId = colleagueId, Culture = "en-US" }, TestContext.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.CancellationToken);
        }

        var sent = await incidents.SendTestAlert(environment.Id, TestContext.CancellationToken);
        Assert.HasCount(ownersRoutes, sent, "The owner's routes get it, and nothing reaches someone who left.");
    }

    /// <summary>Taking someone off a project takes their alert routes on its environments too.</summary>
    [TestMethod]
    public async Task RemovingAProjectMember_Should_RemoveTheirAlertRoutes()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var owner = await SignInWithATeamOfTheirOwn(server);
        var team = (await owner.GetController<ILicenseController>().GetTeams(TestContext.CancellationToken)).Single().Id;
        var environment = await Connect(owner.GetController<IMonitoringsController>(), customerApp, "customer.example");
        var projects = owner.GetController<IProjectsController>();

        var colleagueId = await JoinTeam(server, await TestUsers.SignUp(server, TestContext.CancellationToken), team);

        var members = await projects.AddMember(new() { ProjectId = environment.ProjectId, UserId = colleagueId }, TestContext.CancellationToken);
        Assert.IsTrue(members.Single(member => member.UserId == colleagueId).IsMember);

        var incidents = owner.GetController<IProjectIncidentsController>();
        var alertSettings = await incidents.GetAlertSettings(environment.Id, TestContext.CancellationToken);
        alertSettings.Routes = [.. alertSettings.Routes, new() { Id = Guid.NewGuid(), Step = 1, DelayMinutes = 5, Channel = ProjectAlertChannel.Email, UserId = colleagueId }];
        await incidents.SaveAlertSettings(alertSettings, TestContext.CancellationToken);

        members = await projects.RemoveMember(new() { ProjectId = environment.ProjectId, UserId = colleagueId }, TestContext.CancellationToken);

        Assert.IsFalse(members.Single(member => member.UserId == colleagueId).IsMember, "Still in the team, no longer on the project.");
        Assert.DoesNotContain(route => route.UserId == colleagueId, (await incidents.GetAlertSettings(environment.Id, TestContext.CancellationToken)).Routes);
    }

    /// <summary>A member of the team, as accepting an invitation makes one (inviting asks for an elevated session this test needn't show).</summary>
    private async Task<Guid> JoinTeam(AppTestServer server, string email, Guid team)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await dbContext.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync(TestContext.CancellationToken);

        await dbContext.TenantMemberships.AddAsync(new TenantMembership { Id = Guid.NewGuid(), TenantId = team, UserId = userId, AcceptedOn = DateTimeOffset.UtcNow }, TestContext.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        return userId;
    }

    /// <summary>The code the server emails for an elevated session, entered as the app's dialog does.</summary>
    private async Task EnterElevatedAccess(AppTestServer server, AppClient client, string email)
    {
        await client.GetController<IUserController>().SendElevatedAccessToken(TestContext.CancellationToken);

        var token = server.ApiApp.Services.GetRequiredService<EmailCaptureStore>().Captured
            .Last(captured => captured.Kind is CapturedEmailKind.ElevatedAccess && string.Equals(captured.ToEmailAddress, email, StringComparison.OrdinalIgnoreCase)).Token;

        Assert.IsNotNull(await client.AuthManager.RefreshToken(requestedBy: "test", elevatedAccessToken: token));
    }
}
