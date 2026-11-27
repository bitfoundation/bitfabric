namespace Boilerplate.Workbench.Components.Boards;

/// <summary>
/// A screen of a board's flow (BoardFlow): a page of the catalog, live, in its scenario (device, theme, language, data state
/// and the rest of what a Workbench link carries), with a line under it on what it shows. <paramref name="State"/> is the
/// page's stores as JSON, keyed by the names of its Blex stores, for a screen that shows the page as a few taps leave it,
/// such as a form already open.
/// </summary>
public sealed record BoardFlowScreen(string Id, string PageKey, string? Note = null, WorkbenchScenario? Scenario = null, string? State = null);

/// <summary>An arrow of a board's flow from one screen to another, labeled with what the user does there.</summary>
public sealed record BoardFlowLink(string From, string To, string? Label = null);
