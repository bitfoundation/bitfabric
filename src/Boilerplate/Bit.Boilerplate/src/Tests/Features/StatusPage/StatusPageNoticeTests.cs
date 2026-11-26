using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Boilerplate.Client.Core;
using Boilerplate.Client.Core.Components.Layout;
using Boilerplate.Client.Core.Infrastructure.Services;

namespace Boilerplate.Tests.Features.StatusPage;

/// <summary>
/// <c>StatusPageNotice</c> asks the status page once, a few seconds after the app opens, and again only when a request to
/// the app's own server fails after a while; a notice the user closed stays closed until the status page has something new.
/// </summary>
[TestClass, TestCategory("UITest")]
public class StatusPageNoticeTests
{
    [TestMethod]
    public async Task StatusPageNotice_Should_AskAfterTheAppOpens_AndStayClosedUntilTheStatusPageHasSomethingNew()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var statusPage = new FakeStatusPageService(time) { Latest = ("identified", "We found the cause and are rolling out a fix.", time.GetUtcNow().AddMinutes(-3)) };

        await using var server = new AppTestServer();
        await server.Build(configureTestWebAppServices: services => Use(services, time, statusPage)).Start(TestContext.CancellationToken);

        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<StatusPageNotice>();
        var pubSub = ctx.Services.GetRequiredService<PubSubService>();

        Assert.AreEqual(0, statusPage.Requests, "Nothing is asked while the app starts.");
        Assert.IsEmpty(cut.FindAll(".status-notice"));

        time.Advance(StatusPageNotice.FirstCheckDelay);

        cut.WaitForAssertion(() => Assert.HasCount(1, cut.FindAll(".status-notice.outage")), TimeSpan.FromSeconds(10));
        Assert.Contains("Sign-in stopped working", cut.Find("#status-notice-title").TextContent);
        Assert.Contains("We found the cause and are rolling out a fix.", cut.Markup);
        Assert.AreEqual("https://status.example.com/incidents/b2", cut.Find(".status-notice a[href]").GetAttribute("href"));
        var steps = cut.FindAll(".phases li");
        Assert.HasCount(4, steps);
        Assert.Contains("done", steps[0].ClassName ?? "");
        Assert.Contains("now", steps[1].ClassName ?? "");
        Assert.HasCount(1, cut.FindAll(".known"), "An incident says the team knows about it.");

        cut.Find(".status-notice button.close").Click();
        cut.WaitForAssertion(() => Assert.IsEmpty(cut.FindAll(".status-notice")));

        pubSub.Publish(ClientAppMessages.SERVER_REQUEST_FAILED);
        await Task.Delay(200, TestContext.CancellationToken);
        Assert.AreEqual(1, statusPage.Requests, "A failure right after an ask doesn't ask again.");

        time.Advance(StatusPageNotice.AskAgainAfter);
        pubSub.Publish(ClientAppMessages.SERVER_REQUEST_FAILED);
        cut.WaitForAssertion(() => Assert.AreEqual(2, statusPage.Requests), TimeSpan.FromSeconds(10));
        Assert.IsEmpty(cut.FindAll(".status-notice"), "The same update keeps the closed notice closed.");

        time.Advance(StatusPageNotice.AskAgainAfter);
        statusPage.Latest = ("monitoring", "A fix is in place and we're watching it.", time.GetUtcNow());
        pubSub.Publish(ClientAppMessages.SERVER_REQUEST_FAILED);

        cut.WaitForAssertion(() => Assert.HasCount(1, cut.FindAll(".status-notice")), TimeSpan.FromSeconds(10));
        Assert.Contains("A fix is in place and we're watching it.", cut.Markup, "A new update shows it again.");
        Assert.Contains("now", cut.FindAll(".phases li")[2].ClassName ?? "");
    }

    [TestMethod]
    public async Task StatusPageNotice_Should_ShowNothing_AndBreakNothing_WhenTheStatusPageDoesntAnswer()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var statusPage = new FakeStatusPageService(time) { Fails = true };

        await using var server = new AppTestServer();
        await server.Build(configureTestWebAppServices: services => Use(services, time, statusPage)).Start(TestContext.CancellationToken);

        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<StatusPageNotice>();

        time.Advance(StatusPageNotice.FirstCheckDelay);

        cut.WaitForAssertion(() => Assert.AreEqual(1, statusPage.Requests), TimeSpan.FromSeconds(10));
        Assert.IsEmpty(cut.FindAll(".status-notice"));
        Assert.IsNull(statusPage.LastNotice);
    }

    private static void Use(IServiceCollection services, TimeProvider time, StatusPageService statusPage)
    {
        services.Replace(ServiceDescriptor.Singleton(time));
        services.RemoveAll<StatusPageService>();
        services.AddSingleton(statusPage);
    }

    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;

    private sealed class FakeStatusPageService(TimeProvider time)
        : StatusPageService(new ClientCoreSettings { ServerAddress = "/", StatusPageSummaryUrl = "https://status.example.com/api/v2/summary.json" }, time, NullLogger<StatusPageService>.Instance)
    {
        private int requests;

        public int Requests => Volatile.Read(ref requests);

        public (string Status, string Body, DateTimeOffset At) Latest { get; set; }

        public bool Fails { get; set; }

        public DateTimeOffset StartedAt { get; } = time.GetUtcNow().AddMinutes(-14);

        protected override HttpClient CreateHttpClient() => new(new Handler(this));

        private sealed class Handler(FakeStatusPageService owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref owner.requests);

                if (owner.Fails)
                    throw new HttpRequestException("No such host is known.");

                var (status, body, at) = owner.Latest;
                var json = $$"""
                    {
                      "page": { "url": "https://status.example.com" },
                      "incidents": [ {
                        "id": "b2", "name": "Sign-in stopped working", "status": "{{status}}", "impact": "major",
                        "created_at": "{{owner.StartedAt:O}}", "shortlink": "https://status.example.com/incidents/b2",
                        "incident_updates": [ { "status": "{{status}}", "body": "{{body}}", "created_at": "{{at:O}}" } ]
                      } ],
                      "scheduled_maintenances": []
                    }
                    """;

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
            }
        }
    }
}
