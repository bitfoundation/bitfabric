namespace Bit.Fabric.Workbench.Infrastructure.Services;

/// <summary>
/// The flows every Workbench starts with, so the Flows page shows what a flow is before anyone draws one. They live here, not in
/// the review document, so they reach every copy and can't be changed: a change made on one's board goes into the review's own
/// copy of it. One whose page was removed from the project is left out.
/// </summary>
public static class WorkbenchSampleFlows
{
    public const string IdPrefix = "sample";
    public const string GetStartedId = $"{IdPrefix}getstarted";

    /// <summary>The get started sample's Get started arrow, named as <see cref="Build"/> names an arrow: by its two screens.</summary>
    public const string GetStartedArrowId = "homewizard";

    // The room a phone takes on the board, with space for the label of the arrow between screens.
    private const double ColumnStep = 850;

    // The page state of the screens that need one, by the id the Flows page reads it with: the flow's id, then the screen's.
    private static readonly Dictionary<string, string> states = [];

    public static string? StateOf(string id) => states.GetValueOrDefault(id);

    public static IReadOnlyCollection<string> StateIds => states.Keys;

    public static WorkbenchFlow[] Create(IStringLocalizer<WorkbenchStrings> localizer)
    {
        return Create(localizer, pageKey => WorkbenchPageCatalog.Find(pageKey) is not null);
    }

    public static WorkbenchFlow[] Create(IStringLocalizer<WorkbenchStrings> localizer, Func<string, bool> hasPage)
    {
        string Text(string key) => localizer[key];

        WorkbenchFlow[] flows =
        [
            Build(GetStartedId, Text("From the home page to a new project"),
                  [
                      ("home", "app-home", new(), Text("bitfabric.co's home page"), 0, 0),
                      ("wizard", "new-project", new(), Text("The Create new project wizard"), ColumnStep, 0)
                  ],
                  [
                      ("home", "wizard", Text("Get started"))
                  ])
        ];

        return [.. flows.Where(flow => flow.Screens.All(screen => hasPage(screen.PageKey)))];
    }

    private static WorkbenchFlow Build(string id, string name, (string Id, string PageKey, WorkbenchScenario Scenario, string Note, double X, double Y)[] screens, (string From, string To, string Label)[] links)
    {
        return new(id,
                   name,
                   [.. screens.Select(screen => new WorkbenchFlowScreen(screen.Id, screen.PageKey, screen.Scenario.ToQueryString(), screen.Note, states.ContainsKey($"{id}{screen.Id}"), screen.X, screen.Y))],
                   [.. links.Select(link => new WorkbenchFlowLink($"{link.From}{link.To}", link.From, link.To, link.Label, null))],
                   default);
    }
}
