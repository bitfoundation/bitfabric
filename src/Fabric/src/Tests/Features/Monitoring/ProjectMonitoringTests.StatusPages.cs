using System.Net;
using Bit.Fabric.Server.Api.Infrastructure.Data;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>Status pages (bitfabric-internal/specs/monitoring.md, Status page), from a fake customer app's rounds.</summary>
public partial class ProjectMonitoringTests
{
    [TestMethod]
    public async Task AStatusPage_Should_StartWithTheAppsParts_AndShowNothingUntilPublished()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var statusPages = client.GetController<IStatusPagesController>();
        var project = await Connect(client.GetController<IMonitoringsController>(), customerApp, "customer.example");

        var page = await statusPages.GetStatusPage(project.Id, TestContext.CancellationToken);

        Assert.StartsWith("customer-app", page.Slug!);
        Assert.IsFalse(page.IsPublished);
        // A component per kind of part, named as the app's users would; the part that only misses its settings is in none.
        Assert.AreSequenceEqual(new[] { "Website and app", "Email, SMS and notifications", "Connected services" }, page.Components.Select(component => component.Name["en-US"]).ToArray());
        Assert.DoesNotContain(component => component.CheckKeys.Contains("googleSignIn"), page.Components);

        await using var visitor = server.CreateAppClient();
        var publicStatus = visitor.GetController<IPublicStatusController>();
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => publicStatus.Get(page.Slug!, null, TestContext.CancellationToken), "Nobody sees a page before it's published.");

        page.IsPublished = true;
        page = await statusPages.SaveStatusPage(page, TestContext.CancellationToken);

        var shown = await publicStatus.Get(page.Slug!, "en-US", TestContext.CancellationToken);
        Assert.AreEqual("Customer app", shown.Name);
        Assert.AreEqual(ProjectHealth.Operational, shown.State);
        Assert.HasCount(3, shown.Components);
    }

    [TestMethod]
    public async Task AnIncident_Should_ShowOnThePartsItTouches_WithoutAWordOfTheReport()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var statusPages = client.GetController<IStatusPagesController>();
        var project = await Connect(client.GetController<IMonitoringsController>(), customerApp, "customer.example");

        var page = await statusPages.GetStatusPage(project.Id, TestContext.CancellationToken);
        page.IsPublished = true;
        page = await statusPages.SaveStatusPage(page, TestContext.CancellationToken);

        customerApp.Fail("smtp", "The mail server at mail.internal.example didn't answer.");
        await Probe(server, project.Id);
        await Probe(server, project.Id);

        using var http = server.CreateRawHttpClient();
        var body = await http.GetStringAsync($"api/v1/PublicStatus/Get/{page.Slug}?language=en-US", TestContext.CancellationToken);
        var shown = JsonSerializer.Deserialize(body, AppJsonContext.Default.PublicStatusDto)!;

        Assert.AreEqual(ProjectHealth.Degraded, shown.State);
        Assert.AreEqual(ProjectHealth.Degraded, shown.Components.Single(component => component.Name == "Email, SMS and notifications").State);
        Assert.AreEqual(ProjectHealth.Operational, shown.Components.Single(component => component.Name == "Website and app").State);
        Assert.AreEqual("Email not working properly", shown.Active.Single().Title);

        // The report stays with the team: no key, no description, nothing an attacker could read the app's insides from.
        Assert.DoesNotContain("smtp", body);
        Assert.DoesNotContain("mail.internal.example", body);
    }

    [TestMethod]
    public async Task TheTeam_Should_PostInEveryLanguageTheyWrite_AndAReaderGetsTheirs()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var statusPages = client.GetController<IStatusPagesController>();
        var project = await Connect(client.GetController<IMonitoringsController>(), customerApp, "customer.example");

        var page = await statusPages.GetStatusPage(project.Id, TestContext.CancellationToken);
        page.IsPublished = true;
        page.Languages = ["en-US", "fa-IR"];
        page = await statusPages.SaveStatusPage(page, TestContext.CancellationToken);

        await statusPages.Post(new()
        {
            MonitoringId = project.Id,
            Kind = ProjectIncidentKind.Announcement,
            Title = new() { ["en-US"] = "New sign-in page", ["fa-IR"] = "صفحه‌ی ورود جدید" },
            Text = new() { ["en-US"] = "Signing in got faster.", ["fa-IR"] = "ورود سریع‌تر شد." }
        }, TestContext.CancellationToken);

        await using var visitor = server.CreateAppClient();
        var publicStatus = visitor.GetController<IPublicStatusController>();

        var inPersian = await publicStatus.Get(page.Slug!, "fa-IR", TestContext.CancellationToken);
        Assert.AreEqual("صفحه‌ی ورود جدید", inPersian.History.Single().Title);
        Assert.IsFalse(inPersian.History.Single().Updates.Single().IsFallback);

        // A language the team didn't write in gets the page's main one, marked as such.
        var inGerman = await publicStatus.Get(page.Slug!, "de-DE", TestContext.CancellationToken);
        Assert.AreEqual("New sign-in page", inGerman.History.Single().Title);
        Assert.IsTrue(inGerman.History.Single().Updates.Single().IsFallback);

        // The main language is the one a post can't be without.
        await Assert.ThrowsExactlyAsync<BadRequestException>(() => statusPages.Post(new()
        {
            MonitoringId = project.Id,
            Kind = ProjectIncidentKind.Incident,
            Title = new() { ["fa-IR"] = "کندی" },
            Text = new() { ["fa-IR"] = "بررسی می‌کنیم." }
        }, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task APage_Should_BeReadableAsFeedsABadgeAndAnEmbed_AndFollowedByPush()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var statusPages = client.GetController<IStatusPagesController>();
        var project = await Connect(client.GetController<IMonitoringsController>(), customerApp, "customer.example");

        var page = await statusPages.GetStatusPage(project.Id, TestContext.CancellationToken);
        page.IsPublished = true;
        page = await statusPages.SaveStatusPage(page, TestContext.CancellationToken);

        using var http = server.CreateRawHttpClient();

        Assert.Contains("<rss", await http.GetStringAsync($"api/v1/PublicStatus/Rss/{page.Slug}", TestContext.CancellationToken));
        Assert.Contains("http://www.w3.org/2005/Atom", await http.GetStringAsync($"api/v1/PublicStatus/Atom/{page.Slug}", TestContext.CancellationToken));
        Assert.Contains("\"indicator\":\"none\"", await http.GetStringAsync($"api/v1/PublicStatus/Summary/{page.Slug}", TestContext.CancellationToken), "Statuspage's shape, which other tools read.");
        Assert.Contains("BEGIN:VCALENDAR", await http.GetStringAsync($"api/v1/PublicStatus/Calendar/{page.Slug}", TestContext.CancellationToken));
        Assert.Contains("operational", await http.GetStringAsync($"api/v1/PublicStatus/Badge/{page.Slug}", TestContext.CancellationToken));
        Assert.DoesNotContain("createElement", await http.GetStringAsync($"api/v1/PublicStatus/Embed/{page.Slug}", TestContext.CancellationToken), "The embed shows nothing while all is well.");

        await using var visitor = server.CreateAppClient();
        var publicStatus = visitor.GetController<IPublicStatusController>();
        FollowStatusPageRequestDto browser = new() { Slug = page.Slug, Endpoint = "https://fcm.googleapis.com/fcm/send/status-page-test", P256dh = "BPublicKeyOfTheBrowser", Auth = "authSecret", Culture = "fa-IR" };

        await publicStatus.Follow(browser, TestContext.CancellationToken);
        Assert.AreEqual(1, (await statusPages.GetStatusPage(project.Id, TestContext.CancellationToken)).FollowerCount);

        await publicStatus.Unfollow(browser, TestContext.CancellationToken);
        Assert.AreEqual(0, (await statusPages.GetStatusPage(project.Id, TestContext.CancellationToken)).FollowerCount);
    }

    [TestMethod]
    public async Task ACustomersHostName_Should_ServeTheirPageAtItsHome_AndBitFabricKeepsItsOwn()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var statusPages = client.GetController<IStatusPagesController>();
        var project = await Connect(client.GetController<IMonitoringsController>(), customerApp, "customer.example");

        var page = await statusPages.GetStatusPage(project.Id, TestContext.CancellationToken);
        page.IsPublished = true;
        page = await statusPages.SaveStatusPage(page, TestContext.CancellationToken);

        // As Cloudflare for SaaS leaves it once the customer's CNAME is in place (See CloudflareCustomHostnames). The test
        // database outlives a run, so each run's host name is its own.
        var hostname = $"status-{Guid.NewGuid():N}.customer.example";
        await using (var scope = server.ApiApp.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().StatusPages.IgnoreQueryFilters()
                .Where(statusPage => statusPage.Slug == page.Slug)
                .ExecuteUpdateAsync(setters => setters.SetProperty(statusPage => statusPage.CustomHostname, hostname)
                                                      .SetProperty(statusPage => statusPage.CustomHostnameState, "active"), TestContext.CancellationToken);
        }

        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = server.WebAppAddress };

        async Task<(HttpStatusCode Status, Uri? Location, string Html)> Get(string path, string? host)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Host = host;
            using var response = await http.SendAsync(request, TestContext.CancellationToken);
            return (response.StatusCode, response.Headers.Location, await response.Content.ReadAsStringAsync(TestContext.CancellationToken));
        }

        var onTheirHost = await Get("/en-US/", hostname);
        Assert.AreEqual(HttpStatusCode.OK, onTheirHost.Status, $"Sent to {onTheirHost.Location}");
        Assert.Contains("status-site", onTheirHost.Html, "Their host's home page is the status page.");
        Assert.Contains("Customer app", onTheirHost.Html);

        var inPersian = await Get("/fa-IR/", hostname);
        Assert.AreEqual(HttpStatusCode.OK, inPersian.Status);
        Assert.Contains("dir=\"rtl\"", inPersian.Html);

        var bitFabricsHome = await Get("/en-US/", null);
        Assert.AreEqual(HttpStatusCode.OK, bitFabricsHome.Status);
        Assert.DoesNotContain("status-site", bitFabricsHome.Html, "bit Fabric's own home page stays its own.");

        var elsewhereOnTheirHost = await Get("/en-US/projects", hostname);
        Assert.DoesNotContain("status-site", elsewhereOnTheirHost.Html, "Only the home page of their host is the status page.");
    }

    [TestMethod]
    public async Task AnAddress_Should_BelongToOnePage_AndOwnHostNamesWaitForCloudflare()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var owner = await SignInWithATeamOfTheirOwn(server);
        var ownersPage = await owner.GetController<IStatusPagesController>().GetStatusPage((await Connect(owner.GetController<IMonitoringsController>(), customerApp, "customer.example")).Id, TestContext.CancellationToken);

        await using var other = await SignInWithATeamOfTheirOwn(server);
        var statusPages = other.GetController<IStatusPagesController>();
        var project = await Connect(other.GetController<IMonitoringsController>(), customerApp, "customer.example");
        var page = await statusPages.GetStatusPage(project.Id, TestContext.CancellationToken);

        Assert.AreNotEqual(ownersPage.Slug, page.Slug, "Another team's page of the same app gets an address of its own.");

        page.Slug = ownersPage.Slug;
        await Assert.ThrowsExactlyAsync<BadRequestException>(() => statusPages.SaveStatusPage(page, TestContext.CancellationToken));

        page.Slug = "Not A Slug!";
        await Assert.ThrowsExactlyAsync<BadRequestException>(() => statusPages.SaveStatusPage(page, TestContext.CancellationToken));

        // The test server has no Cloudflare for SaaS, so the page stays on bit Fabric's address.
        Assert.IsNull(page.CnameTarget);
        await Assert.ThrowsExactlyAsync<BadRequestException>(() => statusPages.ConnectCustomHostname(new() { MonitoringId = project.Id, Hostname = "status.customer.example" }, TestContext.CancellationToken));
    }
}
