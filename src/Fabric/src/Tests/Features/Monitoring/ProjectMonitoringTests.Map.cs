using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>A project's application map (bitfabric-internal/specs/monitoring.md, Application map), from a fake customer app's rounds.</summary>
public partial class ProjectMonitoringTests
{
    [TestMethod]
    public async Task TheApplicationMap_Should_ShowEveryPartByTheAppsOwnNames_WithItsLast24Hours_AndItsIncident()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        await Probe(server, project.Id);
        customerApp.Fail("smtp", "The mail server didn't answer within 5 s.");
        await Probe(server, project.Id);
        await Probe(server, project.Id);

        var map = await projects.GetMap(project.Id, TestContext.CancellationToken);
        var details = await projects.GetProject(project.Id, TestContext.CancellationToken);

        Assert.AreEqual(details.CheckedAt, map.CheckedAt);
        Assert.AreSequenceEqual(details.Checks.Select(check => check.Key).ToArray(), map.Parts.Select(part => part.Key).ToArray(), "Every part the last report names, nothing guessed.");

        var email = map.Parts.Single(part => part.Key == "smtp");
        Assert.AreEqual("Email", email.Title, "By the name the app gives it.");
        Assert.AreEqual("Messaging", email.Category);
        Assert.AreEqual(HealthCheckStatus.Degraded, email.Status);
        Assert.IsNotNull(email.Since);
        Assert.IsNotNull(email.CheckedAt);
        Assert.AreEqual(await RoundsOf(server, project.Id), email.Rounds, "Each round of the last 24 hours counts, the one connecting ran included.");
        Assert.AreEqual(2, email.ProblemRounds, "The two rounds it failed in.");

        var payments = map.Parts.Single(part => part.Key == "paymentGateway");
        Assert.AreEqual(120, payments.LastMs);
        Assert.AreEqual(120, payments.TypicalMs, "The average of its timed runs.");
        Assert.AreEqual(120, payments.SlowestMs);

        var incident = (await client.GetController<IProjectIncidentsController>().GetIncidents(project.Id, TestContext.CancellationToken)).Single();
        Assert.AreEqual(incident.Id, email.OngoingIncidentId, "The part leads to the incident about it.");

        var database = map.Parts.Single(part => part.Key == "AppDbContext");
        Assert.AreEqual(HealthCheckStatus.Healthy, database.Status);
        Assert.AreEqual(0, database.ProblemRounds);
        Assert.IsNull(database.OngoingIncidentId);

        Assert.IsTrue(map.Parts.Single(part => part.Key == "googleSignIn").IsNotConfigured, "A part that only misses its settings says so.");
    }

    [TestMethod]
    public async Task TheApplicationMap_Should_ComeFromTheTools_WorstPartFirst()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");

        customerApp.Fail("smtp", "The mail server didn't answer within 5 s.");
        await Probe(server, project.Id);

        await using var mcp = await ConnectToMcp(server, await AccessTokenOf(client));
        var map = await Call(mcp, "GetApplicationMap", new() { ["project"] = "Customer app" });

        Assert.AreEqual(project.Id, map.GetProperty("project").GetProperty("id").GetGuid());

        var group = map.GetProperty("parts")[0];
        var email = group.GetProperty("parts")[0];
        Assert.AreEqual("Messaging", group.GetProperty("category").GetString(), "What needs attention comes first.");
        Assert.AreEqual("Email", email.GetProperty("title").GetString());
        Assert.AreEqual("Degraded", email.GetProperty("status").GetString());
        Assert.AreEqual(2, email.GetProperty("roundsLast24Hours").GetInt32(), "The round connecting ran, and this one.");
        Assert.AreEqual(1, email.GetProperty("problemRoundsLast24Hours").GetInt32());

        var payments = map.GetProperty("parts").EnumerateArray().SelectMany(g => g.GetProperty("parts").EnumerateArray())
            .Single(part => part.GetProperty("key").GetString() == "paymentGateway");
        Assert.AreEqual(120, payments.GetProperty("typicalMsLast24Hours").GetInt32());
        Assert.AreEqual(120, payments.GetProperty("slowestMsLast24Hours").GetInt32());
    }
}
