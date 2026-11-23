using System.Text.Json;
using Bit.Fabric.Server.Api.Features.Monitoring;
using Bit.Fabric.Server.Api.Infrastructure.Data;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>Incidents, alerts and maintenance (bitfabric-internal/specs/monitoring.md), from a fake customer app's rounds.</summary>
public partial class ProjectMonitoringTests
{
    [TestMethod]
    public async Task APartFailingTwice_Should_OpenAnIncident_AlertTheTeam_AndCloseAfterTwoPassingRounds()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();
        var incidents = client.GetController<IProjectIncidentsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        customerApp.Fail("smtp", "The mail server didn't answer within 5 s.");
        await Probe(server, project.Id);
        Assert.IsEmpty(await incidents.GetIncidents(project.Id, TestContext.CancellationToken), "One failing round is no incident: it can be a single bad answer.");

        await Probe(server, project.Id);
        var opened = (await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single();

        Assert.IsNull(opened.EndedAt);
        Assert.AreEqual(ProjectHealth.Degraded, opened.Impact);
        Assert.AreSequenceEqual(new[] { "smtp" }, opened.CheckKeys, "A part that only misses its settings is no incident.");
        Assert.AreEqual("Email not working properly", opened.Title, "Named the way the app names the part.");
        Assert.AreEqual(opened.Title, (await projects.GetProjects(null, TestContext.CancellationToken)).Single().OngoingIncident);

        // Step 0, at once: a push to whoever connected the project, whose devices don't allow it yet, which the alert says.
        var push = opened.Alerts.Single();
        Assert.AreEqual(ProjectAlertChannel.Push, push.Channel);
        Assert.AreEqual(ProjectAlertKind.Opened, push.Kind);
        Assert.IsFalse(push.Succeeded);
        Assert.AreEqual(ProjectAlertProblem.NoDevice, push.Problem, "The alert says why, in a reason the page words in the reader's language.");

        customerApp.Recover("smtp");
        await Probe(server, project.Id);
        Assert.IsNull((await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single().EndedAt, "It closes after two passing rounds, not one.");

        await Probe(server, project.Id);
        var closed = (await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single();

        Assert.IsNotNull(closed.EndedAt);
        Assert.AreEqual(ProjectIncidentPhase.Resolved, closed.Updates[0].Phase);
        Assert.IsNotNull(closed.AgentPrompt, "Once it's over, a prompt for the team's AI agent.");
        Assert.Contains(alert => alert.Kind is ProjectAlertKind.Resolved, closed.Alerts);
        Assert.IsNull((await projects.GetProjects(null, TestContext.CancellationToken)).Single().OngoingIncident);
    }

    /// <summary>
    /// An app's report can leave a part's title empty, as one mid-deployment did on Test (2026-11-13): the incident then
    /// names the part by its key instead of joining empty names into "، ، و".
    /// </summary>
    [TestMethod]
    public async Task APartWithAnEmptyTitle_Should_BeNamedByItsKey()
    {
        var customerApp = new FakeCustomerApp();
        customerApp.Report.Entries["smtp"].Title = "";
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();
        var incidents = client.GetController<IProjectIncidentsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        customerApp.Fail("smtp", "The mail server didn't answer within 5 s.");
        await Probe(server, project.Id);
        await Probe(server, project.Id);
        var opened = (await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single();

        Assert.AreEqual("smtp not working properly", opened.Title);
        Assert.Contains("smtp", opened.Updates.Single().Text!);
    }

    [TestMethod]
    public async Task AnIncidentNobodyAnswers_Should_Escalate_UntilSomeoneIsOnIt()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var incidents = client.GetController<IProjectIncidentsController>();
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");

        customerApp.Fail("AppDbContext", "The database didn't answer.");
        await Probe(server, project.Id);
        await Probe(server, project.Id);

        Assert.AreEqual(ProjectHealth.Down, (await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single().Impact, "A part the app can't work without is down.");

        // Ten minutes on, nobody said they're on it: step 1, an email to whoever connected the project.
        await Escalate(server, project.Id);
        var escalated = (await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single();
        var email = escalated.Alerts.Single(alert => alert.Kind is ProjectAlertKind.Escalated);
        Assert.AreEqual(ProjectAlertChannel.Email, email.Channel);
        Assert.IsTrue(email.Succeeded, email.Problem?.ToString());

        var acknowledged = await incidents.Acknowledge(escalated.Id, TestContext.CancellationToken);
        Assert.IsNotNull(acknowledged.AcknowledgedBy);
        Assert.AreEqual(ProjectIncidentPhase.Identified, acknowledged.Updates[0].Phase);

        // Nothing more goes out once someone is on it.
        await Escalate(server, project.Id);
        Assert.HasCount(escalated.Alerts.Length, (await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single().Alerts);

        var updated = await incidents.PostUpdate(new() { IncidentId = acknowledged.Id, Phase = ProjectIncidentPhase.Monitoring, Text = "Restarted the database.", Version = acknowledged.Version }, TestContext.CancellationToken);
        Assert.AreEqual("Restarted the database.", updated.Updates[0].Text);

        // bit Fabric closes the incidents it opened.
        await Assert.ThrowsExactlyAsync<BadRequestException>(() => incidents.PostUpdate(new() { IncidentId = updated.Id, Phase = ProjectIncidentPhase.Resolved, Text = "Fixed.", Version = updated.Version }, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AWebhook_Should_GetItsChatsShape()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var incidents = client.GetController<IProjectIncidentsController>();
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");

        var settings = await incidents.GetAlertSettings(project.Id, TestContext.CancellationToken);
        Assert.HasCount(2, settings.Routes, "A new project alerts whoever added it: a push at once, an email later.");

        settings.Routes = [.. settings.Routes,
            new() { Step = 0, Channel = ProjectAlertChannel.Webhook, WebhookFormat = ProjectWebhookFormat.Slack, Address = $"{FakeCustomerApp.WebhookOrigin}/services/T000" },
            new() { Step = 0, Channel = ProjectAlertChannel.Webhook, WebhookFormat = ProjectWebhookFormat.Teams, Address = $"{FakeCustomerApp.WebhookOrigin}/powerautomate/automations/direct/workflows/ops/triggers/manual/paths/invoke" }];
        settings = await incidents.SaveAlertSettings(settings, TestContext.CancellationToken);
        Assert.HasCount(4, settings.Routes);

        var results = await incidents.SendTestAlert(project.Id, TestContext.CancellationToken);
        Assert.IsTrue(results.Where(result => result.Channel is ProjectAlertChannel.Webhook).All(result => result.Succeeded));
        Assert.HasCount(2, customerApp.WebhookBodies);
        var bodies = customerApp.WebhookBodies.Select(body => JsonDocument.Parse(body).RootElement).ToList();

        Assert.Contains(body => body.TryGetProperty("text", out _) && body.TryGetProperty("attachments", out _) is false, bodies, "Slack's incoming webhooks read a text.");

        // A Teams channel's webhook is a workflow, whose "Send webhook alerts to a channel" posts only an Adaptive Card
        var teams = bodies.Single(body => body.TryGetProperty("attachments", out _));
        Assert.AreEqual("message", teams.GetProperty("type").GetString());
        var attachment = teams.GetProperty("attachments").EnumerateArray().Single();
        Assert.AreEqual("application/vnd.microsoft.card.adaptive", attachment.GetProperty("contentType").GetString());
        var card = attachment.GetProperty("content");
        Assert.AreEqual("AdaptiveCard", card.GetProperty("type").GetString(), "Teams refuses any other card: Property 'type' must be 'AdaptiveCard'.");
        Assert.IsGreaterThanOrEqualTo(2, card.GetProperty("body").GetArrayLength(), "The title and what happened.");
        Assert.AreEqual("Action.OpenUrl", card.GetProperty("actions")[0].GetProperty("type").GetString(), "A button opens the project.");

        // Only https, and a webhook says what it talks to.
        settings.Routes = [new() { Step = 0, Channel = ProjectAlertChannel.Webhook, WebhookFormat = ProjectWebhookFormat.Discord, Address = "http://hooks.chat.example/x" }];
        await Assert.ThrowsExactlyAsync<BadRequestException>(() => incidents.SaveAlertSettings(settings, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AFailureDuringMaintenance_Should_AlertNobody_AndShowInHistory()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var incidents = client.GetController<IProjectIncidentsController>();
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");

        var now = DateTimeOffset.UtcNow;
        await incidents.AddMaintenanceWindow(new() { ProjectId = project.Id, Title = "Moving the database", StartsAt = now.AddHours(-1), EndsAt = now.AddHours(1) }, TestContext.CancellationToken);

        customerApp.Fail("smtp", "The mail server didn't answer within 5 s.");
        await Probe(server, project.Id);
        await Probe(server, project.Id);

        var incident = (await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single();
        Assert.IsTrue(incident.DuringMaintenance);
        Assert.IsEmpty(incident.Alerts, "During a window nobody is alerted.");

        var history = await incidents.GetHistory(new() { ProjectId = project.Id, From = now.AddDays(-1), To = DateTimeOffset.UtcNow }, TestContext.CancellationToken);
        Assert.Contains(period => period.CheckKey == "smtp" && period.Status is HealthCheckStatus.Degraded && period.Title == "Email", history.Periods);
        Assert.HasCount(1, history.Incidents);
        Assert.HasCount(1, history.Maintenance);
        Assert.IsLessThanOrEqualTo(DateTimeOffset.UtcNow, history.WatchedSince);
    }

    [TestMethod]
    public async Task Settings_Should_Save_EvenAfterRoundsChangedTheProject()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var incidents = client.GetController<IProjectIncidentsController>();
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");

        var settings = await incidents.GetSettings(project.Id, TestContext.CancellationToken);

        // The tab stays open while rounds rewrite the project's state every minute.
        await Probe(server, project.Id);

        settings.Name = "Customer app (production)";
        settings.TimeZoneId = "Asia/Tehran";
        var saved = await incidents.SaveSettings(settings, TestContext.CancellationToken);

        Assert.AreEqual("Customer app (production)", saved.Name);
        Assert.AreEqual("Asia/Tehran", saved.TimeZoneId);
    }

    /// <summary>The escalation job, as if the next step's time had come.</summary>
    private async Task Escalate(AppTestServer server, Guid projectId)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.ProjectIncidents.IgnoreQueryFilters()
            .Where(incident => incident.ProjectId == projectId && incident.NextEscalationAt != null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(incident => incident.NextEscalationAt, DateTimeOffset.UtcNow.AddMinutes(-1)), TestContext.CancellationToken);

        await scope.ServiceProvider.GetRequiredService<ProjectEscalationJobRunner>().Escalate(TestContext.CancellationToken);
    }
}
