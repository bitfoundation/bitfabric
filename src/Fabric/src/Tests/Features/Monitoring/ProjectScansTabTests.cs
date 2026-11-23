using Bit.Fabric.Client.Core.Components.Pages.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bunit;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// A project's Scans tab (bitfabric-internal/specs/scanners.md), rendered in memory from a run of each kind: the grade,
/// score and days left, when and why each ran, the findings ranked urgent first with the prompt for the customer's AI and
/// "This is wrong", the prompt that fixes everything, past runs and the two settings in the home page's words. What the
/// api returns is ProjectMonitoringTests.Scans's to check; this checks what the tab makes of it.
/// </summary>
[TestClass, TestCategory("UITest")]
public class ProjectScansTabTests
{
    [TestMethod]
    public async Task TheScansTab_Should_RankWhatToFix_UrgentFirst_AndSendAWrongFindingsReport()
    {
        var projectId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        ProjectScanFindingDto hangfire = new() { Id = Guid.NewGuid(), Key = ScanFindings.HangfireOpen, Severity = ScanFindingSeverity.Urgent, Where = ["https://api.bluebird.app/hangfire"] };
        ProjectScanFindingDto csp = new() { Id = Guid.NewGuid(), Key = ScanFindings.CspMissing, Severity = ScanFindingSeverity.Important, Where = ["/en-US/"] };
        ProjectScanFindingDto images = new() { Id = Guid.NewGuid(), Key = ScanFindings.ImagesWithoutAlt, Severity = ScanFindingSeverity.Improvement, Parameters = ["3"], Where = ["/en-US/about"] };

        ProjectScansDto scans = new()
        {
            ProjectId = projectId,
            Address = "https://bluebird.app",
            Latest =
            [
                Scan(projectId, ProjectScanKind.Security, now, verdict: ProjectScanVerdict.Poor, score: 20, grade: "F", findings: [hangfire, csp], trigger: ProjectScanTrigger.Deployment, version: "1.0.2+cccc333aa"),
                Scan(projectId, ProjectScanKind.Seo, now, verdict: ProjectScanVerdict.Good, score: 82, findings: [images]),
                Scan(projectId, ProjectScanKind.DomainAndCertificate, now, verdict: ProjectScanVerdict.Good, expiresAt: now.AddDays(61.5))
            ],
            Settings = new() { ProjectId = projectId, WeeklyScans = true }
        };

        var controller = A.Fake<IProjectScansController>();
        A.CallTo(() => controller.GetScans(projectId, A<CancellationToken>._)).Returns(scans);
        A.CallTo(() => controller.GetHistory(A<ProjectScanHistoryRequestDto>._, A<CancellationToken>._))
         .Returns([Scan(projectId, ProjectScanKind.Security, now.AddDays(-7), verdict: ProjectScanVerdict.NeedsWork, score: 70, grade: "B", trigger: ProjectScanTrigger.Weekly)]);

        // In place of the app's client of the api, before the context's services are first read.
        await using var server = new AppTestServer();
        await server.Build(configureTestWebAppServices: services => services.Replace(ServiceDescriptor.Singleton(controller))).Start(TestContext.CancellationToken);
        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<ProjectScans>(parameters => parameters.Add(p => p.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Domain and certificate", cut.Markup));

        var markup = cut.Markup;
        Assert.Contains(">F<", markup, "Security's grade.");
        Assert.Contains(">82<", markup, "SEO's score.");
        Assert.Contains("61 d", markup, "The days until the first certificate or registration ends.");
        Assert.Contains("after the deployment of cccc333", markup, "Why it ran, with the build as people say it.");

        // Ranked urgent first, across the kinds, each in plain words from its key.
        var urgent = markup.IndexOf("The background jobs dashboard (/hangfire) is open to everyone", StringComparison.Ordinal);
        var important = markup.IndexOf("No Content-Security-Policy is set", StringComparison.Ordinal);
        var improvement = markup.IndexOf("3 images have no text alternative", StringComparison.Ordinal);
        Assert.IsGreaterThan(-1, urgent);
        Assert.IsGreaterThan(urgent, important);
        Assert.IsGreaterThan(important, improvement);

        Assert.Contains("Keep HangfireDashboardAuthorizationFilter", markup, "The fix.");
        Assert.Contains("https://api.bluebird.app/hangfire", markup, "Where it was seen.");
        Assert.Contains("Fix everything", markup);
        Assert.HasCount(4, cut.FindAll("button").Where(button => button.TextContent.Contains("Copy the prompt for your AI", StringComparison.Ordinal)), "One per finding, and one for them all.");

        Assert.Contains("weekly scan", markup, "A past run, and why it ran.");
        Assert.Contains("Scan after every deployment", markup);
        Assert.Contains("bit Fabric sees every new deployment of yours in the health check it reads all the time.", markup, "The deployment setting in the home page's words.");
        Assert.Contains("They never sign in or try a password.", markup);

        cut.FindAll("button").First(button => button.TextContent.Contains("This is wrong", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => A.CallTo(() => controller.ReportWrong(A<ReportScanFindingRequestDto>.That.Matches(request => request.FindingId == hangfire.Id), A<CancellationToken>._))
                                    .MustHaveHappenedOnceExactly());
        cut.WaitForAssertion(() => Assert.Contains("Reported as wrong.", cut.Markup));
    }

    /// <summary>The performance card holds the Core Web Vitals of each page and device, each told good, needing improvement or poor.</summary>
    [TestMethod]
    public async Task ThePerformanceCard_Should_ShowTheCoreWebVitals_OfEachPageAndDevice()
    {
        var projectId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        ProjectScanFindingDto lcp = new() { Id = Guid.NewGuid(), Key = ScanFindings.LcpSlow, Severity = ScanFindingSeverity.Important, Parameters = ["4.5"], Where = ["/en-US/"] };

        var performance = Scan(projectId, ProjectScanKind.Performance, now, verdict: ProjectScanVerdict.NeedsWork, score: 54, findings: [lcp]);
        performance.Pages =
        [
            new() { Path = "/en-US/", Device = ProjectScanDevice.Phone, Score = 54, LargestContentfulPaintMs = 4475, CumulativeLayoutShift = 0.001, TotalBlockingTimeMs = 450, FirstContentfulPaintMs = 1409, TimeToFirstByteMs = 659 },
            new() { Path = "/en-US/", Device = ProjectScanDevice.Computer, Score = 92, LargestContentfulPaintMs = 596, CumulativeLayoutShift = 0, TotalBlockingTimeMs = 0, FirstContentfulPaintMs = 376 },
            new() { Path = "/en-US/products", Device = ProjectScanDevice.Phone, Score = 61, LargestContentfulPaintMs = 3100 }
        ];

        var controller = A.Fake<IProjectScansController>();
        A.CallTo(() => controller.GetScans(projectId, A<CancellationToken>._)).Returns(new ProjectScansDto
        {
            ProjectId = projectId,
            Address = "https://bluebird.app",
            Latest = [performance, Scan(projectId, ProjectScanKind.Accessibility, now, verdict: ProjectScanVerdict.Good, score: 96)],
            Settings = new() { ProjectId = projectId, WeeklyScans = true }
        });
        A.CallTo(() => controller.GetHistory(A<ProjectScanHistoryRequestDto>._, A<CancellationToken>._)).Returns([]);

        await using var server = new AppTestServer();
        await server.Build(configureTestWebAppServices: services => services.Replace(ServiceDescriptor.Singleton(controller))).Start(TestContext.CancellationToken);
        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<ProjectScans>(parameters => parameters.Add(p => p.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Core Web Vitals", cut.Markup));

        var markup = cut.Markup;
        Assert.Contains("Out of 100: the lowest of the pages and devices measured.", markup);
        Assert.HasCount(3, cut.FindAll(".vitals-row:not(.head)"), "A row a page and device.");
        Assert.Contains("4.5 s", markup, "The home page's LCP on a phone.");
        Assert.Contains("450 ms", markup, "Its TBT.");
        Assert.Contains("/en-US/products", markup);
        Assert.Contains("Computer", markup);

        var homeOnAPhone = cut.FindAll(".vitals-row:not(.head)")[0];
        Assert.Contains("Poor", homeOnAPhone.TextContent, "Over 4 s: poor, in words as well as in its tone.");
        Assert.Contains("tone-down", homeOnAPhone.InnerHtml);
        Assert.Contains("Needs improvement", homeOnAPhone.TextContent, "TBT between 200 and 600 ms.");
        Assert.Contains("Good", cut.FindAll(".vitals-row:not(.head)")[1].TextContent);

        Assert.Contains("The main content takes 4.5 s to show on a phone", markup, "The finding, from its key.");
        Assert.Contains("The same pages, checked with axe-core", markup, "Accessibility's card says what it checks.");
    }

    private static ProjectScanDto Scan(Guid projectId, ProjectScanKind kind, DateTimeOffset finishedAt, ProjectScanVerdict verdict, int? score = null, string? grade = null,
                                       DateTimeOffset? expiresAt = null, ProjectScanFindingDto[]? findings = null, ProjectScanTrigger trigger = ProjectScanTrigger.Weekly, string? version = null)
    {
        findings ??= [];

        return new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Kind = kind,
            Trigger = trigger,
            ApplicationVersion = version,
            RequestedAt = finishedAt.AddMinutes(-1),
            StartedAt = finishedAt.AddMinutes(-1),
            FinishedAt = finishedAt,
            Outcome = ProjectScanOutcome.Completed,
            Verdict = verdict,
            Score = score,
            Grade = grade,
            ExpiresAt = expiresAt,
            Findings = findings,
            UrgentCount = findings.Count(finding => finding.Severity is ScanFindingSeverity.Urgent),
            ImportantCount = findings.Count(finding => finding.Severity is ScanFindingSeverity.Important),
            ImprovementCount = findings.Count(finding => finding.Severity is ScanFindingSeverity.Improvement)
        };
    }

    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;
}
