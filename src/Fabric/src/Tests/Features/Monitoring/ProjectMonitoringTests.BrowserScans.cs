using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// The browser scans (bitfabric-internal/specs/scanners.md): Performance and Accessibility from bit Fabric's Lighthouse
/// service, which FakeLighthouse stands in for with runs the real service recorded. A slow, inaccessible app against a good
/// one, the pages and devices measured and the Core Web Vitals kept, one Chrome run a page for both kinds, an app Lighthouse
/// can't load and a service that is down, and the tools on /mcp, for the caller's team only. When they run is the other
/// scans' (ProjectMonitoringTests.Scans), which count these two kinds too.
/// </summary>
public partial class ProjectMonitoringTests
{
    [TestMethod]
    public async Task ASlowInaccessibleApp_Should_GetLighthousesFindings_RankedImportantFirst_AndLowScores()
    {
        var lighthouse = new FakeLighthouse { Recording = FakeLighthouse.SlowApp };
        var customerApp = new FakeCustomerApp();
        customerApp.Sitemap = $"""
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
            <url><loc>{customerApp.Address}/fa-IR/products</loc></url><url><loc>{customerApp.Address}/en-US/</loc></url>
            <url><loc>{customerApp.Address}/en-US/products</loc></url><url><loc>{customerApp.Address}/en-US/about</loc></url><url><loc>{customerApp.Address}/en-US/contact</loc></url>
            </urlset>
            """;

        await using var server = await StartServer(customerApp, lighthouse: lighthouse);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");
        await RunQueuedScans(server, project.Id);

        var scans = await client.GetController<IProjectScansController>().GetScans(project.Id, TestContext.CancellationToken);

        // The home page on a phone and a computer, then two of the sitemap's pages in the home page's culture, on a phone.
        var performance = scans.Latest.Single(scan => scan.Kind is ProjectScanKind.Performance);
        Assert.AreEqual(ProjectScanOutcome.Completed, performance.Outcome);
        Assert.AreSequenceEqual(new[] { ("/en-US/", ProjectScanDevice.Phone), ("/en-US/", ProjectScanDevice.Computer), ("/en-US/products", ProjectScanDevice.Phone), ("/en-US/about", ProjectScanDevice.Phone) },
                                performance.Pages.Select(page => (page.Path!, page.Device)).ToArray());
        Assert.AreEqual(54, performance.Score, "The lowest of the pages and devices: the phone's.");
        Assert.AreEqual(72, performance.Pages[1].Score, "The computer's.");
        Assert.AreEqual(ProjectScanVerdict.NeedsWork, performance.Verdict);

        // The Core Web Vitals as numbers, for the tab and /mcp.
        var homeOnAPhone = performance.Pages[0];
        Assert.AreEqual(4475, homeOnAPhone.LargestContentfulPaintMs);
        Assert.AreEqual(1409, homeOnAPhone.FirstContentfulPaintMs);
        Assert.AreEqual(659, homeOnAPhone.TimeToFirstByteMs);
        Assert.IsGreaterThan(3700, homeOnAPhone.TotalBlockingTimeMs!.Value);
        Assert.IsLessThan(0.01, homeOnAPhone.CumulativeLayoutShift!.Value);

        // What the home page's film promises: images bigger than they need to be, files not cached, scripts holding up the page.
        var findings = performance.Findings.ToDictionary(finding => finding.Key == ScanFindings.PerformanceAuditFailed ? $"{finding.Key}:{finding.Parameters[0]}" : finding.Key!);
        Assert.AreSequenceEqual(new[] { "4.5" }, findings[ScanFindings.LcpSlow].Parameters, "The main content shows after 4.5 s on a phone.");
        Assert.AreEqual(ScanFindingSeverity.Important, findings[ScanFindings.LcpSlow].Severity, "Over Google's 4 s.");
        Assert.AreEqual(ScanFindingSeverity.Important, findings[ScanFindings.TbtHigh].Severity);
        Assert.DoesNotContain(ScanFindings.FcpSlow, findings.Keys.ToArray(), "1.4 s is within Google's 1.8 s.");
        Assert.DoesNotContain(ScanFindings.ClsHigh, findings.Keys.ToArray());
        Assert.AreSequenceEqual(new[] { "47" }, findings[ScanFindings.FilesNotCached].Parameters);
        Assert.AreEqual(ScanFindingSeverity.Important, findings[ScanFindings.FilesNotCached].Severity, "Caching them would make the main content show sooner.");
        Assert.Contains("/en-US/", findings[ScanFindings.FilesNotCached].Where);
        Assert.Contains(where => where.StartsWith("https://customer.example/_content/", StringComparison.Ordinal), findings[ScanFindings.FilesNotCached].Where, "The files, at the app's own address.");
        Assert.AreSequenceEqual(new[] { "250" }, findings[ScanFindings.RenderBlocking].Parameters);
        Assert.AreSequenceEqual(new[] { "259" }, findings[ScanFindings.UnusedCode].Parameters, "Unused CSS and JavaScript and a script not minified, together on the phone.");
        Assert.AreSequenceEqual(new[] { "8.8" }, findings[ScanFindings.MainThreadBusy].Parameters, "The main thread's work, not that and the scripts' added up.");
        Assert.AreSequenceEqual(new[] { "8" }, findings[ScanFindings.ImagesTooBig].Parameters);
        Assert.AreEqual(ScanFindingSeverity.Improvement, findings[ScanFindings.ImagesTooBig].Severity, "Smaller, but no sooner.");
        Assert.AreEqual(ScanFindingSeverity.Improvement, findings[ScanFindings.LayoutShiftCauses].Severity);
        Assert.IsTrue(findings.ContainsKey($"{ScanFindings.PerformanceAuditFailed}:forced-reflow-insight"), "An audit bit Fabric has no words for yet, by its id.");
        Assert.DoesNotContain(key => key.Contains("network-dependency-tree", StringComparison.Ordinal), findings.Keys, "An insight that only explains a metric.");
        Assert.IsTrue(performance.Findings.Select(finding => finding.Severity).SequenceEqual(performance.Findings.Select(finding => finding.Severity).Order()), "Ranked important first.");

        // Text too faint to read, a button without a name and images without alt.
        var accessibility = scans.Latest.Single(scan => scan.Kind is ProjectScanKind.Accessibility);
        Assert.AreEqual(ProjectScanOutcome.Completed, accessibility.Outcome);
        Assert.AreEqual(87, accessibility.Score);
        Assert.AreEqual(ProjectScanVerdict.NeedsWork, accessibility.Verdict);
        Assert.HasCount(4, accessibility.Pages);
        Assert.IsTrue(accessibility.Pages.All(page => page.LargestContentfulPaintMs is null), "The vitals are Performance's.");
        var barriers = accessibility.Findings.ToDictionary(finding => finding.Key!);
        Assert.AreEqual(ScanFindingSeverity.Important, barriers[ScanFindings.ButtonsWithoutName].Severity, "axe-core calls it critical.");
        Assert.AreSequenceEqual(new[] { "2" }, barriers[ScanFindings.ContrastTooLow].Parameters);
        Assert.AreSequenceEqual(new[] { "2" }, barriers[ScanFindings.ImagesWithoutDescription].Parameters);
        Assert.Contains("<img src=\"/images/products/mug.png\" class=\"product-image\">", barriers[ScanFindings.ImagesWithoutDescription].Where, "The element, for the customer's AI to find.");
        Assert.AreSequenceEqual(new[] { "7" }, barriers[ScanFindings.StructureUnclear].Parameters, "The headings out of order and the page without a main landmark, together.");
        Assert.AreEqual(ScanFindingSeverity.Improvement, barriers[ScanFindings.StructureUnclear].Severity);

        // Each in the viewer's language from its key, with a prompt in English for their AI.
        var localizer = server.ApiApp.Services.GetRequiredService<IStringLocalizer<AppStrings>>();
        foreach (var (kind, finding) in new[] { performance, accessibility }.SelectMany(scan => scan.Findings.Select(finding => (scan.Kind, finding))))
        {
            Assert.IsNotNull(ScanFindings.Localize(localizer, finding), $"{finding.Key} has texts.");
            Assert.DoesNotContain("{0}", ScanPrompts.For(scans.Address, kind, finding), $"{finding.Key} has the parameters its texts need.");
        }
        Assert.StartsWith("bit Fabric's performance scan of my app at https://customer.example", ScanPrompts.For(scans.Address, ProjectScanKind.Performance, findings[ScanFindings.LcpSlow]));
        Assert.Contains("The main content takes 4.5 s to show on a phone", ScanPrompts.For(scans.Address, ProjectScanKind.Performance, findings[ScanFindings.LcpSlow]));

        // Both kinds read the same runs: eight requests, four pages loaded in Chrome.
        Assert.HasCount(8, lighthouse.Asked);
        Assert.AreEqual(4, lighthouse.Runs);
        Assert.DoesNotContain(asked => asked.Url.Contains("/fa-IR/", StringComparison.Ordinal), lighthouse.Asked, "The home page's culture first.");

        // The card shows them the way it shows the others.
        var card = (await client.GetController<IProjectsController>().GetProjects(null, TestContext.CancellationToken)).Single();
        Assert.AreEqual(54, card.Scans.Single(scan => scan.Kind is ProjectScanKind.Performance).Score);
        Assert.AreEqual(ScanFindingSeverity.Important, card.Scans.Single(scan => scan.Kind is ProjectScanKind.Accessibility).TopFinding!.Severity);
    }

    [TestMethod]
    public async Task ABrowserScan_Should_SayWhenLighthouseCantLoadTheApp_AndFailWhenTheServiceIsDown()
    {
        var lighthouse = new FakeLighthouse();
        lighthouse.Unloadable.Add("/en-US/");
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp, lighthouse: lighthouse);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");
        var scansController = client.GetController<IProjectScansController>();
        await RunQueuedScans(server, project.Id);

        var latest = (await scansController.GetScans(project.Id, TestContext.CancellationToken)).Latest;
        Assert.AreEqual(ProjectScanOutcome.Unreachable, latest.Single(scan => scan.Kind is ProjectScanKind.Performance).Outcome, "The home page answered an error, so nothing is said about it.");
        Assert.AreEqual(ProjectScanOutcome.Unreachable, latest.Single(scan => scan.Kind is ProjectScanKind.Accessibility).Outcome);
        Assert.AreEqual(ProjectScanOutcome.Completed, latest.Single(scan => scan.Kind is ProjectScanKind.Security).Outcome, "The other scans don't need the browser.");
        Assert.HasCount(1, lighthouse.Asked.Where(asked => asked.FormFactor is "mobile").DistinctBy(asked => asked.Url), "Nothing more once the home page can't be loaded.");

        lighthouse.Unloadable.Clear();
        lighthouse.IsDown = true;
        await scansController.RunNow(new() { ProjectId = project.Id }, TestContext.CancellationToken);
        await RunQueuedScans(server, project.Id);

        latest = (await scansController.GetScans(project.Id, TestContext.CancellationToken)).Latest;
        Assert.AreEqual(ProjectScanOutcome.Failed, latest.Single(scan => scan.Kind is ProjectScanKind.Performance).Outcome, "bit Fabric's own failure, not the app's.");
        Assert.IsNull(latest.Single(scan => scan.Kind is ProjectScanKind.Performance).Verdict);
        Assert.AreEqual(ProjectScanOutcome.Completed, latest.Single(scan => scan.Kind is ProjectScanKind.Seo).Outcome);
    }

    /// <summary>The scans' tools on /mcp give the browser scans' pages and Core Web Vitals, to the caller's team alone.</summary>
    [TestMethod]
    public async Task TheMcpTools_Should_ReturnTheCoreWebVitals_ToTheCallersTeamOnly()
    {
        var lighthouse = new FakeLighthouse { Recording = FakeLighthouse.SlowApp };
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp, lighthouse: lighthouse);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");
        await RunQueuedScans(server, project.Id);

        await using var mcp = await McpClientOf(server, client);

        var tools = await mcp.ListToolsAsync(cancellationToken: TestContext.CancellationToken);
        Assert.Contains("Core Web Vitals", tools.Single(tool => tool.Name == "GetProjectScans").Description!);

        var latest = await Text(mcp, "GetProjectScans", new() { ["project"] = "customer.example" });
        Assert.Contains("\"kind\": \"Performance\"", latest);
        Assert.Contains("\"kind\": \"Accessibility\"", latest);
        Assert.Contains("\"device\": \"Phone\"", latest);
        Assert.Contains("\"device\": \"Computer\"", latest);
        Assert.Contains("\"largestContentfulPaintMs\": 4475", latest);
        Assert.Contains("\"timeToFirstByteMs\": 659", latest);
        Assert.Contains("The main content takes 4.5 s to show on a phone", latest, "In the caller's language, from the finding's key.");
        Assert.Contains("Buttons with no name for screen readers: 1", latest);

        var history = await Text(mcp, "GetProjectScanHistory", new() { ["project"] = project.Id.ToString(), ["kind"] = "Performance" });
        Assert.Contains("\"kind\": \"Performance\"", history);
        Assert.DoesNotContain("\"kind\": \"Security\"", history);
        Assert.Contains("\"largestContentfulPaintMs\"", history, "Each past run's vitals.");

        await using var stranger = await SignInWithATeamOfTheirOwn(server);
        await using var strangersMcp = await McpClientOf(server, stranger);
        var strangers = await Text(strangersMcp, "GetProjectScans", new() { ["project"] = "customer.example" });
        Assert.StartsWith("No project of the user's team matches", strangers);
        Assert.DoesNotContain("largestContentfulPaintMs", strangers);

        var performanceId = (await client.GetController<IProjectScansController>().GetScans(project.Id, TestContext.CancellationToken)).Latest.Single(scan => scan.Kind is ProjectScanKind.Performance).Id;
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => stranger.GetController<IProjectScansController>().GetScan(performanceId, TestContext.CancellationToken));
    }
}
