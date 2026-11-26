using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Boilerplate.Client.Core;
using Boilerplate.Client.Core.Components.Layout.Diagnostic;
using Boilerplate.Client.Core.Infrastructure.Services;

namespace Boilerplate.Tests.Features.StatusPage;

/// <summary>
/// BevTools' Status tab (<c>AppStatusPagePanel</c>) reads the app's status page the moment it opens, with none of the
/// notice's wait, shows what the summary says and what the app's users are shown for it, reads it again on request, and
/// says why when the status page doesn't answer.
/// </summary>
[TestClass, TestCategory("UITest")]
public class AppStatusPagePanelTests
{
    [TestMethod]
    public async Task TheStatusTab_Should_ReadTheStatusPageAtOnce_ShowWhatItSays_AndReadItAgainOnRequest()
    {
        var statusPage = new FakeStatusPageService("https://status.example.com/api/v2/summary.json");

        await using var server = new AppTestServer();
        await server.Build(configureTestWebAppServices: services => Use(services, statusPage)).Start(TestContext.CancellationToken);

        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<AppStatusPagePanel>();

        cut.WaitForAssertion(() => Assert.Contains("Partial System Outage", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.AreEqual(1, statusPage.Requests, "Read when the tab opens, without the notice's wait.");
        Assert.Contains("https://status.example.com/api/v2/summary.json", cut.Find(".address").TextContent);
        Assert.Contains("Outage: Sign-in stopped working", cut.Markup, "What the app's users are shown for it.");

        var rows = cut.FindAll(".row");
        Assert.HasCount(2, rows, "A group gathers other components, which are listed on their own.");
        Assert.Contains("Website and app", rows[0].TextContent);
        Assert.Contains("Operational", rows[0].TextContent);
        Assert.Contains("Sign-in", rows[1].TextContent);
        Assert.Contains("Major outage", rows[1].TextContent);
        Assert.Contains("We found the cause and are rolling out a fix.", cut.Markup);
        Assert.Contains("Database upgrade", cut.Markup);

        statusPage.Resolved = true;
        cut.Find("button").Click();

        cut.WaitForAssertion(() => Assert.Contains("All Systems Operational", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.AreEqual(2, statusPage.Requests);
        Assert.Contains("Nothing: the app's users see no notice.", cut.Markup);
    }

    [TestMethod]
    public async Task TheStatusTab_Should_SayWhy_WhenTheStatusPageDoesntAnswer()
    {
        var statusPage = new FakeStatusPageService("https://status.example.com/api/v2/summary.json") { Fails = true };

        await using var server = new AppTestServer();
        await server.Build(configureTestWebAppServices: services => Use(services, statusPage)).Start(TestContext.CancellationToken);

        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<AppStatusPagePanel>();

        cut.WaitForAssertion(() => Assert.Contains("The status page didn't answer", cut.Markup), TimeSpan.FromSeconds(10));
        Assert.Contains("HttpRequestException: No such host is known.", cut.Find(".error").TextContent);
    }

    [TestMethod]
    public async Task TheStatusTab_Should_SaySo_WhenNoStatusPageIsSet()
    {
        var statusPage = new FakeStatusPageService(null);

        await using var server = new AppTestServer();
        await server.Build(configureTestWebAppServices: services => Use(services, statusPage)).Start(TestContext.CancellationToken);

        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<AppStatusPagePanel>();

        Assert.Contains("No status page", cut.Markup);
        Assert.Contains("StatusPageSummaryUrl is empty", cut.Markup);
        Assert.AreEqual(0, statusPage.Requests);
        Assert.IsEmpty(cut.FindAll("button"), "Nothing to check again.");
    }

    private static void Use(IServiceCollection services, StatusPageService statusPage)
    {
        services.RemoveAll<StatusPageService>();
        services.AddSingleton(statusPage);
    }

    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;

    private sealed class FakeStatusPageService(string? summaryUrl)
        : StatusPageService(new ClientCoreSettings { ServerAddress = "/", StatusPageSummaryUrl = summaryUrl }, TimeProvider.System, NullLogger<StatusPageService>.Instance)
    {
        private int requests;

        public int Requests => Volatile.Read(ref requests);

        public bool Fails { get; set; }

        public bool Resolved { get; set; }

        protected override HttpClient CreateHttpClient() => new(new Handler(this));

        private sealed class Handler(FakeStatusPageService owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref owner.requests);

                if (owner.Fails)
                    throw new HttpRequestException("No such host is known.");

                var now = DateTimeOffset.UtcNow;
                var json = owner.Resolved
                    ? """
                      {
                        "page": { "name": "Example", "url": "https://status.example.com" },
                        "status": { "indicator": "none", "description": "All Systems Operational" },
                        "components": [ { "name": "Website and app", "status": "operational" }, { "name": "Sign-in", "status": "operational" } ],
                        "incidents": [], "scheduled_maintenances": []
                      }
                      """
                    : $$"""
                      {
                        "page": { "name": "Example", "url": "https://status.example.com" },
                        "status": { "indicator": "major", "description": "Partial System Outage" },
                        "components": [
                          { "name": "Everything", "status": "major_outage", "group": true },
                          { "name": "Website and app", "status": "operational" },
                          { "name": "Sign-in", "status": "major_outage" }
                        ],
                        "incidents": [ {
                          "id": "b2", "name": "Sign-in stopped working", "status": "identified", "impact": "major",
                          "created_at": "{{now.AddMinutes(-14):O}}",
                          "incident_updates": [ { "status": "identified", "body": "We found the cause and are rolling out a fix.", "created_at": "{{now.AddMinutes(-3):O}}" } ]
                        } ],
                        "scheduled_maintenances": [ {
                          "id": "m1", "name": "Database upgrade", "status": "scheduled",
                          "scheduled_for": "{{now.AddDays(2):O}}", "scheduled_until": "{{now.AddDays(2).AddHours(1):O}}"
                        } ]
                      }
                      """;

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
            }
        }
    }
}
