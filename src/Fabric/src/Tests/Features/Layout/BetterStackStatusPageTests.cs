using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Bit.Fabric.Client.Core;
using Bit.Fabric.Client.Core.Infrastructure.Services;

namespace Bit.Fabric.Tests.Features.Layout;

/// <summary>
/// bit Fabric's own status page is a Better Stack one, whose <c>/index.json</c> (JSON:API, served as text/html) the app
/// reads for <c>StatusPageNotice</c> and BevTools' Status tab, beside the <c>summary.json</c> shape its customers' pages
/// serve (See <see cref="BetterStackStatusPage"/>).
/// </summary>
[TestClass, TestCategory("UnitTest")]
public class BetterStackStatusPageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 21, 14, 0, TimeSpan.Zero);

    // A monitor down, the incident its failure opened with two updates, and a maintenance two hours away.
    private const string Index = """
        {
          "data": {
            "id": "165", "type": "status_page",
            "attributes": { "company_name": "Example", "subdomain": "example", "custom_domain": null, "aggregate_state": "downtime", "updated_at": "2026-11-25T21:10:00.000Z" },
            "relationships": {
              "sections": { "data": [ { "id": "163", "type": "status_page_section" } ] },
              "resources": { "data": [ { "id": "365", "type": "status_page_resource" }, { "id": "366", "type": "status_page_resource" } ] },
              "status_reports": { "data": [ { "id": "327", "type": "status_report" }, { "id": "328", "type": "status_report" } ] }
            }
          },
          "included": [
            { "id": "163", "type": "status_page_section", "attributes": { "name": "Current status by service", "position": 0 } },
            { "id": "365", "type": "status_page_resource", "attributes": { "status_page_section_id": 163, "resource_type": "Monitor", "public_name": "API", "status": "downtime", "availability": 0.998 } },
            { "id": "366", "type": "status_page_resource", "attributes": { "status_page_section_id": 163, "resource_type": "Monitor", "public_name": "Website", "status": "operational", "availability": 1.0 } },
            {
              "id": "327", "type": "status_report",
              "attributes": { "title": "API is down", "report_type": "automatic", "starts_at": "2026-11-25T20:58:00.000Z", "ends_at": null, "aggregate_state": "downtime" },
              "relationships": { "status_updates": { "data": [ { "id": "512", "type": "status_update" }, { "id": "513", "type": "status_update" } ] } }
            },
            { "id": "512", "type": "status_update", "attributes": { "message": "The API doesn't answer.", "published_at": "2026-11-25T20:58:00.000Z" } },
            { "id": "513", "type": "status_update", "attributes": { "message": "We found the cause and are rolling out a fix.", "published_at": "2026-11-25T21:09:00.000Z" } },
            {
              "id": "328", "type": "status_report",
              "attributes": { "title": "Database upgrade", "report_type": "maintenance", "starts_at": "2026-11-25T23:14:00.000Z", "ends_at": "2026-11-25T23:44:00.000Z", "aggregate_state": "maintenance" },
              "relationships": { "status_updates": { "data": [] } }
            }
          ]
        }
        """;

    [TestMethod]
    public async Task TheIndex_Should_ShowItsOpenIncident_WithItsLatestUpdate_AndLinkToThePage()
    {
        var service = new TestStatusPageService("https://status.example.com/index.json",
            () => new(HttpStatusCode.OK) { Content = new StringContent(Index, System.Text.Encoding.UTF8, "text/html") });

        var notice = await service.GetNotice(TestContext.CancellationToken);

        Assert.IsNotNull(notice);
        Assert.AreEqual(StatusNoticeKind.Outage, notice.Kind);
        Assert.AreEqual("API is down", notice.Title);
        Assert.AreEqual("We found the cause and are rolling out a fix.", notice.Text);
        Assert.AreEqual(new DateTimeOffset(2026, 11, 25, 20, 58, 0, TimeSpan.Zero), notice.StartsAt);
        Assert.IsNull(notice.Phase, "Better Stack names no step of an incident.");
        Assert.AreEqual("https://status.example.com/", notice.Link);

        var summary = (await service.Read(TestContext.CancellationToken))!.Summary!;
        Assert.AreEqual("major", summary.Status!.Indicator);
        Assert.AreEqual("Example", summary.Page!.Name);
        Assert.AreSequenceEqual(new[] { "API: major_outage", "Website: operational" }, summary.Components!.Select(c => $"{c.Name}: {c.Status}").ToArray());
    }

    [TestMethod]
    public void AMaintenanceWithinADay_Should_ShowOnceTheIncidentEnded_AndNothingWhenAllIsWell()
    {
        var ended = Index.Replace("\"ends_at\": null", "\"ends_at\": \"2026-11-25T21:12:00.000Z\"");
        var maintenance = StatusNotice.From(Read(ended), Now);

        Assert.IsNotNull(maintenance);
        Assert.AreEqual(StatusNoticeKind.Maintenance, maintenance.Kind);
        Assert.AreEqual("Database upgrade", maintenance.Title);
        Assert.IsFalse(maintenance.InProgress);
        Assert.AreEqual(new DateTimeOffset(2026, 11, 25, 23, 44, 0, TimeSpan.Zero), maintenance.EndsAt);

        Assert.IsTrue(StatusNotice.From(Read(ended), Now.AddHours(2.25))!.InProgress, "Under way once it starts.");

        var operational = """
            { "data": { "id": "165", "type": "status_page", "attributes": { "company_name": "Example", "aggregate_state": "operational" } }, "included": [] }
            """;
        Assert.IsNull(StatusNotice.From(Read(operational), Now));
        Assert.AreEqual("none", Read(operational).Status!.Indicator);
    }

    [TestMethod]
    public async Task ASummaryJson_Should_StillBeReadInItsOwnShape()
    {
        var service = new TestStatusPageService("https://api.example.com/summary.json",
            () => new(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    { "incidents": [ { "id": "a1", "name": "Email stopped working", "status": "investigating", "impact": "minor", "created_at": "2026-11-25T20:00:00+00:00" } ] }
                    """, System.Text.Encoding.UTF8, "application/json")
            });

        var notice = await service.GetNotice(TestContext.CancellationToken);

        Assert.AreEqual(StatusNoticeKind.PartialOutage, notice!.Kind);
        Assert.AreEqual("Email stopped working", notice.Title);
        Assert.AreEqual(0, notice.Phase);
    }

    [TestMethod]
    public async Task TheStatusPage_Should_BeAskedWithoutTheTraceContext_AndEveryOtherRequestKeepIt()
    {
        // The propagator Client.Web's Program sets, used the way HttpClient's own tracing uses it: in the browser a
        // traceparent makes the request a preflighted one, which a Better Stack page doesn't answer.
        var propagator = new TraceContextPropagator(DistributedContextPropagator.CreateDefaultPropagator());
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();
        static void Set(object? carrier, string name, string? value) => ((HttpRequestMessage)carrier!).Headers.TryAddWithoutValidation(name, value);

        bool? traced = null;
        var service = new TestStatusPageService("https://status.example.com/index.json",
            () => new(HttpStatusCode.OK) { Content = new StringContent(Index, System.Text.Encoding.UTF8, "text/html") },
            request =>
            {
                propagator.Inject(activity, request, Set);
                traced = request.Headers.Contains("traceparent");
            });

        Assert.IsNotNull((await service.Read(TestContext.CancellationToken))!.Summary);
        Assert.IsFalse(traced);

        using var other = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/api/v1/projects");
        propagator.Inject(activity, other, Set);
        Assert.IsTrue(other.Headers.Contains("traceparent"));
    }

    private static StatusPageSummary Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.IsTrue(BetterStackStatusPage.Is(document.RootElement));
        return BetterStackStatusPage.Read(document.RootElement, new Uri("https://status.example.com/index.json"), Now);
    }

    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;

    private sealed class TestStatusPageService(string url, Func<HttpResponseMessage> answer, Action<HttpRequestMessage>? sending = null)
        : StatusPageService(new ClientCoreSettings { ServerAddress = "/", StatusPageSummaryUrl = url }, new FixedTimeProvider(Now), NullLogger<StatusPageService>.Instance)
    {
        protected override HttpClient CreateHttpClient() => new(new AnswerHandler(answer, sending));
    }

    private sealed class AnswerHandler(Func<HttpResponseMessage> answer, Action<HttpRequestMessage>? sending) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            sending?.Invoke(request);
            return Task.FromResult(answer());
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
