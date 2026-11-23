using Bit.Fabric.Workbench.Components.Pages.Compare.Data;
using Bit.Fabric.Workbench.Components.Pages.HomeDirections;

namespace Bit.Fabric.Workbench.Components.Pages.Compare;

/// <summary>
/// bitfabric.co/compare in two formats, its design options (2026-11-19): the side-by-side table leads, or who should choose
/// which leads and the table follows. The subject is the page's switch, so the hub's cards and the way back change the view's
/// switch, and a link or a screenshot shows the page that was open. It inherits SitePageHost so Get started opens the
/// Workbench's own wizard, as on the home page directions.
/// </summary>
public partial class ComparePage
{
    public const string Key = "compare";

    public const string SubjectSwitch = "subject";

    public const string TableKey = "table";

    public const string FitKey = "fit";

    private ElementReference top;

    private bool isOpening;

    private string VariantKey => WorkbenchPageCatalog.Find(Key)?.ResolveVariant(Scenario)?.Key ?? TableKey;

    private bool IsFit => VariantKey == FitKey;

    /// <summary>Built on each render, so a corrected fact shows with the next build.</summary>
    private CompareSubject Subject => Scenario.GetSwitch(SubjectSwitch) switch
    {
        LovableComparison.Key => LovableComparison.Build(Localizer),
        BoltComparison.Key => BoltComparison.Build(Localizer),
        Base44Comparison.Key => Base44Comparison.Build(Localizer),
        ReplitComparison.Key => ReplitComparison.Build(Localizer),
        V0Comparison.Key => V0Comparison.Build(Localizer),
        EmptyProjectComparison.Key => EmptyProjectComparison.Build(Localizer),
        StarterKitsComparison.Key => StarterKitsComparison.Build(Localizer),
        _ => HubComparison.Build(Localizer)
    };

    private CompareSubject[] Subjects => HubComparison.Subjects(Localizer);

    /// <summary>The id of a source's entry in the page's list, which the table's numbers lead to.</summary>
    public static string AnchorOf(string sourceId) => $"src-{sourceId.ToLowerInvariant()}";

    /// <summary>Opens a comparison, or the hub: the view's switch follows, the hub being its default.</summary>
    private async Task Open(string key)
    {
        var scenario = Scenario.WithSwitch(SubjectSwitch, key == HubComparison.Key ? null : key);

        isOpening = true;

        // The router learns the new address too, as SitePageHost does on its way to the same page, so the links into the page,
        // such as a source's number, keep the subject when they're followed.
        ScenarioState.Set(scenario);
        NavigationManager.NavigateTo(WorkbenchUrls.Preview(Key, scenario, PaneId), replace: true);

        await ReportScenario(scenario);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (isOpening is false)
            return;

        isOpening = false;

        try
        {
            await top.FocusAsync();
        }
        catch (JSException)
        {
            // The view went away before the page could scroll; nothing is left to show
        }
    }
}
