using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// Monitoring keeps watching when the approval breaks (bitfabric-internal/specs/monitoring.md, Decisions): found when
/// Production's database was recreated and Test's monitoring of it renewed in that minute, got HTTP 500, and stopped checking
/// anything at all.
/// </summary>
public partial class ProjectMonitoringTests
{
    [TestMethod]
    public async Task ARenewalTheAppFailsWithAServerError_Should_KeepWatching_NeedReconnectingOnlyAfterAnHour_AndReconnectByItself()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IMonitoringsController>();
        var project = await Connect(projects, customerApp, "customer.example");
        var start = DateTimeOffset.UtcNow.AddMinutes(1);

        // The app's database is being recreated: its token endpoint fails, as Production's answered 500 on 26 Nov.
        customerApp.ExpireAccessToken();
        customerApp.RefreshFailsWith = HttpStatusCode.InternalServerError;

        await ProbeAt(server, project.Id, start);
        var card = await CardOf(projects);
        Assert.AreEqual(ProjectConnectionStatus.Connected, card.ConnectionStatus, "A server error may pass: no reconnecting yet.");
        Assert.AreEqual(ProjectHealth.Operational, card.Health, "Its public /health says it is up.");
        Assert.AreEqual(ProjectProbeOutcome.PublicHealth, await LastOutcome(server, project.Id));

        await ProbeAt(server, project.Id, start.AddMinutes(45));
        Assert.AreEqual(ProjectConnectionStatus.Connected, (await CardOf(projects)).ConnectionStatus, "Still within the hour.");

        await ProbeAt(server, project.Id, start.AddMinutes(61));
        card = await CardOf(projects);
        Assert.AreEqual(ProjectConnectionStatus.ReconnectNeeded, card.ConnectionStatus, "An hour of it is worth telling.");
        Assert.Contains("HTTP 500", card.ConnectionProblem!);
        Assert.AreEqual(ProjectHealth.Operational, card.Health, "It is still watched.");

        // The app is back. The token is tried again every 15 minutes, not every minute.
        customerApp.RefreshFailsWith = null;
        var refreshes = customerApp.RefreshCount;

        await ProbeAt(server, project.Id, start.AddMinutes(70));
        Assert.AreEqual(refreshes, customerApp.RefreshCount, "Not due yet.");
        Assert.AreEqual(ProjectConnectionStatus.ReconnectNeeded, (await CardOf(projects)).ConnectionStatus);

        await ProbeAt(server, project.Id, start.AddMinutes(77));
        card = await CardOf(projects);
        Assert.AreEqual(ProjectConnectionStatus.Connected, card.ConnectionStatus, "A report read with the token connects it again, with no click.");
        Assert.IsNull(card.ConnectionProblem);
        Assert.AreEqual(ProjectProbeOutcome.Read, await LastOutcome(server, project.Id));
    }

    [TestMethod]
    public async Task AnAppDownWhileItNeedsReconnecting_Should_StillOpenAnIncident_AndCloseIt()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IMonitoringsController>();
        var incidents = client.GetController<IProjectIncidentsController>();
        var project = await Connect(projects, customerApp, "customer.example");
        var start = DateTimeOffset.UtcNow.AddMinutes(1);

        customerApp.Revoke();
        await ProbeAt(server, project.Id, start);
        Assert.AreEqual(ProjectConnectionStatus.ReconnectNeeded, (await CardOf(projects)).ConnectionStatus, "A refusal of the app's own needs reconnecting at once.");

        // Its public /health answers 503 Unhealthy, twice in a row.
        customerApp.Fail("AppDbContext", "The database didn't answer.");
        await ProbeAt(server, project.Id, start.AddMinutes(1));
        await ProbeAt(server, project.Id, start.AddMinutes(2));

        var card = await CardOf(projects);
        Assert.AreEqual(ProjectHealth.Down, card.Health);
        Assert.IsNotNull(card.OngoingIncident, "Uptime and incidents go on without the report.");
        Assert.IsNull((await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single().EndedAt);

        customerApp.Recover("AppDbContext");
        await ProbeAt(server, project.Id, start.AddMinutes(3));
        await ProbeAt(server, project.Id, start.AddMinutes(4));

        Assert.IsNotNull((await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single().EndedAt, "Healthy twice in a row closes it.");
        Assert.AreEqual(ProjectHealth.Operational, (await CardOf(projects)).Health);
    }

    [TestMethod]
    public async Task AReportThatIsGone_Should_NeedReconnectingAfterAnHour_AndSaySo()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IMonitoringsController>();
        var project = await Connect(projects, customerApp, "customer.example");
        var start = DateTimeOffset.UtcNow.AddMinutes(1);

        // A developer removed the code behind /healthz, or a deployment is on its way.
        customerApp.ServesHealthz = false;

        await ProbeAt(server, project.Id, start);
        var card = await CardOf(projects);
        Assert.AreEqual(ProjectConnectionStatus.Connected, card.ConnectionStatus, "A 404 may be a deployment on its way.");
        Assert.AreEqual(ProjectHealth.Operational, card.Health, "A missing report is no outage: /health says it is up.");
        Assert.IsNull(card.OngoingIncident);

        await ProbeAt(server, project.Id, start.AddMinutes(61));
        card = await CardOf(projects);
        Assert.AreEqual(ProjectConnectionStatus.ReconnectNeeded, card.ConnectionStatus);
        Assert.AreEqual(ProjectConnectionProblems.Describe(ProjectConnectionProblems.ReportGone, CultureInfo.GetCultureInfo("en")), card.ConnectionProblem);
    }

    [TestMethod]
    public async Task TheTroubleshootPrompt_Should_FitTheEnvironment_InEnglish()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IMonitoringsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        customerApp.Fail("smtp", "The mail server didn't answer within 5 s.");
        await Probe(server, project.Id);

        var app = await projects.GetTroubleshootPrompt(project.Id, TestContext.CancellationToken);
        Assert.AreEqual(TroubleshootPromptKind.App, app.Kind);
        Assert.Contains("https://customer.example", app.Prompt!);
        Assert.Contains("(`smtp`): Degraded", app.Prompt!, "The part that fails, by the app's own key.");
        Assert.Contains("dev-mcp-production", app.Prompt!, "The environment's own dev MCP.");
        Assert.Contains("troubleshoot", app.Prompt!);

        customerApp.Revoke();
        await Probe(server, project.Id);

        var connection = await projects.GetTroubleshootPrompt(project.Id, TestContext.CancellationToken);
        Assert.AreEqual(TroubleshootPromptKind.Connection, connection.Kind);
        Assert.Contains("new password", connection.Prompt!, "The app's own reason.");
        Assert.Contains("https://customer.example/.well-known/oauth-protected-resource/healthz", connection.Prompt!);
        Assert.Contains("https://customer.example/oauth/token", connection.Prompt!);
        Assert.Contains("Reconnect", connection.Prompt!);
        Assert.DoesNotContain(customerApp.AccessToken!, connection.Prompt!, "Never a token.");
    }

    private async Task<MonitoringDto> CardOf(IMonitoringsController projects)
        => (await projects.GetMonitorings(null, TestContext.CancellationToken)).Single();

    private async Task<ProjectProbeOutcome> LastOutcome(AppTestServer server, Guid monitoringId)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProjectProbes.IgnoreQueryFilters()
            .Where(probe => probe.MonitoringId == monitoringId && probe.Colo == null)
            .OrderByDescending(probe => probe.At)
            .Select(probe => probe.Outcome)
            .FirstAsync(TestContext.CancellationToken);
    }
}
