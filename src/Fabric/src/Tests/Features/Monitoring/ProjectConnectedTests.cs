using Bit.Fabric.Client.Core.Components.Pages.Monitoring;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bunit;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// What the approved designs showed and the pages lacked, rendered in memory: the last step of adding a
/// project, with each location's first check as it arrives and the next steps, and the project card's 30 days. What the
/// api returns is ProjectMonitoringTests.Leftovers's to check; this checks what the pages make of it.
/// </summary>
[TestClass, TestCategory("UITest")]
public class ProjectConnectedTests
{
    [TestMethod]
    public async Task TheLastStep_Should_ShowEachLocationsFirstCheck_AsItArrives_AndLeadToTheNextSteps()
    {
        var projectId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        ProjectNetworkReadingDto eastern = new() { Location = "enam", Colo = "IAD", At = now, ResponseMs = 96 };
        ProjectNetworkReadingDto oceania = new() { Location = "oc", Colo = "SYD", At = now, ResponseMs = 131 };

        ProjectDetailsDto Details(params ProjectNetworkReadingDto[] readings) => new()
        {
            Project = new() { Id = projectId, Name = "Customer app", Address = "https://customer.example", Locations = ["enam", "oc"], ConnectionStatus = ProjectConnectionStatus.Connected, Health = ProjectHealth.Operational },
            Checks =
            [
                new() { Key = "AppDbContext", Title = "Database", Category = "Platform", IconName = "Database", Status = HealthCheckStatus.Healthy },
                new() { Key = "googleSignIn", Title = "Sign in with Google", Category = "Identity", Status = HealthCheckStatus.Degraded, IsNotConfigured = true }
            ],
            CheckedAt = now,
            HomePageMs = 212,
            NetworkReadings = readings
        };

        // Germany read the report the moment the approval came back; Eastern North America has answered, Oceania not yet.
        var current = Details(eastern);
        var controller = A.Fake<IProjectsController>();
        A.CallTo(() => controller.GetProject(projectId, A<CancellationToken>._)).ReturnsLazily(() => current);

        await using var server = new AppTestServer();
        await server.Build(configureTestWebAppServices: services => services.Replace(ServiceDescriptor.Singleton(controller))).Start(TestContext.CancellationToken);
        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<ProjectConnected>(parameters => parameters.Add(p => p.ProjectId, projectId));

        cut.WaitForAssertion(() => Assert.Contains("Customer app is connected", cut.Markup));

        var markup = cut.Markup;
        Assert.Contains("The first checks are running from every location", markup);
        Assert.Contains("Germany", markup);
        Assert.Contains("212 ms", markup, "Germany's home page, timed in the round that read the report.");
        Assert.Contains("Eastern North America", markup);
        Assert.Contains("96 ms, data center IAD", markup, "Every reading names the data center it ran in.");
        Assert.Contains("Oceania", markup);
        Assert.Contains("checking…", markup, "Oceania hasn't answered yet.");
        Assert.Contains("2 parts of your app found", markup);
        Assert.Contains("Not configured", markup);

        // The three next steps, each leading to where it is done.
        Assert.Contains("Alerts on this device", markup);
        Assert.Contains("Turn on", markup);
        Assert.Contains($"href=\"/projects/{projectId}?tab=alerts\"", markup);
        Assert.Contains("Your status page", markup);
        Assert.Contains($"href=\"/projects/{projectId}?tab=status\"", markup);
        Assert.Contains("Your team", markup);
        Assert.Contains($"href=\"{PageUrls.ManageMyTenants}\"", markup);
        Assert.Contains($"href=\"/projects/{projectId}\"", markup, "Open the project.");

        // Oceania's first reading comes in a minute later: the page looks again on its own.
        current = Details(eastern, oceania);

        cut.WaitForAssertion(() => Assert.Contains("Every location has answered.", cut.Markup), TimeSpan.FromSeconds(20));
        Assert.Contains("131 ms, data center SYD", cut.Markup);
        Assert.DoesNotContain("checking…", cut.Markup);
    }

    [TestMethod]
    public async Task TheProjectCard_Should_DrawItsLast30Days_ABarADay_ThatOpenTheHistory()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var projectId = Guid.NewGuid();

        ProjectDto project = new()
        {
            Id = projectId,
            Name = "Customer app",
            Address = "https://customer.example",
            ConnectionStatus = ProjectConnectionStatus.Connected,
            Health = ProjectHealth.Operational,
            Uptime30Days = 99.5,
            Last30Days = [.. Enumerable.Range(0, 30).Select(index => new ProjectDayDto
            {
                Date = today.AddDays(index - 29),
                Tone = index switch { < 10 => ProjectDayTone.NoData, 20 => ProjectDayTone.Down, 25 => ProjectDayTone.Degraded, _ => ProjectDayTone.Ok },
                DownMinutes = index is 20 ? 12 : 0,
                DegradedMinutes = index is 25 ? 5 : 0
            })]
        };

        await using var server = new AppTestServer();
        await server.Build().Start(TestContext.CancellationToken);
        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<ProjectCard>(parameters => parameters.Add(p => p.Project, project));

        var bars = cut.FindAll(".day-bars .bar");
        Assert.HasCount(30, bars, "A bar a day.");
        Assert.HasCount(10, bars.Where(bar => bar.ClassList.Contains("tone-nodata")), "The days before it was watched.");
        Assert.Contains("tone-down", bars[20].ClassName!);
        Assert.Contains("tone-degraded", bars[25].ClassName!);
        Assert.Contains("12 min down", bars[20].GetAttribute("title")!, "Each bar's tooltip says how its day went.");

        Assert.AreEqual($"/projects/{projectId}?tab=history", cut.Find(".day-bars").GetAttribute("href"));
        Assert.Contains("30 days", cut.Markup);
        Assert.Contains("Uptime 99.50%", cut.Markup);
    }

    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;
}
