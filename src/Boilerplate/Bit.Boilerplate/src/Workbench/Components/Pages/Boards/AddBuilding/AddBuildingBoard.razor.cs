namespace Boilerplate.Workbench.Components.Pages.Boards.AddBuilding;

/// <summary>
/// One of the example boards the project comes with: how someone adds a landmark building, told on the buildings page itself
/// (BoardFlow). The form and the map are the page's own states, given as its store's JSON.
/// </summary>
public partial class AddBuildingBoard
{
    private const string FormState = """{"buildings":{"Draft":{"id":"d98de887-43b0-49c3-8b78-fd6161c7337d","name":"","city":"","province":"Groningen","type":"Museum","year":2026,"height":null,"floors":null,"latitude":52.2,"longitude":5.3},"IsDraftNew":true}}""";

    private const string MapState = """{"buildings":{"IsMapShown":true}}""";

    private BoardFlowScreen[] Screens =>
    [
        new("list", "buildings", Localizer[nameof(WorkbenchStrings.SampleFlowBuildingsList)]),
        new("form", "buildings", Localizer[nameof(WorkbenchStrings.SampleFlowBuildingsForm)], State: FormState),
        new("map", "buildings", Localizer[nameof(WorkbenchStrings.SampleFlowBuildingsMap)], State: MapState)
    ];

    private BoardFlowLink[] Links =>
    [
        new("list", "form", Localizer[nameof(WorkbenchStrings.BuildingsAdd)]),
        new("list", "map", Localizer[nameof(WorkbenchStrings.BuildingsMapView)])
    ];
}
