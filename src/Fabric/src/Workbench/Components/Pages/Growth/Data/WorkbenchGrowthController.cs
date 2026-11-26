using Bit.Fabric.Shared.Features.Growth;
using Bit.Fabric.Shared.Features.Growth.Dtos;

namespace Bit.Fabric.Workbench.Components.Pages.Growth.Data;

/// <summary>
/// Stands in for bit Fabric's growth api: the counts of the sample world the History switch picks (months of growth, a
/// first day or a slow week), through the view's data state, so the app's own page shows every state it has.
/// </summary>
public sealed class WorkbenchGrowthController(WorkbenchScenarioState scenarioState) : WorkbenchStandIn(scenarioState, "growth"), IGrowthController
{
    public Task<GrowthDataDto> GetData(string? timeZone, CancellationToken cancellationToken)
        => Load("data",
                () => GrowthSampleData.Count(GrowthSamples.Build(ScenarioState.Scenario.GetSwitch(GrowthSamples.HistorySwitch))),
                () => new GrowthDataDto { Today = GrowthSamples.Today },
                cancellationToken);
}
