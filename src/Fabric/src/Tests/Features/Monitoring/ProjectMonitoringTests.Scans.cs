using Bit.Fabric.Server.Api.Features.Monitoring;
using Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// The scanners without a browser (bitfabric-internal/specs/scanners.md): what each finds on an app that leaves things open
/// and on one done right, when they run, "this is wrong", another team, and the tools on bit Fabric's own /mcp. The fake
/// app answers what the scanners read, and stands in for the TLS handshake and RDAP (See FakeCustomerApp).
/// </summary>
public partial class ProjectMonitoringTests
{
    [TestMethod]
    public async Task AnAppThatLeavesThingsOpen_Should_GetEveryFinding_RankedUrgentFirst_AndALowScore()
    {
        var now = DateTimeOffset.UtcNow;
        var customerApp = new FakeCustomerApp { Server = "https://api.customer.example", ServesPlainHttp = true };

        customerApp.HomeHeaders["Server"] = "nginx/1.25.3";
        customerApp.HomeHeaders["X-Powered-By"] = "ASP.NET";
        customerApp.HomeCookies.Add("sessionid=abc; path=/");
        customerApp.OpenPaths.UnionWith(["/healthz", "/hangfire", "/dev-mcp", "/scalar", "/openapi/v1.json", "/odata/$metadata"]);
        // A policy in a meta tag that still lets inline scripts and eval run, and can't say frame-ancestors.
        customerApp.HomeHtml = """
            <html><head><meta http-equiv="Content-Security-Policy" content="script-src 'self' 'unsafe-inline' 'unsafe-eval'"><meta name="robots" content="noindex"></head>
            <body><div id="app">Loading...</div><img src="/logo.png"></body></html>
            """;
        customerApp.RobotsTxt = "User-agent: *\nDisallow: /\n";
        customerApp.Sitemap = $"<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><url><loc>{customerApp.Address}/en-US/a</loc></url><url><loc>{customerApp.Address}/en-US/b</loc></url></urlset>";
        customerApp.Pages["/en-US/a"] = customerApp.Pages["/en-US/b"] = "<html><head><title>Bluebird</title><meta name=\"description\" content=\"Book a table.\"></head><body><p>Hi</p></body></html>";
        customerApp.Certificates["customer.example"] = new(now.AddDays(10.5), NameMatches: true, ChainTrusted: true);
        customerApp.Certificates["api.customer.example"] = new(now.AddDays(20.5), NameMatches: false, ChainTrusted: false);
        customerApp.Registrations["customer.example"] = customerApp.Registrations["api.customer.example"] = new("customer.example", now.AddDays(25.5));

        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");
        await RunQueuedScans(server, project.Id);

        var scans = await client.GetController<IProjectScansController>().GetScans(project.Id, TestContext.CancellationToken);

        var security = scans.Latest.Single(scan => scan.Kind is ProjectScanKind.Security);
        Assert.AreEqual(ProjectScanOutcome.Completed, security.Outcome);
        Assert.AreEqual("F", security.Grade);
        Assert.AreEqual(0, security.Score);
        Assert.AreEqual(ProjectScanVerdict.Poor, security.Verdict);
        Assert.AreEqual(ScanFindingSeverity.Urgent, security.Findings[0].Severity, "Ranked urgent first.");
        Assert.IsTrue(security.Findings.Select(finding => finding.Severity).SequenceEqual(security.Findings.Select(finding => finding.Severity).Order()));
        ContainsAll(new[]
        {
            ScanFindings.HttpsRedirectMissing, ScanFindings.HstsMissing, ScanFindings.CspUnsafeInline, ScanFindings.CspUnsafeEval, ScanFindings.FrameProtectionMissing,
            ScanFindings.NosniffMissing, ScanFindings.ReferrerPolicyMissing, ScanFindings.CookieNotSecure, ScanFindings.CookieNotHttpOnly, ScanFindings.CookieNoSameSite,
            ScanFindings.ServerVersionDisclosed, ScanFindings.HealthzOpen, ScanFindings.HangfireOpen, ScanFindings.DevMcpOpen, ScanFindings.ApiDocsOpen, ScanFindings.ODataMetadataOpen
        }, security.Findings.Select(finding => finding.Key).ToArray());
        Assert.DoesNotContain(ScanFindings.CspMissing, security.Findings.Select(finding => finding.Key).ToArray(), "A policy in a meta tag is a policy.");
        // The template's endpoints are read on the server's address, each where it really answered.
        Assert.AreSequenceEqual(new[] { "https://api.customer.example/openapi/v1.json", "https://api.customer.example/scalar" },
                                security.Findings.Where(finding => finding.Key == ScanFindings.ApiDocsOpen).SelectMany(finding => finding.Where).Order(StringComparer.Ordinal).ToArray());
        Assert.AreEqual("sessionid", security.Findings.Single(finding => finding.Key == ScanFindings.CookieNotHttpOnly).Parameters.Single());
        Assert.Contains("nginx/1.25.3", security.Findings.Single(finding => finding.Key == ScanFindings.ServerVersionDisclosed).Parameters.Single());
        Assert.DoesNotContain(url => url.Contains("/oauth/", StringComparison.Ordinal) || url.Contains("/identity/", StringComparison.OrdinalIgnoreCase), customerApp.ScanRequested,
                              "No scan signs in or tries a password.");

        var seo = scans.Latest.Single(scan => scan.Kind is ProjectScanKind.Seo);
        Assert.AreEqual(0, seo.Score);
        Assert.AreEqual(ProjectScanVerdict.Poor, seo.Verdict);
        string[] seoKeys = [.. seo.Findings.Select(finding => finding.Key!)];
        ContainsAll(new[]
        {
            ScanFindings.RobotsBlocksAll, ScanFindings.RobotsTxtNoSitemap, ScanFindings.NoIndex, ScanFindings.NotPrerendered, ScanFindings.TitleMissing,
            ScanFindings.DescriptionMissing, ScanFindings.LangMissing, ScanFindings.TitleLength, ScanFindings.DescriptionLength, ScanFindings.DuplicateTitles,
            ScanFindings.DuplicateDescriptions, ScanFindings.CanonicalMissing, ScanFindings.HreflangMissing, ScanFindings.SocialPreviewMissing, ScanFindings.ImagesWithoutAlt
        }, seoKeys);
        Assert.DoesNotContain(ScanFindings.SitemapMissing, seoKeys, "The sitemap is at /sitemap.xml, though robots.txt doesn't name it.");
        Assert.AreSequenceEqual(new[] { "/en-US/" }, seo.Findings.Single(finding => finding.Key == ScanFindings.NoIndex).Where);
        Assert.AreSequenceEqual(new[] { "/en-US/a", "/en-US/b" }, seo.Findings.Single(finding => finding.Key == ScanFindings.DuplicateTitles).Where);
        Assert.AreEqual("Bluebird", seo.Findings.Single(finding => finding.Key == ScanFindings.DuplicateTitles).Parameters.Single());
        Assert.AreEqual("1", seo.Findings.Single(finding => finding.Key == ScanFindings.ImagesWithoutAlt).Parameters.Single());

        var domain = scans.Latest.Single(scan => scan.Kind is ProjectScanKind.DomainAndCertificate);
        Assert.AreEqual(ProjectScanVerdict.Poor, domain.Verdict);
        Assert.IsLessThan(1, Math.Abs((domain.ExpiresAt!.Value - now.AddDays(10.5)).TotalMinutes), "The first to end: the web host's certificate.");
        var expiring = domain.Findings.Where(finding => finding.Key == ScanFindings.CertificateExpiresSoon).ToDictionary(finding => finding.Parameters[0]);
        Assert.AreEqual(ScanFindingSeverity.Urgent, expiring["customer.example"].Severity, "Under 14 days.");
        Assert.AreEqual("10", expiring["customer.example"].Parameters[1]);
        Assert.AreEqual(ScanFindingSeverity.Important, expiring["api.customer.example"].Severity, "Under 30 days.");
        Assert.Contains(finding => finding.Key == ScanFindings.CertificateNameMismatch && finding.Parameters[0] == "api.customer.example", domain.Findings);
        Assert.Contains(finding => finding.Key == ScanFindings.CertificateChainInvalid && finding.Parameters[0] == "api.customer.example", domain.Findings);
        var registration = domain.Findings.Single(finding => finding.Key == ScanFindings.DomainExpiresSoon);
        Assert.AreSequenceEqual(new[] { "customer.example", "25" }, registration.Parameters, "Both hosts are under one registration, reported once.");
        Assert.AreEqual(ScanFindingSeverity.Important, registration.Severity);

        // Every finding reads in the viewer's language from its key, and comes with a prompt in English for their AI agent.
        var localizer = server.ApiApp.Services.GetRequiredService<IStringLocalizer<AppStrings>>();
        foreach (var finding in scans.Latest.SelectMany(scan => scan.Findings))
        {
            Assert.IsNotNull(ScanFindings.Localize(localizer, finding), $"{finding.Key} has texts.");
        }
        var everything = ScanPrompts.ForAll(scans.Address, scans.Latest.SelectMany(scan => scan.Findings.Select(finding => (scan.Kind, finding))));
        Assert.StartsWith("bit Fabric scanned my app at https://customer.example", everything);
        Assert.Contains("run the build and the tests", everything);
        Assert.IsLessThan(everything.IndexOf("[important", StringComparison.Ordinal), everything.IndexOf("[urgent", StringComparison.Ordinal), "Urgent ones come first.");
        Assert.IsLessThan(everything.IndexOf("[improvement", StringComparison.Ordinal), everything.IndexOf("[important", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AnAppDoneRight_Should_GetTopMarks_AndNothingToFix()
    {
        var customerApp = new FakeCustomerApp().DoneRight();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");
        await RunQueuedScans(server, project.Id);

        var scans = await client.GetController<IProjectScansController>().GetScans(project.Id, TestContext.CancellationToken);

        Assert.AreSequenceEqual(ScanFindings.AvailableKinds, scans.Latest.Select(scan => scan.Kind).ToArray());
        Assert.IsTrue(scans.Latest.All(scan => scan.Outcome is ProjectScanOutcome.Completed && scan.Verdict is ProjectScanVerdict.Good),
                      string.Join(", ", scans.Latest.SelectMany(scan => scan.Findings).Select(finding => finding.Key)));
        Assert.IsEmpty(scans.Latest.SelectMany(scan => scan.Findings));

        var security = scans.Latest.Single(scan => scan.Kind is ProjectScanKind.Security);
        Assert.AreEqual("A+", security.Grade);
        Assert.AreEqual(110, security.Score, "100, and Observatory's bonuses for a private Referrer-Policy and cookies that all say SameSite.");
        Assert.AreEqual(100, scans.Latest.Single(scan => scan.Kind is ProjectScanKind.Seo).Score);
        Assert.IsLessThan(1, Math.Abs((scans.Latest.Single(scan => scan.Kind is ProjectScanKind.DomainAndCertificate).ExpiresAt!.Value - DateTimeOffset.UtcNow.AddDays(60)).TotalHours));
        Assert.AreEqual(99, scans.Latest.Single(scan => scan.Kind is ProjectScanKind.Performance).Score, "The phone's 99, the lower of the phone's and the computer's.");
        Assert.AreEqual(100, scans.Latest.Single(scan => scan.Kind is ProjectScanKind.Accessibility).Score);

        // The SEO scan read the home page and the sitemap that robots.txt names through the sitemap index.
        Assert.Contains("https://customer.example/sitemap_index.xml", customerApp.ScanRequested);
        Assert.Contains("https://customer.example/en-US/about", customerApp.ScanRequested);

        // The card on the projects page shows each scan, as the approved design draws it.
        var card = (await client.GetController<IProjectsController>().GetProjects(null, TestContext.CancellationToken)).Single();
        Assert.AreEqual("A+", card.Scans.Single(scan => scan.Kind is ProjectScanKind.Security).Grade);
        Assert.IsTrue(card.Scans.All(scan => scan.FinishedAt is not null && scan.TopFinding is null));
    }

    [TestMethod]
    public async Task AProjectJustConnected_Should_GetItsFirstScans_AndItsCardShowThem()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");
        var scansController = client.GetController<IProjectScansController>();

        // Asked for the moment it's connected, one of each kind, the browser scans included.
        var asked = await scansController.GetScans(project.Id, TestContext.CancellationToken);
        Assert.AreEqual(5, asked.Pending.Length + asked.Latest.Length);
        Assert.IsTrue(asked.Pending.Concat(asked.Latest).All(scan => scan.Trigger is ProjectScanTrigger.Connected));

        await RunQueuedScans(server, project.Id);

        var history = await scansController.GetHistory(new() { ProjectId = project.Id }, TestContext.CancellationToken);
        Assert.HasCount(5, history);
        Assert.IsTrue(history.All(scan => scan.Trigger is ProjectScanTrigger.Connected));

        // The fake app sends none of the headers a policy needs: its card's tooltip is the most urgent of them.
        var card = (await client.GetController<IProjectsController>().GetProjects(null, TestContext.CancellationToken)).Single();
        var security = card.Scans.Single(scan => scan.Kind is ProjectScanKind.Security);
        Assert.IsGreaterThan(0, security.FindingCount);
        Assert.AreEqual(ScanFindingSeverity.Important, security.TopFinding!.Severity);
        Assert.DoesNotContain(scan => scan.IsRunning, card.Scans);
        Assert.AreSequenceEqual(ScanFindings.AvailableKinds, card.Scans.Select(scan => scan.Kind).ToArray(), "As the approved card design orders them.");
    }

    [TestMethod]
    public async Task ADeployment_Should_BeScanned_OnlyOnceTheOwnerAllowsIt()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");
        var scansController = client.GetController<IProjectScansController>();
        await RunQueuedScans(server, project.Id);

        var settings = await scansController.GetSettings(project.Id, TestContext.CancellationToken);
        Assert.IsFalse(settings.ScanAfterDeployments, "Off until the owner allows it.");
        Assert.IsTrue(settings.WeeklyScans);

        customerApp.Report.ApplicationVersion = "1.0.1+bbbb222";
        customerApp.Report.StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        await Probe(server, project.Id);
        await RunQueuedScans(server, project.Id);

        Assert.DoesNotContain(scan => scan.Trigger is ProjectScanTrigger.Deployment, await scansController.GetHistory(new() { ProjectId = project.Id }, TestContext.CancellationToken));

        settings.ScanAfterDeployments = true;
        var saved = await scansController.SaveSettings(settings, TestContext.CancellationToken);
        Assert.IsTrue(saved.ScanAfterDeployments);

        // Changed again from a stale copy: someone else saved them since.
        settings.WeeklyScans = false;
        await Assert.ThrowsExactlyAsync<ConflictException>(() => scansController.SaveSettings(settings, TestContext.CancellationToken));
        Assert.IsTrue((await scansController.GetSettings(project.Id, TestContext.CancellationToken)).WeeklyScans);

        customerApp.Report.ApplicationVersion = "1.0.2+cccc333";
        customerApp.Report.StartedAt = DateTimeOffset.UtcNow;
        await Probe(server, project.Id);
        await RunQueuedScans(server, project.Id);

        var deployment = (await scansController.GetHistory(new() { ProjectId = project.Id }, TestContext.CancellationToken)).Where(scan => scan.Trigger is ProjectScanTrigger.Deployment).ToArray();
        Assert.HasCount(5, deployment);
        Assert.IsTrue(deployment.All(scan => scan.ApplicationVersion == "1.0.2+cccc333" && scan.FinishedAt is not null), "The build the deployment brought.");

        // The same build seen again, from the next round, is no new deployment.
        await Probe(server, project.Id);
        Assert.IsEmpty((await scansController.GetScans(project.Id, TestContext.CancellationToken)).Pending);
    }

    [TestMethod]
    public async Task RunScansNow_Should_StartEveryKind_AtMostOnceEvery10Minutes()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");
        var scansController = client.GetController<IProjectScansController>();
        await RunQueuedScans(server, project.Id);

        Assert.IsNull((await scansController.GetScans(project.Id, TestContext.CancellationToken)).CanRunAgainAt);

        var started = await scansController.RunNow(new() { ProjectId = project.Id }, TestContext.CancellationToken);
        Assert.HasCount(5, started.Pending);
        Assert.IsTrue(started.Pending.All(scan => scan.Trigger is ProjectScanTrigger.Manual));
        Assert.IsNotNull(started.CanRunAgainAt);
        Assert.IsLessThan(TimeSpan.FromMinutes(10.1), started.CanRunAgainAt.Value - DateTimeOffset.UtcNow);

        await Assert.ThrowsExactlyAsync<TooManyRequestsException>(() => scansController.RunNow(new() { ProjectId = project.Id }, TestContext.CancellationToken));

        await RunQueuedScans(server, project.Id);
        var history = await scansController.GetHistory(new() { ProjectId = project.Id, Kind = ProjectScanKind.Security }, TestContext.CancellationToken);
        Assert.AreSequenceEqual(new[] { ProjectScanTrigger.Manual, ProjectScanTrigger.Connected }, history.Select(scan => scan.Trigger).ToArray(), "One kind's runs, newest first.");
    }

    [TestMethod]
    public async Task AFindingSaidToBeWrong_Should_BeRecorded_WhoWhenAndWhich()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");
        var scansController = client.GetController<IProjectScansController>();
        await RunQueuedScans(server, project.Id);

        var finding = (await scansController.GetScans(project.Id, TestContext.CancellationToken)).Latest.SelectMany(scan => scan.Findings).First(finding => finding.Key == ScanFindings.CspMissing);
        Assert.IsFalse(finding.IsReportedWrong);

        await scansController.ReportWrong(new() { FindingId = finding.Id, Note = "Our CDN adds the policy." }, TestContext.CancellationToken);
        await scansController.ReportWrong(new() { FindingId = finding.Id }, TestContext.CancellationToken);

        var reported = (await scansController.GetScans(project.Id, TestContext.CancellationToken)).Latest.SelectMany(scan => scan.Findings).Single(item => item.Id == finding.Id);
        Assert.IsTrue(reported.IsReportedWrong);

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var reports = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProjectScanFindingReports.IgnoreQueryFilters()
                                 .Where(report => report.FindingId == finding.Id).ToListAsync(TestContext.CancellationToken);
        var user = await client.GetController<IUserController>().GetCurrentUser(TestContext.CancellationToken);

        var row = reports.Single(); // Said twice by one member: one report.
        Assert.AreEqual(user.Id, row.UserId);
        Assert.AreEqual(ScanFindings.CspMissing, row.FindingKey);
        Assert.AreEqual(ProjectScanKind.Security, row.Kind);
        Assert.AreEqual("Our CDN adds the policy.", row.Note);
        Assert.IsLessThan(1, Math.Abs((DateTimeOffset.UtcNow - row.ReportedAt).TotalMinutes));
    }

    [TestMethod]
    public async Task AnotherTeam_Should_SeeNoneOfAProjectsScans()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var owner = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(owner.GetController<IProjectsController>(), customerApp, "customer.example");
        await RunQueuedScans(server, project.Id);
        var latest = (await owner.GetController<IProjectScansController>().GetScans(project.Id, TestContext.CancellationToken)).Latest;
        var findingId = latest.SelectMany(scan => scan.Findings).First().Id;

        await using var stranger = await SignInWithATeamOfTheirOwn(server);
        var scans = stranger.GetController<IProjectScansController>();

        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => scans.GetScans(project.Id, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => scans.GetHistory(new() { ProjectId = project.Id }, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => scans.GetScan(latest[0].Id, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => scans.RunNow(new() { ProjectId = project.Id }, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => scans.GetSettings(project.Id, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => scans.SaveSettings(new() { ProjectId = project.Id, ScanAfterDeployments = true }, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => scans.ReportWrong(new() { FindingId = findingId }, TestContext.CancellationToken));
    }

    /// <summary>
    /// Every feature gets tools on bit Fabric's own /mcp. Called over the real endpoint
    /// with the user's own access token, as their AI agent would, and with a stranger's.
    /// </summary>
    [TestMethod]
    public async Task TheMcpTools_Should_BeListed_AndReadTheFindings_OfTheCallersTeamOnly()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");
        await RunQueuedScans(server, project.Id);

        await using var mcp = await McpClientOf(server, client);

        var tools = await mcp.ListToolsAsync(cancellationToken: TestContext.CancellationToken);
        ContainsAll(new[] { "GetProjectScans", "GetProjectScanHistory", "RunProjectScans" }, tools.Select(tool => tool.Name).ToArray());
        Assert.Contains("confirm", tools.Single(tool => tool.Name == "RunProjectScans").Description!, "The model asks the user before it runs them.");

        // Found by the address the user says, the way they say it.
        var latest = await Text(mcp, "GetProjectScans", new() { ["project"] = "customer.example" });
        Assert.Contains("\"key\": \"CspMissing\"", latest);
        Assert.Contains("No Content-Security-Policy is set", latest, "In the caller's language, from the finding's key.");
        Assert.Contains("promptForAnAIAgent", latest);
        Assert.Contains("fixEverythingPrompt", latest);

        var history = await Text(mcp, "GetProjectScanHistory", new() { ["project"] = project.Id.ToString(), ["kind"] = "Security" });
        Assert.Contains("\"trigger\": \"Connected\"", history);

        var started = await Text(mcp, "RunProjectScans", new() { ["project"] = "Customer app" });
        Assert.Contains("The scans have started", started);
        Assert.Contains("10 minutes", await Text(mcp, "RunProjectScans", new() { ["project"] = "Customer app" }), "At most once every 10 minutes.");

        await using var stranger = await SignInWithATeamOfTheirOwn(server);
        await using var strangersMcp = await McpClientOf(server, stranger);
        var strangers = await Text(strangersMcp, "GetProjectScans", new() { ["project"] = "customer.example" });
        Assert.StartsWith("No project of the user's team matches", strangers);
        Assert.DoesNotContain("CspMissing", strangers);
    }

    /// <summary>
    /// The real RDAP lookup, its http client answered by a stand-in for IANA's bootstrap file and a registry: the longest name
    /// first, the registry's expiration event, and a TLD without RDAP giving nothing rather than a false alarm.
    /// </summary>
    [TestMethod, TestCategory("UnitTest")]
    public async Task ADomainsRegistration_Should_BeReadOverRdap_AndATldWithoutRdap_GiveNothing()
    {
        var expiresAt = new DateTimeOffset(2027, 8, 14, 0, 0, 0, TimeSpan.Zero);
        List<string> asked = [];

        using var http = new HttpClient(new RdapStandIn(asked, expiresAt));
        using var cache = new FusionCache(new FusionCacheOptions());
        var lookup = new RdapDomainRegistrationLookup(new SingleClientFactory(http), cache, Microsoft.Extensions.Logging.Abstractions.NullLogger<RdapDomainRegistrationLookup>.Instance);

        var registration = await lookup.Find("api.app.bluebird.test", TestContext.CancellationToken);

        Assert.IsNotNull(registration);
        Assert.AreEqual("bluebird.test", registration.Domain);
        Assert.AreEqual(expiresAt, registration.ExpiresAt);
        Assert.AreSequenceEqual(new[] { RdapDomainRegistrationLookup.BootstrapUrl, "https://rdap.test/domain/api.app.bluebird.test", "https://rdap.test/domain/app.bluebird.test", "https://rdap.test/domain/bluebird.test" },
                                asked.ToArray(), "A registry knows only registered names, so the longest is asked first.");

        Assert.IsNull(await lookup.Find("bluebird.norap", TestContext.CancellationToken), "A TLD the bootstrap has no RDAP server for.");

        // Kept: a second scan asks nobody.
        asked.Clear();
        Assert.AreEqual(expiresAt, (await lookup.Find("bluebird.test", TestContext.CancellationToken))!.ExpiresAt);
        Assert.IsEmpty(asked);
    }

    private static void ContainsAll(string[] expected, string[] actual)
    {
        foreach (var key in expected)
        {
            Assert.Contains(key, actual);
        }
    }

    /// <summary>Runs the project's scans still waiting, here and now, as their Hangfire jobs would (See ScanJobsHeldBack).</summary>
    private async Task RunQueuedScans(AppTestServer server, Guid projectId)
    {
        Guid[] scanIds;

        await using (var scope = server.ApiApp.Services.CreateAsyncScope())
        {
            scanIds = [.. await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProjectScans.IgnoreQueryFilters()
                                     .Where(scan => scan.ProjectId == projectId && scan.FinishedAt == null)
                                     .Select(scan => scan.Id)
                                     .ToListAsync(TestContext.CancellationToken)];
        }

        foreach (var scanId in scanIds)
        {
            await using var scope = server.ApiApp.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ProjectScanJobRunner>().Run(scanId, TestContext.CancellationToken);
        }
    }

    private async Task<McpClient> McpClientOf(AppTestServer server, AppClient client)
    {
        var accessToken = await client.Services.GetRequiredService<IAuthTokenProvider>().GetAccessToken();

        return await McpClient.CreateAsync(new HttpClientTransport(new()
        {
            Endpoint = new Uri(server.ApiAppAddress, "mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {accessToken}" }
        }), cancellationToken: TestContext.CancellationToken);
    }

    private async Task<string> Text(McpClient mcp, string tool, Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(tool, arguments, cancellationToken: TestContext.CancellationToken);

        Assert.AreNotEqual(true, result.IsError, string.Join(" ", result.Content.OfType<TextContentBlock>().Select(block => block.Text)));

        return string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
    }

    /// <summary>IANA's bootstrap file with the <c>test</c> TLD's registry, which knows bluebird.test alone.</summary>
    private sealed class RdapStandIn(List<string> asked, DateTimeOffset expiresAt) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            lock (asked) asked.Add(url);

            var body = url == RdapDomainRegistrationLookup.BootstrapUrl
                ? """{"version":"1.0","services":[[["test","example"],["https://rdap.test/"]],[["com"],["https://rdap.verisign.com/com/v1/"]]]}"""
                : url == "https://rdap.test/domain/bluebird.test"
                    ? $$"""{"objectClassName":"domain","ldhName":"bluebird.test","events":[{"eventAction":"registration","eventDate":"2020-08-14T00:00:00Z"},{"eventAction":"expiration","eventDate":"{{expiresAt:yyyy-MM-ddTHH:mm:ssZ}}"}]}"""
                    : null;

            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/rdap+json") });
        }
    }

    private sealed class SingleClientFactory(HttpClient http) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => http;
    }
}
