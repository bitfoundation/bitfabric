using Bit.Fabric.Server.Api.Features.Monitoring;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Infrastructure.Dtos;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// The bitplatform Network's side of bit Fabric (bitfabric-internal/specs/monitoring.md, Germany, plus the locations the
/// customer picks): the locations a project picks, the targets each location reads with its secret, and the readings it
/// posts back, which become rounds and rollups of their region with the data center they ran in. The Worker itself
/// (src/Network) is typechecked, not run here.
/// </summary>
public partial class ProjectMonitoringTests
{
    private const string NetworkSecret = "the-network-secret-of-the-tests";

    [TestMethod]
    public async Task TheNetworksEndpoints_Should_RefuseAWrongOrMissingSecret_AndBeOffWithoutOne()
    {
        await using var server = await StartServer(new FakeCustomerApp(), NetworkSecret);
        using var http = server.CreateRawHttpClient();

        foreach (var secret in new[] { null, "", "a-wrong-secret", NetworkSecret[..^1] })
        {
            using var targets = await SendAsTheNetwork(http, HttpMethod.Get, "api/v1/MonitoringNetwork/Targets?location=enam", secret);
            Assert.AreEqual(HttpStatusCode.Unauthorized, targets.StatusCode, $"Targets with the secret '{secret}'");

            using var results = await SendAsTheNetwork(http, HttpMethod.Post, "api/v1/MonitoringNetwork/Results", secret,
                                                       new NetworkResultsDto { Location = "enam", Colo = "IAD", Results = [new() { MonitoringId = Guid.NewGuid(), At = DateTimeOffset.UtcNow, ResponseMs = 10 }] });
            Assert.AreEqual(HttpStatusCode.Unauthorized, results.StatusCode, $"Results with the secret '{secret}'");
        }

        using var right = await SendAsTheNetwork(http, HttpMethod.Get, "api/v1/MonitoringNetwork/Targets?location=enam", NetworkSecret);
        Assert.AreEqual(HttpStatusCode.OK, right.StatusCode);

        using var unknownLocation = await SendAsTheNetwork(http, HttpMethod.Get, "api/v1/MonitoringNetwork/Targets?location=sam", NetworkSecret);
        Assert.AreEqual(HttpStatusCode.BadRequest, unknownLocation.StatusCode, "South America isn't a location of the Network.");

        // No secret set: the endpoints aren't there, whatever the caller sends.
        await using var withoutSecret = await StartServer(new FakeCustomerApp());
        using var offHttp = withoutSecret.CreateRawHttpClient();
        using var off = await SendAsTheNetwork(offHttp, HttpMethod.Get, "api/v1/MonitoringNetwork/Targets?location=enam", NetworkSecret);
        Assert.AreEqual(HttpStatusCode.NotFound, off.StatusCode);
    }

    [TestMethod]
    public async Task TheNetworksTargets_Should_BeTheProjectsThatPickedTheLocation()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp, NetworkSecret);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IMonitoringsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        // A new project is checked from Eastern North America and Oceania besides Germany.
        Assert.AreSequenceEqual(new[] { "enam", "oc" }, project.Locations);

        var target = (await Targets(server, "enam")).Single(target => target.MonitoringId == project.Id);
        Assert.AreEqual("https://customer.example/en-US/", target.Url, "The page the home page lands on, as the first round found it, which the Network asks for with a query string of its own.");
        Assert.IsFalse(target.IsServerHealth);
        Assert.Contains(target => target.MonitoringId == project.Id, await Targets(server, "oc"));
        Assert.DoesNotContain(target => target.MonitoringId == project.Id, await Targets(server, "weur"));

        var changed = await projects.SetLocations(new() { MonitoringId = project.Id, Locations = ["weur", "apac-ne", "weur"] }, TestContext.CancellationToken);

        // Each once, in the order the page lists them.
        Assert.AreSequenceEqual(new[] { "weur", "apac-ne" }, changed.Locations);
        Assert.Contains(target => target.MonitoringId == project.Id, await Targets(server, "weur"));
        Assert.Contains(target => target.MonitoringId == project.Id, await Targets(server, "apac-ne"));
        Assert.DoesNotContain(target => target.MonitoringId == project.Id, await Targets(server, "enam"));
        Assert.DoesNotContain(target => target.MonitoringId == project.Id, await Targets(server, "oc"));

        await Assert.ThrowsExactlyAsync<BadRequestException>(() => projects.SetLocations(new() { MonitoringId = project.Id, Locations = ["sam"] }, TestContext.CancellationToken));

        // None at all leaves Germany alone.
        Assert.IsEmpty((await projects.SetLocations(new() { MonitoringId = project.Id, Locations = [] }, TestContext.CancellationToken)).Locations);
        Assert.DoesNotContain(target => target.MonitoringId == project.Id, await Targets(server, "weur"));

        // Another team can't move a project it can't see.
        await using var stranger = await SignInWithATeamOfTheirOwn(server);
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => stranger.GetController<IMonitoringsController>()
            .SetLocations(new() { MonitoringId = project.Id, Locations = ["enam"] }, TestContext.CancellationToken));

        // A project waiting to be reconnected is still timed, as from Germany; one the team disconnected isn't.
        await projects.SetLocations(new() { MonitoringId = project.Id, Locations = ["enam"] }, TestContext.CancellationToken);
        customerApp.Revoke();
        await Probe(server, project.Id);
        Assert.Contains(target => target.MonitoringId == project.Id, await Targets(server, "enam"));

        await projects.Disconnect(project.Id, TestContext.CancellationToken);
        Assert.DoesNotContain(target => target.MonitoringId == project.Id, await Targets(server, "enam"));
    }

    [TestMethod]
    public async Task AStaticFrontEnd_Should_HaveItsServerTimedByTheNetwork_WithNoCacheCheck()
    {
        var customerApp = new FakeCustomerApp { Server = "https://api.customer.example", HasStaticFrontEnd = true };
        await using var server = await StartServer(customerApp, NetworkSecret);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IMonitoringsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        var target = (await Targets(server, "enam")).Single(target => target.MonitoringId == project.Id);
        Assert.AreEqual("https://api.customer.example/health", target.Url);
        Assert.IsTrue(target.IsServerHealth);

        await PostResults(server, new() { Location = "enam", Colo = "IAD", Results = [new() { MonitoringId = project.Id, At = DateTimeOffset.UtcNow, ResponseMs = 40, CacheHit = true }] });

        // A server's /health says nothing of a cache in front of the pages (bitfabric-internal/specs/monitoring.md, Response
        // time).
        Assert.IsFalse((await projects.GetMonitorings(null, TestContext.CancellationToken)).Single().HasCacheWarning);
    }

    [TestMethod]
    public async Task TheNetworksReadings_Should_BecomeRoundsAndRollupsOfTheirRegion_WithTheirDataCenter()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp, NetworkSecret);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IMonitoringsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        var now = DateTimeOffset.UtcNow;

        var answer = await PostResults(server, new()
        {
            Location = "enam",
            Colo = "iad",
            Results =
            [
                new() { MonitoringId = project.Id, At = now.AddMinutes(-2), ResponseMs = null, Error = "timeout" },
                // The same app's cache ignores the query string when asked from there.
                new() { MonitoringId = project.Id, At = now, ResponseMs = 123, CacheHit = true }
            ]
        });
        Assert.AreEqual(2, answer.Accepted);

        var details = await projects.GetMonitoring(project.Id, TestContext.CancellationToken);

        // Germany first, then the Network's locations; every page lists the regions that reported in the last 10 minutes.
        Assert.AreSequenceEqual(new[] { "Germany", "Eastern North America" }, details.Regions);
        var reading = details.NetworkReadings.Single();
        Assert.AreEqual("enam", reading.Location);
        Assert.AreEqual("IAD", reading.Colo, "Every reading names the data center it really ran in.");
        Assert.AreEqual(123, reading.ResponseMs);
        // The report and its parts are still Germany's alone.
        Assert.AreEqual("1.0.0+aaaa111", details.ApplicationVersion);
        Assert.AreEqual(ProjectHealth.Operational, details.Monitoring!.Health);

        Assert.IsTrue((await projects.GetMonitorings(null, TestContext.CancellationToken)).Single().HasCacheWarning);

        // A bar a minute: the home page from Eastern North America, with no report row, since the Network reads none.
        var minutes = await projects.GetPerformance(new() { MonitoringId = project.Id, From = now.AddHours(-1), To = now.AddMinutes(1), StepMinutes = 1 }, TestContext.CancellationToken);
        var home = minutes.Series.Single(series => series.Key == ProjectRollupKeys.HomePage && series.Region == "Eastern North America").Points;
        Assert.AreEqual(123, home[^1].AverageMs);
        Assert.AreEqual(1, home[0].Failed, "No answer is a failed bar.");
        Assert.DoesNotContain(series => series.Key == ProjectRollupKeys.Report && series.Region == "Eastern North America", minutes.Series);

        // The 15 minute rollups count both, as they count Germany's home page.
        var quarters = await projects.GetPerformance(new() { MonitoringId = project.Id, From = now.AddHours(-1), To = now.AddMinutes(1), StepMinutes = 15 }, TestContext.CancellationToken);
        var homeQuarters = quarters.Series.Single(series => series.Key == ProjectRollupKeys.HomePage && series.Region == "Eastern North America").Points;
        Assert.AreEqual(2, homeQuarters.Sum(point => point.Count));
        Assert.AreEqual(1, homeQuarters.Sum(point => point.Failed));
        Assert.AreEqual(123, homeQuarters.Max(point => point.MaxMs));
        Assert.DoesNotContain(series => series.Key == ProjectRollupKeys.Report && series.Region == "Eastern North America", quarters.Series);

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rounds = await dbContext.ProjectProbes.IgnoreQueryFilters()
                                                  .Where(probe => probe.MonitoringId == project.Id && probe.Colo != null)
                                                  .OrderBy(probe => probe.At)
                                                  .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(2, rounds);
        Assert.IsTrue(rounds.All(round => round.Region == "Eastern North America" && round.Colo == "IAD" && round.Checks.Count is 0 && round.Status is null));
        Assert.AreEqual(ProjectProbeOutcome.Unreachable, rounds[0].Outcome);
        Assert.AreEqual("Your app didn't answer in time.", rounds[0].Problem);
        Assert.AreEqual(ProjectProbeOutcome.Reached, rounds[1].Outcome);
        Assert.IsTrue(rounds[1].HasCacheWarning);
    }

    [TestMethod]
    public async Task AReadingOfAProjectTheLocationDoesntTime_Should_BeIgnored()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp, NetworkSecret);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IMonitoringsController>();
        var project = await Connect(projects, customerApp, "customer.example");
        await projects.SetLocations(new() { MonitoringId = project.Id, Locations = ["weur"] }, TestContext.CancellationToken);

        var now = DateTimeOffset.UtcNow;

        var answer = await PostResults(server, new()
        {
            Location = "enam",
            Colo = "IAD",
            Results =
            [
                // No such project.
                new() { MonitoringId = Guid.NewGuid(), At = now, ResponseMs = 10 },
                // A project that didn't pick Eastern North America.
                new() { MonitoringId = project.Id, At = now, ResponseMs = 10 }
            ]
        });
        Assert.AreEqual(0, answer.Accepted);

        // From the location it picked, but too long ago to add to its history.
        Assert.AreEqual(0, (await PostResults(server, new() { Location = "weur", Colo = "AMS", Results = [new() { MonitoringId = project.Id, At = now.AddHours(-1), ResponseMs = 10 }] })).Accepted);

        var details = await projects.GetMonitoring(project.Id, TestContext.CancellationToken);
        Assert.IsEmpty(details.NetworkReadings);
        Assert.AreSequenceEqual(new[] { "Germany" }, details.Regions);

        // A reading that doesn't say where it ran is refused whole.
        using var http = server.CreateRawHttpClient();
        using var noColo = await SendAsTheNetwork(http, HttpMethod.Post, "api/v1/MonitoringNetwork/Results", NetworkSecret,
                                                  new NetworkResultsDto { Location = "weur", Colo = "", Results = [new() { MonitoringId = project.Id, At = now, ResponseMs = 10 }] });
        Assert.AreEqual(HttpStatusCode.BadRequest, noColo.StatusCode);
    }

    private async Task<NetworkTargetDto[]> Targets(AppTestServer server, string location)
    {
        using var http = server.CreateRawHttpClient();
        using var response = await SendAsTheNetwork(http, HttpMethod.Get, $"api/v1/MonitoringNetwork/Targets?location={location}", NetworkSecret);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync(AppJsonContext.Default.NetworkTargetDtoArray, TestContext.CancellationToken))!;
    }

    private async Task<NetworkResultsResponseDto> PostResults(AppTestServer server, NetworkResultsDto results)
    {
        using var http = server.CreateRawHttpClient();
        using var response = await SendAsTheNetwork(http, HttpMethod.Post, "api/v1/MonitoringNetwork/Results", NetworkSecret, results);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync(AppJsonContext.Default.NetworkResultsResponseDto, TestContext.CancellationToken))!;
    }

    /// <summary>A request the way the Worker sends it: its secret in a header, its readings as JSON.</summary>
    private async Task<HttpResponseMessage> SendAsTheNetwork(HttpClient http, HttpMethod method, string url, string? secret, NetworkResultsDto? body = null)
    {
        using var request = new HttpRequestMessage(method, url);

        if (secret is not null)
        {
            request.Headers.Add(MonitoringNetworkController.SecretHeader, secret);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, AppJsonContext.Default.NetworkResultsDto);
        }

        return await http.SendAsync(request, TestContext.CancellationToken);
    }
}
