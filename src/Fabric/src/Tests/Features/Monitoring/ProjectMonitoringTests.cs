using Bit.Fabric.Server.Api.Features.Monitoring;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Features.Identity;
using Bit.Fabric.Shared.Features.Tenants;
using Bit.Fabric.Tests.Features.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// Adding a customer's app and watching it (bitfabric-internal/specs/monitoring.md), through the app's own api client,
/// against a fake customer app on the monitoring http client (See FakeCustomerApp).
/// </summary>
[TestClass, TestCategory("IntegrationTest")]
public partial class ProjectMonitoringTests
{
    [TestMethod]
    public async Task AProject_Should_BeAddedThroughItsOwnApproval_AndShowItsParts()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();

        var found = await projects.ProbeAddress(new() { Address = "Customer.example/" }, TestContext.CancellationToken);

        Assert.AreEqual(AddressProbeOutcome.Found, found.Outcome);
        Assert.AreEqual("https://customer.example", found.Address);
        Assert.AreEqual("https://customer.example", found.ServerAddress, "The app says where its server is.");
        Assert.IsFalse(found.HasStaticFrontEnd);
        Assert.AreEqual("11.0.0", found.TemplateVersion);
        Assert.AreEqual("Customer app", found.AppTitle, "The app's name comes from its web app manifest.");

        var project = await Connect(projects, customerApp, "customer.example");

        Assert.AreEqual("Customer app", project.Name);
        // The report is Degraded only for a check that misses its settings, which is no outage
        // (bitfabric-internal/specs/monitoring.md, Not configured).
        Assert.AreEqual(ProjectHealth.Operational, project.Health);
        Assert.AreEqual(ProjectConnectionStatus.Connected, project.ConnectionStatus);

        var details = await projects.GetProject(project.Id, TestContext.CancellationToken);

        Assert.AreSequenceEqual(new[] { "AppDbContext", "googleSignIn", "paymentGateway", "smtp" }, details.Checks.Select(check => check.Key).Order(StringComparer.Ordinal).ToArray());
        Assert.AreEqual(5000, details.Checks.Single(check => check.Key == "smtp").TimeoutMs);

        // Each part as the app names it, its own checks included (bitfabric-internal/specs/monitoring.md, Each part's
        // category, name, icon and description).
        var paymentGateway = details.Checks.Single(check => check.Key == "paymentGateway");
        Assert.AreEqual("Integrations", paymentGateway.Category);
        Assert.AreEqual("Payment gateway", paymentGateway.Title);
        Assert.AreEqual("Money", paymentGateway.IconName);
        Assert.AreEqual("Authorizes and voids a one cent payment.", paymentGateway.About);

        Assert.IsTrue(details.Checks.Single(check => check.Key == "googleSignIn").IsNotConfigured);

        // A cached check: its real run's time and when that was, not the cache lookup's 0 ms.
        var smtp = details.Checks.Single(check => check.Key == "smtp");
        Assert.AreEqual(850, smtp.DurationMs);
        Assert.IsNotNull(smtp.VerifiedAt);

        Assert.AreEqual("1.0.0+aaaa111", details.ApplicationVersion);
        Assert.Contains("Germany", details.Regions);
        Assert.IsNotNull(details.CheckedAt);
        Assert.IsNotEmpty(paymentGateway.Recent, "Each part's card shows its last results.");
    }

    [TestMethod]
    public async Task TwoRoundsOfAProjectAtOnce_Should_BothBeSaved()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);

        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");
        var before = await RoundsOf(server, project.Id);

        // A slow app keeps each round waiting for its answers, so both would read the project before either saved it, as the
        // first round of a connect and the job's can.
        customerApp.Delay = TimeSpan.FromMilliseconds(500);

        await Task.WhenAll(Probe(server, project.Id), Probe(server, project.Id));

        Assert.AreEqual(before + 2, await RoundsOf(server, project.Id));
    }

    [TestMethod]
    public async Task AFailingCheck_Should_ShowOnItsCardAndPage()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        customerApp.Fail("smtp", "The mail server didn't answer within 5 s.");
        await Probe(server, project.Id);

        var card = (await projects.GetProjects(null, TestContext.CancellationToken)).Single();
        Assert.AreEqual(ProjectHealth.Degraded, card.Health);
        // By the name the app gives it; the check that only misses its settings isn't failing.
        Assert.AreSequenceEqual(new[] { "Email" }, card.FailingChecks);

        var smtp = (await projects.GetProject(project.Id, TestContext.CancellationToken)).Checks.Single(check => check.Key == "smtp");
        Assert.AreEqual(HealthCheckStatus.Degraded, smtp.Status);
        Assert.AreEqual("The mail server didn't answer within 5 s.", smtp.Description);
        Assert.IsNotNull(smtp.Since);
    }

    [TestMethod]
    public async Task Performance_Should_ShowEveryRound_AndItsRollupsBeyond()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        customerApp.Fail("smtp", "The mail server didn't answer within 5 s.");
        await Probe(server, project.Id);

        var now = DateTimeOffset.UtcNow;

        // Zoomed in: a bar a minute, and both rounds may share one, in which case the later one shows.
        var minutes = await projects.GetPerformance(new() { ProjectId = project.Id, From = now.AddHours(-1), To = now.AddMinutes(1), StepMinutes = 1 }, TestContext.CancellationToken);
        Assert.AreEqual(1, minutes.StepMinutes);
        Assert.AreEqual(1, minutes.Series.Single(series => series.Key == "smtp").Points[^1].Degraded);
        Assert.Contains(series => series.Key == ProjectRollupKeys.HomePage && series.Region == "Germany", minutes.Series);
        Assert.AreEqual(120, minutes.Series.Single(series => series.Key == "paymentGateway").Points[^1].AverageMs);
        // A reused result's time was counted with the round that saw it run (bitfabric-internal/specs/monitoring.md, A cached
        // check's time is its real run's).
        Assert.IsNull(minutes.Series.Single(series => series.Key == "smtp").Points[^1].AverageMs);
        // A check that only misses its settings has no history to draw.
        Assert.DoesNotContain(series => series.Key == "googleSignIn", minutes.Series);

        // Zoomed out: the 15 minute rollups count both rounds.
        var quarters = await projects.GetPerformance(new() { ProjectId = project.Id, From = now.AddHours(-1), To = now.AddMinutes(1), StepMinutes = 15 }, TestContext.CancellationToken);
        Assert.AreEqual(15, quarters.StepMinutes);
        var smtp = quarters.Series.Single(series => series.Key == "smtp").Points;
        Assert.AreEqual(2, smtp.Sum(point => point.Count));
        Assert.AreEqual(1, smtp.Sum(point => point.Degraded));

        // A month can't be drawn a minute a bar; the bars widen rather than send 43,200 a row.
        var month = await projects.GetPerformance(new() { ProjectId = project.Id, From = now.AddDays(-30), To = now, StepMinutes = 1 }, TestContext.CancellationToken);
        Assert.AreEqual(60, month.StepMinutes);

        // The card's uptime comes from the same rollups: two rounds, the app never down.
        Assert.AreEqual(100, (await projects.GetProjects(null, TestContext.CancellationToken)).Single().Uptime30Days);
    }

    [TestMethod]
    public async Task ANewBuild_Should_BeMarkedAsADeployment_AndItsTemplateVersionReadAgain()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        var deployedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        customerApp.Report.ApplicationVersion = "1.0.1+bbbb222";
        customerApp.Report.StartedAt = deployedAt;
        customerApp.TemplateVersion = "11.0.1";
        await Probe(server, project.Id);

        var details = await projects.GetProject(project.Id, TestContext.CancellationToken);

        Assert.AreEqual("1.0.1+bbbb222", details.ApplicationVersion);
        Assert.AreEqual("11.0.1", (await projects.GetProjects(null, TestContext.CancellationToken)).Single().TemplateVersion, "A deployment can be an upgrade of the template, so its version is read again.");
        Assert.IsNotNull(details.LastDeployedAt);
        Assert.IsLessThan(1, Math.Abs((details.LastDeployedAt.Value - deployedAt).TotalSeconds));
    }

    [TestMethod]
    public async Task ARevokedApproval_Should_AskForReconnecting()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        customerApp.Revoke();
        await Probe(server, project.Id);

        var card = (await projects.GetProjects(null, TestContext.CancellationToken)).Single();
        Assert.AreEqual(ProjectConnectionStatus.ReconnectNeeded, card.ConnectionStatus);
        Assert.AreEqual(ProjectHealth.NoData, card.Health);
        // The app's own reason, not a guess.
        Assert.Contains("new password", card.ConnectionProblem!);
    }

    [TestMethod]
    public async Task AnApiOnAnotherAddress_Should_BeFoundFromWhatTheAppSays()
    {
        var customerApp = new FakeCustomerApp { Server = "https://backend.example.net" };
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();

        var found = await projects.ProbeAddress(new() { Address = "customer.example" }, TestContext.CancellationToken);

        Assert.AreEqual(AddressProbeOutcome.Found, found.Outcome);
        Assert.AreEqual("https://backend.example.net", found.ServerAddress);
        // Nothing is guessed: no home page is read for its links, and no api.<host> is tried.
        Assert.DoesNotContain("https://api.customer.example/.well-known/oauth-protected-resource/healthz", customerApp.Requested);

        var project = await Connect(projects, customerApp, "customer.example");
        Assert.AreEqual("https://backend.example.net", project.ServerAddress);
        Assert.AreEqual(ProjectHealth.Operational, project.Health);
        Assert.Contains("https://customer.example/en-US/", customerApp.Requested, "A server renders its pages, so a visitor's wait is timed on its home page.");
    }

    [TestMethod]
    public async Task AStaticFrontEnd_Should_HaveItsServerTimedInstead()
    {
        var customerApp = new FakeCustomerApp { Server = "https://api.customer.example", HasStaticFrontEnd = true };
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();

        var found = await projects.ProbeAddress(new() { Address = "customer.example" }, TestContext.CancellationToken);
        Assert.IsTrue(found.HasStaticFrontEnd, "The app says its pages are hosted statically.");

        await Connect(projects, customerApp, "customer.example");

        Assert.Contains("https://api.customer.example/health", customerApp.Requested);
        Assert.DoesNotContain("https://customer.example/en-US/", customerApp.Requested);
    }

    [TestMethod]
    public async Task AnAddressWithoutAHealthReport_Should_BeUnknown_AndOneThatDoesntAnswer_Unreachable()
    {
        var customerApp = new FakeCustomerApp { ServesBitApp = false };
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();

        // An app answers without saying it's a bit app: "unknown", never "too old".
        Assert.AreEqual(AddressProbeOutcome.NotFound, (await projects.ProbeAddress(new() { Address = "customer.example" }, TestContext.CancellationToken)).Outcome);

        // It says where its server is, but the server has no health report to approve.
        (customerApp.ServesBitApp, customerApp.ServesMetadata) = (true, false);
        customerApp.Requested.Clear();
        Assert.AreEqual(AddressProbeOutcome.NotFound, (await projects.ProbeAddress(new() { Address = "customer.example" }, TestContext.CancellationToken)).Outcome);

        customerApp.Answers = false;
        customerApp.Requested.Clear();
        Assert.AreEqual(AddressProbeOutcome.Unreachable, (await projects.ProbeAddress(new() { Address = "customer.example" }, TestContext.CancellationToken)).Outcome);

        // One attempt each, as a visitor makes it: the app-wide retries and circuit breaker would multiply the requests, distort
        // the timings, and let one app that doesn't answer open the breaker every customer's app shares (MonitoringHttp).
        Assert.AreSequenceEqual(new[] { "https://customer.example/.well-known/bit-app.json" },
                                customerApp.Requested.Where(url => url.Contains("/.well-known/", StringComparison.Ordinal)).ToArray());

        await Assert.ThrowsExactlyAsync<BadRequestException>(() => projects.StartConnect(new() { Address = "customer.example" }, TestContext.CancellationToken));
    }

    /// <summary>
    /// The approval opens beside the add page, and the customer's app sends its answer to bit Fabric's api, which hands it
    /// to that page through web-interop-app.html, the way external sign-in does (bitfabric-internal/specs/monitoring.md, The
    /// approval opens beside the page).
    /// </summary>
    [TestMethod]
    public async Task TheApprovalsAnswer_Should_GoBackToThePageThatOpenedIt()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();

        using var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });

        async Task<string> AnswerFor(int? localHttpPort)
        {
            var start = await projects.StartConnect(new() { Address = "customer.example", LocalHttpPort = localHttpPort, LocalHttpNonce = localHttpPort is null ? null : "nonce" }, TestContext.CancellationToken);
            var authorize = QueryHelpers.ParseQuery(new Uri(start.AuthorizeUrl!).Query);

            using var answer = await browser.GetAsync(QueryHelpers.AddQueryString(authorize["redirect_uri"]!, new Dictionary<string, string?> { ["state"] = authorize["state"], ["code"] = "approved", ["iss"] = customerApp.Server }), TestContext.CancellationToken);

            Assert.AreEqual(HttpStatusCode.Redirect, answer.StatusCode);
            return answer.Headers.Location!.ToString();
        }

        // On the web: to the window that opened the approval.
        var web = new Uri(await AnswerFor(localHttpPort: null));
        Assert.AreEqual(PageUrls.WebInteropApp, web.AbsolutePath);
        var interop = QueryHelpers.ParseQuery(web.Query);
        Assert.AreEqual("ExternalSignInCallback", interop["actionName"].ToString());
        Assert.StartsWith($"{PageUrls.AddProjectCallback}?state=", interop["url"].ToString(), "The add page's own address, which it recognizes.");
        Assert.Contains("code=approved", interop["url"].ToString());

        // In a native app: to its local server, which hands it to the app.
        var native = new Uri(await AnswerFor(localHttpPort: 41234));
        Assert.AreEqual("http://localhost:41234", native.GetLeftPart(UriPartial.Authority));
        Assert.AreEqual("nonce", QueryHelpers.ParseQuery(native.Query)["localHttpNonce"].ToString());
    }

    [TestMethod]
    public async Task ARound_Should_TimeOneRequest_ThePageTheHomePageLandsOn()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        // The server's time is asked for past any cache (?bf=), then the page as a visitor asks for it.
        string[] HomePages() => [.. customerApp.Requested.Where(url => url is "https://customer.example/" or "https://customer.example/en-US/" or "https://customer.example/fa-IR/")];
        string[] Kinds() => [.. customerApp.HomeRequested.Select(url => $"{url.AbsolutePath}{(QueryHelpers.ParseQuery(url.Query).ContainsKey("bf") ? " past the cache" : "")}")];

        // The first round followed / to /en-US/; the next asks for /en-US/ straight away.
        customerApp.Requested.Clear();
        customerApp.HomeRequested.Clear();
        await Probe(server, project.Id);
        Assert.AreSequenceEqual(new[] { "https://customer.example/en-US/", "https://customer.example/en-US/" }, HomePages());
        Assert.AreSequenceEqual(new[] { "/en-US/ past the cache", "/en-US/" }, Kinds());

        // The app lands somewhere else now: that round follows it and times the request after it, and the next asks for the new page.
        customerApp.Landing = "/fa-IR/";
        customerApp.Requested.Clear();
        await Probe(server, project.Id);
        Assert.AreSequenceEqual(new[] { "https://customer.example/en-US/", "https://customer.example/fa-IR/", "https://customer.example/fa-IR/" }, HomePages());

        customerApp.Requested.Clear();
        await Probe(server, project.Id);
        Assert.AreSequenceEqual(new[] { "https://customer.example/fa-IR/", "https://customer.example/fa-IR/" }, HomePages());

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var rounds = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProjectProbes.IgnoreQueryFilters()
            .Where(probe => probe.ProjectId == project.Id).ToListAsync(TestContext.CancellationToken);
        Assert.IsTrue(rounds.All(round => round.ResponseMs is not null), "Every round timed the page, the one that was redirected too.");
    }

    [TestMethod]
    public async Task ACacheInFrontOfTheApp_Should_GiveTwoNumbers_WhatVisitorsWaitFor_AndTheServersOwn()
    {
        var customerApp = new FakeCustomerApp { CachesHomePage = true };
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();
        var project = await Connect(projects, customerApp, "customer.example");
        await Probe(server, project.Id);

        var shown = (await projects.GetProjects(null, TestContext.CancellationToken)).Single();
        Assert.IsNotNull(shown.MedianMs, "The server's own time, past the cache.");
        Assert.IsNotNull(shown.MedianVisitorMs, "What a visitor waits for, through the cache.");
        Assert.IsNotNull((await projects.GetProject(project.Id, TestContext.CancellationToken)).HomePageVisitorMs);

        var performance = await projects.GetPerformance(new() { ProjectId = project.Id, From = DateTimeOffset.UtcNow.AddHours(-1), To = DateTimeOffset.UtcNow.AddMinutes(1), StepMinutes = 1 }, TestContext.CancellationToken);
        Assert.Contains(series => series.Key == ProjectRollupKeys.HomePageForVisitors && series.Points.Length > 0, performance.Series);

        var statusPages = client.GetController<IStatusPagesController>();
        var page = await statusPages.GetStatusPage(project.Id, TestContext.CancellationToken);
        (page.IsPublished, page.ShowsResponseTime) = (true, true);
        page = await statusPages.SaveStatusPage(page, TestContext.CancellationToken);
        await using var visitor = server.CreateAppClient();
        var status = await visitor.GetController<IPublicStatusController>().Get(page.Slug!, "en-US", TestContext.CancellationToken);
        Assert.IsNotNull(status.MedianResponseMs);
        Assert.IsNotNull(status.MedianVisitorMs);

        // Without a cache the two would be one number, so there is one.
        customerApp.CachesHomePage = false;
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.ProjectProbes.IgnoreQueryFilters().Where(probe => probe.ProjectId == project.Id).ExecuteDeleteAsync(TestContext.CancellationToken);
        await Probe(server, project.Id);
        shown = (await projects.GetProjects(null, TestContext.CancellationToken)).Single();
        Assert.IsNotNull(shown.MedianMs);
        Assert.IsNull(shown.MedianVisitorMs);
    }

    [TestMethod, TestCategory("UnitTest")]
    public void ARoundsMoment_Should_BeTheProjectsOwn_WithinTheSpread()
    {
        var spread = TimeSpan.FromSeconds(30);
        var projectIds = Enumerable.Range(0, 300).Select(_ => Guid.CreateVersion7()).ToArray();
        var moments = projectIds.Select(id => ProjectProbeJobRunner.OffsetOf(id, spread)).ToArray();

        Assert.IsTrue(moments.All(moment => moment >= TimeSpan.Zero && moment < spread), "Each within the spread.");
        Assert.AreSequenceEqual(moments, projectIds.Select(id => ProjectProbeJobRunner.OffsetOf(id, spread)).ToArray(), "The same moment every minute.");
        Assert.IsGreaterThan(25, moments.Select(moment => (int)moment.TotalSeconds).Distinct().Count(), "Spread over the half minute, ids made in the same millisecond included.");
        Assert.AreEqual(TimeSpan.Zero, ProjectProbeJobRunner.OffsetOf(projectIds[0], TimeSpan.Zero), "No spread: all at once, as before.");
    }

    [TestMethod]
    public async Task ACacheThatIgnoresTheQueryString_Should_BeReported()
    {
        var customerApp = new FakeCustomerApp { CachesIgnoringQueryString = true };
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IProjectsController>();
        await Connect(projects, customerApp, "customer.example");

        Assert.IsTrue((await projects.GetProjects(null, TestContext.CancellationToken)).Single().HasCacheWarning);
    }

    [TestMethod]
    public async Task AnotherTeam_Should_SeeNoneOfAProject()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var owner = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(owner.GetController<IProjectsController>(), customerApp, "customer.example");

        await using var stranger = await SignInWithATeamOfTheirOwn(server);
        var strangersProjects = stranger.GetController<IProjectsController>();

        Assert.IsEmpty(await strangersProjects.GetProjects(null, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => strangersProjects.GetProject(project.Id, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => strangersProjects.GetPerformance(new() { ProjectId = project.Id, From = DateTimeOffset.UtcNow.AddHours(-1), To = DateTimeOffset.UtcNow, StepMinutes = 1 }, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => strangersProjects.Delete(project.Id, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Projects_Should_BeRefused_WithoutATeam()
    {
        await using var server = await StartServer(new FakeCustomerApp());
        var email = await TestUsers.SignUp(server, TestContext.CancellationToken);
        await using var client = server.CreateAppClient();
        await client.AuthManager.SignIn(new() { Email = email, Password = TestUsers.Password }, TestContext.CancellationToken);

        // Without a tenant of their own a project would land in the fallback tenant every tenant-less user shares.
        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => client.GetController<IProjectsController>().GetProjects(null, TestContext.CancellationToken));
    }

    /// <param name="customerApp">Answers every request bit Fabric makes to a customer's app.</param>
    /// <param name="networkSecret">The bitplatform Network's secret (See MonitoringNetworkController); null leaves its endpoints off, as by default.</param>
    private async Task<AppTestServer> StartServer(FakeCustomerApp customerApp, string? networkSecret = null, FakeLighthouse? lighthouse = null)
    {
        lighthouse ??= new FakeLighthouse();

        var server = new AppTestServer();

        await server.Build(configureTestApiAppServices: services =>
        {
            // Hangfire activates a job in whichever test server configured it last, so a scan it ran could read another
            // test's fake app. The tests run the scans they ask for themselves (See RunQueuedScans), and read the emails
            // handed to the email service rather than send them (See HeldEmails).
            var jobClient = services.Last(descriptor => descriptor.ServiceType == typeof(Hangfire.IBackgroundJobClient));
            services.AddSingleton<HeldEmails>();
            services.Replace(ServiceDescriptor.Singleton<Hangfire.IBackgroundJobClient>(serviceProvider => new ScanJobsHeldBack((Hangfire.IBackgroundJobClient)(jobClient.ImplementationInstance
                ?? jobClient.ImplementationFactory?.Invoke(serviceProvider)
                ?? ActivatorUtilities.CreateInstance(serviceProvider, jobClient.ImplementationType!)), serviceProvider.GetRequiredService<HeldEmails>())));
        }, configureTestServices: services =>
        {
            // Last in the pipeline but the socket: every request to a customer's app is answered by the fake.
            services.AddHttpClient(MonitoringHttp.ClientName).AddHttpMessageHandler(() => new FakeCustomerApp.Handler(customerApp));

            // bit Fabric's Lighthouse service, answering with the runs it recorded.
            services.AddHttpClient(LighthouseClient.ClientName).AddHttpMessageHandler(() => new FakeLighthouse.Handler(lighthouse));

            // The TLS handshake and RDAP of the domain and certificate scan, which the fake app isn't on the network for.
            services.Replace(ServiceDescriptor.Scoped<ICertificateInspector>(_ => new FakeCustomerApp.CertificateStandIn(customerApp)));
            services.Replace(ServiceDescriptor.Scoped<IDomainRegistrationLookup>(_ => new FakeCustomerApp.RegistrationStandIn(customerApp)));
        }, configureTestConfiguration: configuration =>
        {
            configuration["Monitoring:NetworkSecret"] = networkSecret;
            // The tests share one database, whose projects pile up: every location times all of them here.
            configuration["Monitoring:NetworkTargetsPerLocation"] = $"{int.MaxValue}";
            // An AI app such as grok.com, which a user approves bit Fabric's /mcp for (See ProjectMonitoringTests.Mcp). After
            // the client appsettings.Development.json declares.
            configuration["OAuth:Clients:1:ClientId"] = AIAppClientId;
            configuration["OAuth:Clients:1:ClientName"] = "An AI app";
            configuration["OAuth:Clients:1:RedirectUris:0"] = AIAppRedirectUri;
            // A deployment's scans start right away rather than a couple of minutes later.
            configuration["Monitoring:ScanAfterDeploymentDelay"] = "00:00:00";
            configuration["Monitoring:LighthouseUrl"] = FakeLighthouse.Address;
        }).Start(TestContext.CancellationToken);

        return server;
    }

    /// <summary>
    /// Passes every job on to Hangfire but the scans', which a test runs where it can see them (See RunQueuedScans), and the
    /// emails', which it keeps for the test to read (See HeldEmails).
    /// </summary>
    private sealed class ScanJobsHeldBack(Hangfire.IBackgroundJobClient inner, HeldEmails emails) : Hangfire.IBackgroundJobClient
    {
        public string Create(Hangfire.Common.Job job, Hangfire.States.IState state)
        {
            if (job.Type == typeof(ProjectScanJobRunner))
                return $"held-{Guid.NewGuid():N}";

            // SendEmailJob(toEmailAddress, toName, subject, body, ...)
            if (job.Type == typeof(Bit.Fabric.Server.Api.Infrastructure.Services.EmailServiceJobsRunner))
            {
                emails.Add(new((string)job.Args[0]!, (string)job.Args[2]!, (string)job.Args[3]!));
                return $"held-{Guid.NewGuid():N}";
            }

            return inner.Create(job, state);
        }

        public bool ChangeState(string jobId, Hangfire.States.IState state, string expectedState)
            => jobId.StartsWith("held-", StringComparison.Ordinal) || inner.ChangeState(jobId, state, expectedState);
    }

    /// <summary>An email handed to the email service, which sends the sign-in codes too, and never left the test server.</summary>
    public sealed record HeldEmail(string To, string Subject, string Body);

    /// <summary>Every email a test server's api handed to the email service (See ScanJobsHeldBack).</summary>
    public sealed class HeldEmails
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<HeldEmail> held = [];

        public IReadOnlyCollection<HeldEmail> All => held;

        public void Add(HeldEmail email) => held.Enqueue(email);
    }

    /// <summary>A new user in a new team, since tests share one database and a project belongs to a team.</summary>
    private async Task<AppClient> SignInWithATeamOfTheirOwn(AppTestServer server)
    {
        var email = await TestUsers.SignUp(server, TestContext.CancellationToken);
        var client = server.CreateAppClient();
        await client.AuthManager.SignIn(new() { Email = email, Password = TestUsers.Password }, TestContext.CancellationToken);

        // Making a team asks for no elevated access (bitfabric-internal/specs/monitoring.md, Decisions), so the first project
        // isn't held up by a code.
        var team = await client.GetController<ITenantController>().Create(new() { Name = $"t{Guid.NewGuid():N}"[..20], Title = "Test team" }, TestContext.CancellationToken);
        Assert.IsTrue(await client.AuthManager.SwitchTenant(team.Id, TestContext.CancellationToken));

        return client;
    }

    /// <summary>The add flow, the way the browser goes through it: start, the customer app's approval, the callback.</summary>
    private async Task<ProjectDto> Connect(IProjectsController projects, FakeCustomerApp customerApp, string address)
    {
        var start = await projects.StartConnect(new() { Address = address }, TestContext.CancellationToken);
        var authorize = QueryHelpers.ParseQuery(new Uri(start.AuthorizeUrl!).Query);

        Assert.AreEqual("healthz", authorize["scope"].ToString());
        Assert.AreEqual("S256", authorize["code_challenge_method"].ToString());
        Assert.AreEqual($"{customerApp.Server}/healthz", authorize["resource"].ToString());
        Assert.EndsWith(MonitoringOAuthClient.CallbackPath, authorize["redirect_uri"].ToString());

        // The customer approves in their app, which sends the browser back with a code.
        customerApp.Challenge = authorize["code_challenge"];

        return await projects.CompleteConnect(new() { State = authorize["state"], Code = "approved", Iss = customerApp.Server }, TestContext.CancellationToken);
    }

    private async Task Probe(AppTestServer server, Guid projectId)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProjectProber>().Probe(projectId, TestContext.CancellationToken);
    }

    private async Task<int> RoundsOf(AppTestServer server, Guid projectId)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProjectProbes.IgnoreQueryFilters().CountAsync(probe => probe.ProjectId == projectId, TestContext.CancellationToken);
    }

    public TestContext TestContext { get; set; } = default!;
}
