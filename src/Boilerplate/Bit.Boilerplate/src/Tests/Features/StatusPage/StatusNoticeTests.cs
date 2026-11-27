using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Boilerplate.Client.Core;
using Boilerplate.Client.Core.Infrastructure.Services;

namespace Boilerplate.Tests.Features.StatusPage;

/// <summary>
/// What <c>StatusPageNotice</c> shows from a status page's <c>summary.json</c>, in bit Fabric's shape and in Atlassian
/// Statuspage's, and that a summary that doesn't come back, or comes back as junk, is logged as an error and shows nothing.
/// </summary>
[TestClass, TestCategory("UnitTest")]
public class StatusNoticeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 21, 14, 0, TimeSpan.Zero);

    // bit Fabric's api/v1/PublicStatus/Summary/{page}: incidents with their updates newest first, maintenance with its times only.
    private const string BitFabricSummary = """
        {
          "page": { "id": "bit-salesmodule", "name": "bit SalesModule", "url": "https://bitfabric.co/status/bit-salesmodule", "time_zone": "Etc/UTC", "updated_at": "2026-11-25T21:13:03+00:00" },
          "components": [ { "id": "01a2", "name": "Sign-in", "description": null, "status": "major_outage" } ],
          "incidents": [
            {
              "id": "a1", "name": "Email stopped working", "status": "investigating", "impact": "minor", "created_at": "2026-11-25T20:00:00+00:00",
              "shortlink": "https://bitfabric.co/status/bit-salesmodule#a1",
              "incident_updates": [ { "status": "investigating", "body": "Looking into it.", "created_at": "2026-11-25T20:00:00+00:00" } ]
            },
            {
              "id": "b2", "name": "Sign-in stopped working", "status": "identified", "impact": "major", "created_at": "2026-11-25T21:00:00+00:00",
              "shortlink": "https://bitfabric.co/status/bit-salesmodule#b2",
              "incident_updates": [
                { "status": "identified", "body": "We found the cause and are rolling out a fix.", "created_at": "2026-11-25T21:11:00+00:00" },
                { "status": "investigating", "body": "Looking into it.", "created_at": "2026-11-25T21:00:00+00:00" }
              ]
            }
          ],
          "scheduled_maintenances": [ { "name": "Database upgrade", "scheduled_for": "2026-11-25T23:00:00+00:00", "scheduled_until": "2026-11-25T23:30:00+00:00" } ],
          "status": { "indicator": "major", "description": "Sign-in stopped working" }
        }
        """;

    // Atlassian Statuspage's /api/v2/summary.json.
    private const string StatuspageSummary = """
        {
          "page": { "id": "kctbh9vrtdwd", "name": "Example", "url": "https://status.example.com", "updated_at": "2026-11-25T21:10:00.000Z" },
          "components": [],
          "incidents": [
            {
              "id": "p31zjtct2jer", "name": "Elevated error rates", "status": "monitoring", "impact": "critical",
              "created_at": "2026-11-25T19:50:00.000Z", "started_at": "2026-11-25T19:45:00.000Z", "updated_at": "2026-11-25T21:05:00.000Z",
              "shortlink": "https://stspg.io/abc",
              "incident_updates": [
                { "id": "u1", "status": "investigating", "body": "We are investigating.", "created_at": "2026-11-25T19:50:00.000Z", "display_at": "2026-11-25T19:50:00.000Z" },
                { "id": "u2", "status": "monitoring", "body": "A fix is in place and we are monitoring.", "created_at": "2026-11-25T21:05:00.000Z", "display_at": "2026-11-25T21:05:00.000Z" }
              ]
            }
          ],
          "scheduled_maintenances": [],
          "status": { "indicator": "critical", "description": "Major System Outage" }
        }
        """;

    [TestMethod]
    public void BitFabricsSummary_Should_ShowTheMostSevereIncident_WithItsLatestUpdateAndStep()
    {
        var notice = StatusNotice.From(Parse(BitFabricSummary), Now)!;

        Assert.AreEqual(StatusNoticeKind.Outage, notice.Kind, "A major incident outranks a minor one, and the maintenance waits.");
        Assert.AreEqual("Sign-in stopped working", notice.Title);
        Assert.AreEqual("We found the cause and are rolling out a fix.", notice.Text);
        Assert.AreEqual(1, notice.Phase, "Identified: the cause is found.");
        Assert.AreEqual(new DateTimeOffset(2026, 11, 25, 21, 0, 0, TimeSpan.Zero), notice.StartsAt);
        Assert.AreEqual(new DateTimeOffset(2026, 11, 25, 21, 11, 0, TimeSpan.Zero), notice.UpdatedAt);
        Assert.AreEqual("https://bitfabric.co/status/bit-salesmodule#b2", notice.Link);
    }

    [TestMethod]
    public void StatuspagesSummary_Should_ShowItsIncident_FromItsStartAndLatestDisplayedUpdate()
    {
        var notice = StatusNotice.From(Parse(StatuspageSummary), Now)!;

        Assert.AreEqual(StatusNoticeKind.Outage, notice.Kind);
        Assert.AreEqual("A fix is in place and we are monitoring.", notice.Text, "The latest update, whatever order the list is in.");
        Assert.AreEqual(2, notice.Phase);
        Assert.AreEqual(new DateTimeOffset(2026, 11, 25, 19, 45, 0, TimeSpan.Zero), notice.StartsAt, "started_at wins over created_at.");
        Assert.AreEqual("https://stspg.io/abc", notice.Link);
    }

    [TestMethod]
    public void Maintenance_Should_Show_WhileUnderWayOrWithinADayOfStarting_AndNotOtherwise()
    {
        StatusPageSummary Summary(StatusPageSummaryIncident maintenance) => new() { Page = new() { Url = "https://status.example.com" }, ScheduledMaintenances = [maintenance] };

        var soon = StatusNotice.From(Summary(new() { Name = "Database upgrade", ScheduledFor = Now.AddHours(2), ScheduledUntil = Now.AddHours(2.5) }), Now)!;
        Assert.AreEqual(StatusNoticeKind.Maintenance, soon.Kind);
        Assert.IsFalse(soon.InProgress);
        Assert.AreEqual("https://status.example.com/", soon.Link, "With no link of its own, the status page's.");

        var underWay = StatusNotice.From(Summary(new() { Name = "Database upgrade", ScheduledFor = Now.AddMinutes(-10), ScheduledUntil = Now.AddMinutes(20) }), Now)!;
        Assert.IsTrue(underWay.InProgress);

        var markedUnderWay = StatusNotice.From(Summary(new() { Name = "Overrunning", Status = "in_progress", ScheduledFor = Now.AddHours(-2), ScheduledUntil = Now.AddHours(-1) }), Now)!;
        Assert.IsTrue(markedUnderWay.InProgress, "The service saying it's under way wins over the planned times.");

        Assert.IsNull(StatusNotice.From(Summary(new() { Name = "Later", ScheduledFor = Now.AddDays(3), ScheduledUntil = Now.AddDays(3).AddHours(1) }), Now));
        Assert.IsNull(StatusNotice.From(Summary(new() { Name = "Over", ScheduledFor = Now.AddHours(-3), ScheduledUntil = Now.AddHours(-2) }), Now));
        Assert.IsNull(StatusNotice.From(Summary(new() { Name = "Done", Status = "completed", ScheduledFor = Now.AddMinutes(-10), ScheduledUntil = Now.AddMinutes(20) }), Now));
    }

    [TestMethod]
    public void NothingWrong_Should_ShowNothing()
    {
        Assert.IsNull(StatusNotice.From(Parse("""{ "page": { "url": "https://status.example.com" }, "incidents": [], "scheduled_maintenances": [], "status": { "indicator": "none" } }"""), Now));
        Assert.IsNull(StatusNotice.From(Parse("""{ "incidents": [ { "id": "x", "name": "Fixed", "status": "resolved", "impact": "major" } ] }"""), Now), "A resolved incident is over.");
        Assert.IsNull(StatusNotice.From(Parse("{}"), Now), "Every field is optional.");
    }

    [TestMethod]
    public void Key_Should_ChangeOnlyWithANewIncidentOrANewUpdate()
    {
        var summary = Parse(BitFabricSummary);
        var key = StatusNotice.From(summary, Now)!.Key;

        Assert.AreEqual(key, StatusNotice.From(Parse(BitFabricSummary), Now.AddMinutes(3))!.Key, "The same incident and update keep a closed notice closed.");

        summary.Incidents![1].IncidentUpdates = [new() { Status = "monitoring", Body = "A fix is in place.", CreatedAt = Now }, .. summary.Incidents[1].IncidentUpdates!];
        Assert.AreNotEqual(key, StatusNotice.From(summary, Now)!.Key, "A new update shows it again.");
    }

    [TestMethod]
    public async Task GetNotice_Should_ReadTheSummaryWithoutTheAppsHeaders()
    {
        HttpRequestMessage? sent = null;
        var service = new TestStatusPageService("https://api.example.com/summary.json", request =>
        {
            sent = request;
            return new(HttpStatusCode.OK) { Content = new StringContent(BitFabricSummary, System.Text.Encoding.UTF8, "application/json") };
        });

        var notice = await service.GetNotice(TestContext.CancellationToken);

        Assert.IsNotNull(notice);
        Assert.AreEqual("Sign-in stopped working", notice.Title);
        Assert.AreEqual("https://api.example.com/summary.json", sent!.RequestUri!.ToString());
        Assert.IsNull(sent.Headers.Authorization, "The app's access token never goes to the status page.");
        Assert.IsEmpty(service.Logger.Errors);
        Assert.AreSame(notice, service.LastNotice);
    }

    [TestMethod]
    public async Task GetNotice_Should_ShowNothing_AndLogNoError_WhenTheStatusPageCantBeReached()
    {
        var service = new TestStatusPageService("https://api.example.com/summary.json", _ => throw new HttpRequestException("No such host is known."));

        Assert.IsNull(await service.GetNotice(TestContext.CancellationToken));
        Assert.IsEmpty(service.Logger.Errors);
    }

    [TestMethod]
    public async Task GetNotice_Should_LogAnErrorAndShowNothing_WhenTheSummaryIsJunk()
    {
        foreach (var answer in new Func<HttpRequestMessage, HttpResponseMessage>[]
        {
            _ => new(HttpStatusCode.BadGateway) { Content = new StringContent("<html>Bad gateway</html>") },
            _ => new(HttpStatusCode.OK) { Content = new StringContent("<html>Not a summary</html>", System.Text.Encoding.UTF8, "text/html") },
            _ => new(HttpStatusCode.OK) { Content = new StringContent("""{ "incidents": "not a list" }""", System.Text.Encoding.UTF8, "application/json") }
        })
        {
            var service = new TestStatusPageService("https://api.example.com/summary.json", answer);

            Assert.IsNull(await service.GetNotice(TestContext.CancellationToken));
            Assert.HasCount(1, service.Logger.Errors);
        }
    }

    [TestMethod]
    public async Task GetNotice_Should_AskNothing_WhenTheSettingIsEmpty_AndLogAnError_WhenItIsntAnAddress()
    {
        var asked = false;

        var off = new TestStatusPageService(null, _ => { asked = true; return new(HttpStatusCode.OK); });
        Assert.IsFalse(off.IsEnabled);
        Assert.IsNull(await off.GetNotice(TestContext.CancellationToken));
        Assert.IsEmpty(off.Logger.Errors);

        var wrong = new TestStatusPageService("status/summary.json", _ => { asked = true; return new(HttpStatusCode.OK); });
        Assert.IsNull(await wrong.GetNotice(TestContext.CancellationToken));
        Assert.HasCount(1, wrong.Logger.Errors);

        Assert.IsFalse(asked);
    }

    private static StatusPageSummary Parse(string json) => JsonSerializer.Deserialize(json, StatusPageSummaryJsonContext.Default.StatusPageSummary)!;

    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;

    private sealed class TestStatusPageService(string? url, Func<HttpRequestMessage, HttpResponseMessage> answer, ErrorsLogger logger)
        : StatusPageService(new ClientCoreSettings { ServerAddress = "/", StatusPageSummaryUrl = url }, new FixedTimeProvider(Now), logger)
    {
        public TestStatusPageService(string? url, Func<HttpRequestMessage, HttpResponseMessage> answer)
            : this(url, answer, new ErrorsLogger())
        {
        }

        public ErrorsLogger Logger { get; } = logger;

        protected override HttpClient CreateHttpClient() => new(new AnswerHandler(answer));
    }

    private sealed class AnswerHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(answer(request));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    public sealed class ErrorsLogger : ILogger<StatusPageService>
    {
        public List<string> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }
    }
}
