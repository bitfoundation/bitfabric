using Bit.Fabric.Client.Core.Components.Pages.Monitoring;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bunit;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// The application map's way into a part (bitfabric-internal/specs/monitoring.md, A part's History and Performance open on
/// that part, The map shows what lies behind a part), rendered in memory: its buttons name the part in the link they
/// open, its details say what lies behind it, and History shows that part alone with a chip that shows every part again.
/// </summary>
[TestClass, TestCategory("UITest"), Retry(2)]
public class ProjectMapBunitTests
{
    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task APartsDetails_Should_SayWhatLiesBehindIt_AndOpenItsHistoryAndPerformanceOnIt()
    {
        await using var server = new AppTestServer();
        await server.Build().Start(TestContext.CancellationToken);
        await using var ctx = server.CreateBunitContext();

        ProjectMapPartDto sms = new() { Key = "sms", Title = "SMS", Category = "Messaging", Status = HealthCheckStatus.Healthy, Provider = "Twilio", Host = "api.twilio.com", Rounds = 1440 };
        List<ProjectTabLink> opened = [];

        var cut = ctx.Render<ProjectMapDetails>(parameters => parameters
            .Add(p => p.Part, sms)
            .Add(p => p.Parts, [sms])
            .Add(p => p.OnOpenTab, (ProjectTabLink link) => opened.Add(link)));

        var behind = cut.Find(".behind");
        Assert.Contains("Twilio", behind.TextContent);
        Assert.Contains("api.twilio.com", behind.TextContent);
        Assert.DoesNotContain(AppStrings.MonitoringAppMapModel, behind.TextContent, "Only an AI part has a model.");

        cut.FindAll("button").Single(button => button.TextContent.Contains(AppStrings.MonitoringHistory)).Click();
        cut.FindAll("button").Single(button => button.TextContent.Contains(AppStrings.AppHomePerformance)).Click();

        Assert.AreSequenceEqual(new ProjectTabLink[] { new("history", "sms"), new("performance", "sms") }, opened.ToArray(), "The link names the part, so the tab opens on it.");

        // A report that names nothing behind a part shows nothing in its place.
        cut.Render(parameters => parameters.Add(p => p.Part, new ProjectMapPartDto { Key = "AppDbContext", Title = "Database", Status = HealthCheckStatus.Healthy }));
        Assert.IsEmpty(cut.FindAll(".behind"));
    }

    [TestMethod]
    public async Task History_Should_ShowOnePart_WithAChipThatShowsEveryPartAgain()
    {
        // The history of a project with two parts, which the page reads through the api.
        var incidents = A.Fake<IProjectIncidentsController>();
        A.CallTo(() => incidents.GetHistory(A<ProjectHistoryRequestDto>._, A<CancellationToken>._)).Returns(new ProjectHistoryDto
        {
            WatchedSince = DateTimeOffset.UtcNow.AddDays(-10),
            Parts = [new() { Key = "smtp", Title = "Email" }, new() { Key = "sms", Title = "SMS" }]
        });

        await using var server = new AppTestServer();
        await server.Build(configureTestWebAppServices: services => services.AddScoped(_ => incidents)).Start(TestContext.CancellationToken);
        await using var ctx = server.CreateBunitContext();

        MonitoringDto project = new() { Id = Guid.NewGuid(), Name = "Customer app", CreatedOn = DateTimeOffset.UtcNow.AddDays(-10) };
        var everyPartShown = 0;

        var cut = ctx.Render<ProjectHistory>(parameters => parameters
            .Add(p => p.Project, project)
            .Add(p => p.Check, "smtp")
            .Add(p => p.OnShowEveryPart, () => everyPartShown++));

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll(".row .row-title").Select(title => title.TextContent.Trim()).ToArray();
            Assert.AreSequenceEqual(new[] { "Customer app", "Email" }, rows, "The app's own row, and the one part.");
        }, TimeSpan.FromSeconds(10));

        Assert.Contains("Email", cut.Find(".part-filter").TextContent, "A chip says which part the tab shows.");

        cut.Find(".part-filter button").Click();
        Assert.AreEqual(1, everyPartShown, "Its close shows every part again.");

        // On every part: all the rows, and no chip.
        var all = ctx.Render<ProjectHistory>(parameters => parameters.Add(p => p.Project, project));
        all.WaitForAssertion(() => Assert.HasCount(3, all.FindAll(".row")), TimeSpan.FromSeconds(10));
        Assert.IsEmpty(all.FindAll(".part-filter"));
    }
}
