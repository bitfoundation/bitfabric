using Bit.Fabric.Client.Core.Components.Pages.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bunit;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// A project's Locations tab (bitfabric-internal/specs/monitoring.md, Where it is checked from), rendered in memory:
/// Germany always on, a checkbox per location of the bitplatform Network with its newest reading and the data center it ran
/// in, and nothing to save until a pick changes. Saving goes through SetLocations, which ProjectMonitoringTests.Network
/// covers.
/// </summary>
[TestClass, TestCategory("UITest")]
public class ProjectNetworkTabTests
{
    [TestMethod]
    public async Task TheLocationsTab_Should_ShowEachLocationWithItsDataCenter_AndSaveOnlyAChange()
    {
        await using var server = new AppTestServer();
        await server.Build().Start(TestContext.CancellationToken);
        await using var ctx = server.CreateBunitContext();

        var cut = ctx.Render<ProjectNetwork>(parameters => parameters
            .Add(p => p.Project, new ProjectDto { Id = Guid.NewGuid(), Name = "Customer app", Locations = ["enam", "oc"] })
            .Add(p => p.Readings, [new ProjectNetworkReadingDto { Location = "enam", Colo = "IAD", At = DateTimeOffset.UtcNow.AddSeconds(-20), ResponseMs = 120 }]));

        var checkboxes = cut.FindAll("input[type=checkbox]");
        Assert.HasCount(9, checkboxes, "Germany and the Network's eight locations.");
        Assert.AreSequenceEqual(new[] { true, false, true, false, false, false, false, false, true }, checkboxes.Select(checkbox => checkbox.HasAttribute("checked")).ToArray(),
                                "Germany, then Eastern North America and Oceania, the picks of a new project.");

        Assert.Contains("On the bitplatform Network", cut.Markup);
        Assert.Contains("Eastern North America", cut.Markup);
        Assert.Contains("from data center IAD", cut.Markup, "Every reading names the data center it ran in.");
        Assert.Contains("No reading in the last hour yet.", cut.Markup, "Oceania is picked and hasn't reported.");
        Assert.DoesNotContain("Durable Object", cut.Markup);
        Assert.DoesNotContain("Worker", cut.Markup);

        Assert.IsTrue(IsDisabled(SaveButton(cut)), "Nothing changed yet.");

        // Picking Western Europe makes it a change to save.
        cut.FindAll("input[type=checkbox]")[3].Click();

        cut.WaitForAssertion(() => Assert.IsFalse(IsDisabled(SaveButton(cut))));
    }

    private static AngleSharp.Dom.IElement SaveButton(IRenderedComponent<ProjectNetwork> cut)
        => cut.FindAll("button").Single(button => button.TextContent.Contains("Save locations", StringComparison.Ordinal));

    private static bool IsDisabled(AngleSharp.Dom.IElement button) => button.HasAttribute("disabled") || button.GetAttribute("aria-disabled") is "true";

    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext { get; set; } = default!;
}
