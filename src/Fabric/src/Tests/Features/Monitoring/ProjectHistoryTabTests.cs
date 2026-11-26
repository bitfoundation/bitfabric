using Bit.Fabric.Client.Core.Components.Pages.Monitoring;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bunit;

namespace Bit.Fabric.Tests.Features.Monitoring;

[TestClass, TestCategory("UITest")]
public class ProjectHistoryTabTests
{
    /// <summary>
    /// The tab draws the parts the server lists, by the names it gives them, rather than the keys or the titles a period
    /// kept from the round it began in, which an app mid-deployment can leave empty, as Sales's did on Test (2026-11-13).
    /// A part the app added to its report yesterday has no data before then, said as such rather than "not watched yet",
    /// as the project was watched; the days before the project was say "not watched yet".
    /// </summary>
    [TestMethod]
    public async Task ThePartsTheServerLists_Should_BeDrawnByTheirNames_WithTheDaysBeforeOneWasAddedAsNotInTheReport()
    {
        var monitoringId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var controller = A.Fake<IProjectIncidentsController>();
        A.CallTo(() => controller.GetHistory(A<ProjectHistoryRequestDto>._, A<CancellationToken>._)).Returns(new ProjectHistoryDto
        {
            WatchedSince = now.AddDays(-3),
            Parts =
            [
                new() { Key = "apns", Title = "Apple push", ReportedFrom = now.AddDays(-3) },
                new() { Key = "search", Title = "Search", ReportedFrom = now.AddDays(-1) }
            ],
            Periods = [new() { CheckKey = "apns", Title = "Apple push", Status = HealthCheckStatus.Degraded, StartedAt = now.AddHours(-2), EndedAt = now.AddHours(-1.9) }]
        });

        await using var server = new AppTestServer();
        await server.Build(configureTestWebAppServices: services => services.Replace(ServiceDescriptor.Singleton(controller))).Start(TestContext.CancellationToken);
        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<ProjectHistory>(parameters => parameters.Add(p => p.Project, new MonitoringDto { Id = monitoringId, Name = "Bluebird" }));

        cut.WaitForAssertion(() => Assert.Contains("Apple push", cut.Markup));
        Assert.DoesNotContain(">apns<", cut.Markup);

        var search = cut.FindAll("div[role=group][aria-label='Search'] button").Select(bar => bar.GetAttribute("aria-label")!).ToArray();
        Assert.HasCount(90, search);
        Assert.Contains(label => label.Contains("not in the app's report that day", StringComparison.Ordinal), search, "The days between the project's first and the part's.");
        Assert.Contains(label => label.Contains("not watched yet", StringComparison.Ordinal), search, "The days before the project was watched.");
        Assert.Contains("worked all day", search[^1]);
    }

    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;
}
