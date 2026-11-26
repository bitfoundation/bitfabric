using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// History over days that change what the app reports (bitfabric-internal/specs/monitoring.md, A part counts only while the
/// app reports it).
/// </summary>
public partial class ProjectMonitoringTests
{
    /// <summary>
    /// Over four days the app's report changes the way a real app's does between deployments: a part is added, a part
    /// fails and is then taken out, and a part that misses its settings is reported, as a template from before
    /// <c>isNotConfigured</c> did, as Degraded like an outage, then as not configured (AdminPanel on Test, 2026-11-13).
    /// History counts each part only while the report had it, so the days before the new part, and after the one taken
    /// out, have no data rather than a healthy day, and it leaves out the part that only misses its settings, its old
    /// Degraded period included.
    /// </summary>
    [TestMethod]
    public async Task History_Should_CountEachPartOnlyWhileTheReportHadIt_AndLeaveOutOneThatOnlyMissesItsSettings()
    {
        var customerApp = new FakeCustomerApp();
        var googleSignIn = customerApp.Report.Entries["googleSignIn"];
        googleSignIn.IsNotConfigured = false;
        googleSignIn.Tags = ["category:Identity"];

        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var incidents = client.GetController<IProjectIncidentsController>();
        var project = await Connect(client.GetController<IMonitoringsController>(), customerApp, "customer.example");
        var connectedAt = DateTimeOffset.UtcNow;

        // The next day: the app's template now says what only misses its settings, and its payment gateway fails.
        googleSignIn.IsNotConfigured = true;
        googleSignIn.Tags = ["category:Identity", "notConfigured"];
        customerApp.Fail("paymentGateway", "The gateway answered 502.");
        await ProbeAt(server, project.Id, connectedAt.AddDays(1));

        // The day after: a deployment takes the payment gateway out and adds search.
        customerApp.Report.Entries.Remove("paymentGateway");
        customerApp.Report.Entries["search"] = new() { Status = HealthCheckStatus.Healthy, FailureStatus = HealthCheckStatus.Degraded, Tags = ["category:Integrations"], Category = "Integrations", Title = "Search", IconName = "Search" };
        await ProbeAt(server, project.Id, connectedAt.AddDays(2));
        await ProbeAt(server, project.Id, connectedAt.AddDays(3));

        var now = connectedAt.AddDays(3).AddMinutes(1);
        var days = ProjectHistoryDays.Last(4, TimeZoneInfo.Utc, now);
        var history = await incidents.GetHistory(new() { MonitoringId = project.Id, From = days[0].Start, To = now }, TestContext.CancellationToken);

        Assert.AreSequenceEqual(new[] { "AppDbContext", "paymentGateway", "search", "smtp" }, history.Parts.Select(part => part.Key).Order(StringComparer.Ordinal).ToArray(),
            "The parts of the latest report, and the one taken out after it failed; not the one that only misses its settings.");
        Assert.DoesNotContain(period => period.CheckKey == "googleSignIn", history.Periods, "Its Degraded period from before the template said so goes with it.");

        ProjectHistoryDay On(string key, DateTimeOffset at) => ProjectHistoryDays.OfPart(history, history.Parts.Single(part => part.Key == key), days, now)
            .Single(day => day.Date == DateOnly.FromDateTime(at.UtcDateTime));

        var search = history.Parts.Single(part => part.Key == "search");
        Assert.AreEqual("Search", search.Title);
        Assert.IsNull(search.ReportedTo, "The latest report has it.");
        Assert.AreEqual(ProjectDayTone.NoData, On("search", connectedAt).Tone, "Search didn't exist yet: no data, not a healthy day.");
        Assert.AreEqual(ProjectDayTone.NoData, On("search", connectedAt.AddDays(1)).Tone);
        Assert.AreEqual(ProjectDayTone.Ok, On("search", connectedAt.AddDays(2)).Tone);
        Assert.AreEqual(ProjectDayTone.Ok, On("search", connectedAt.AddDays(3)).Tone);

        var paymentGateway = history.Parts.Single(part => part.Key == "paymentGateway");
        Assert.AreEqual("Payment gateway", paymentGateway.Title, "Named the way the app last named it.");
        Assert.IsNotNull(paymentGateway.ReportedTo, "The report doesn't have it any more.");
        Assert.AreEqual(ProjectDayTone.Ok, On("paymentGateway", connectedAt).Tone);
        Assert.AreEqual(ProjectDayTone.Degraded, On("paymentGateway", connectedAt.AddDays(1)).Tone);
        Assert.AreEqual(ProjectDayTone.NoData, On("paymentGateway", connectedAt.AddDays(3)).Tone, "Taken out: no data, not a healthy day.");

        Assert.AreEqual(ProjectDayTone.Ok, On("AppDbContext", connectedAt.AddDays(1)).Tone, "A part reported all along counts every day.");
        Assert.AreEqual(ProjectDayTone.Ok, On("AppDbContext", connectedAt.AddDays(3)).Tone);
    }

    /// <summary>A round on a day the test chooses: the prober takes its time from the clock it is given, and so do the periods and rollups it writes.</summary>
    private async Task ProbeAt(AppTestServer server, Guid monitoringId, DateTimeOffset at)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        await ActivatorUtilities.CreateInstance<ProjectProber>(scope.ServiceProvider, new ClockAt(at)).Probe(monitoringId, TestContext.CancellationToken);
    }

    private sealed class ClockAt(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
