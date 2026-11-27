using Bit.Fabric.Workbench.Components.Pages.ProjectTelemetry.Data;

namespace Bit.Fabric.Workbench.Components.Pages.ProjectTelemetry;

/// <summary>
/// A project's telemetry dashboard in bit Fabric, designed here on sample data before the app builds it: what needs a
/// look first, the cards the customer's AI added, then the ready cards every project gets, and the alerts their AI set
/// up. A card or an alert is added by asking one's own AI agent, through bit Fabric's MCP, and deleted here. Each person
/// arranges the cards for themselves, moving them by dragging (bit Bortable), sizing and hiding them, and their layout is
/// kept. Two directions: by area, in groups that fold away, or one board filtered by area.
/// </summary>
public partial class ProjectTelemetryPage
{
    public const string Key = "project-telemetry";
    public const string ByAreaKey = "by-area";
    public const string OneBoardKey = "one-board";

    private static readonly string[] Environments = ["Production", "Test"];

    private static readonly (string Range, string Label)[] Ranges =
    [
        ("1h", nameof(WorkbenchStrings.TelemetryRange1Hour)),
        ("24h", nameof(WorkbenchStrings.TelemetryRange24Hours)),
        ("7d", nameof(WorkbenchStrings.TelemetryRange7Days)),
        ("30d", nameof(WorkbenchStrings.TelemetryRange30Days))
    ];

    private DashboardCard[] cards = [];
    private DashboardAlert[] alerts = [];
    private bool isLoading = true;
    private bool isFailed;

    [AutoInject] private ProjectTelemetryStore store = default!;
    [AutoInject] private Clipboard clipboard = default!;

    private string Direction => WorkbenchPageCatalog.Find(Key)?.ResolveVariant(Scenario)?.Key ?? ByAreaKey;

    protected override void OnInitialized()
    {
        Subscribe(store);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender is false)
            return;

        await WhenStateRestored();
        await Load();
        StateHasChanged();
    }

    private async Task Load()
    {
        isLoading = true;
        isFailed = false;

        try
        {
            (cards, alerts) = await ScenarioState.Load<(DashboardCard[], DashboardAlert[])>(Key, Sample, () => ([], []), CurrentCancellationToken);
        }
        catch (WorkbenchSimulatedException)
        {
            isFailed = true;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        isLoading = false;
    }

    /// <summary>
    /// The sample cards and alerts, by the page's switches: what the AI added, or nothing yet; a job alert firing, or all
    /// quiet. The Mixed state shows two cards not measured yet, as a fresh app would.
    /// </summary>
    private (DashboardCard[], DashboardAlert[]) Sample()
    {
        var withAi = Scenario.GetSwitch(TelemetryDashboardData.AiSwitch) is not TelemetryDashboardData.AiNone;
        var jobsFailing = Scenario.GetSwitch(TelemetryDashboardData.AlertSwitch) is not TelemetryDashboardData.AlertQuiet;
        var sample = TelemetryDashboardData.Cards(withAi, jobsFailing);

        if (Scenario.Data is WorkbenchDataState.Mixed)
        {
            sample = [.. sample.Select(card => card.Key is "vitals" or "resize" ? card with { Level = DashboardLevel.NotMeasured } : card)];
        }

        return (sample, withAi ? TelemetryDashboardData.Alerts(jobsFailing) : []);
    }

    private IEnumerable<DashboardCard> Present => cards.Where(card => store.Deleted.Contains(card.Key) is false);

    /// <summary>The cards in the user's order, then the ones the order doesn't name yet.</summary>
    private IEnumerable<DashboardCard> Ordered => Present.OrderBy(card => store.Order.IndexOf(card.Key) is var index and >= 0 ? index : int.MaxValue);

    private IEnumerable<DashboardCard> Shown => Ordered.Where(card => store.Hidden.Contains(card.Key) is false);

    private IList<DashboardCard> Visible(string groupKey) => [.. Shown.Where(card => card.GroupKey == groupKey)];

    /// <summary>One board's cards: every card shown, or one area's.</summary>
    private IList<DashboardCard> BoardCards => [.. Shown.Where(card => store.Area is "" || card.GroupKey == store.Area)];

    private IEnumerable<DashboardGroup> GroupsWithCards => TelemetryDashboardData.Groups.Where(group => Shown.Any(card => card.GroupKey == group.Key));

    private IReadOnlyList<DashboardCard> HiddenCards => [.. Present.Where(card => store.Hidden.Contains(card.Key))];

    private IReadOnlyList<DashboardAlert> VisibleAlerts => [.. alerts.Where(alert => store.Deleted.Contains(alert.Key) is false)
                                                                    .Select(alert => store.Paused.Contains(alert.Key) ? alert with { State = DashboardAlertState.Paused } : alert)];

    private IReadOnlyList<DashboardAlert> FiringAlerts => [.. VisibleAlerts.Where(alert => alert.State is DashboardAlertState.Firing)];

    private IReadOnlyList<DashboardCard> NeedsLook => [.. Shown.Where(card => card.Level is DashboardLevel.Bad or DashboardLevel.Watch).OrderByDescending(card => card.Level)];

    private int BadCount => FiringAlerts.Count + NeedsLook.Count(card => card.Level is DashboardLevel.Bad);

    private int WatchCount => NeedsLook.Count(card => card.Level is DashboardLevel.Watch);

    private string SummaryTone => BadCount > 0 ? "bad" : WatchCount > 0 ? "watch" : "good";

    /// <summary>What needs a look first, then what is worth watching, in one line.</summary>
    private string SummaryTitle => (BadCount, WatchCount) switch
    {
        (0, 0) => Localizer[nameof(WorkbenchStrings.TelemetryAllGood)],
        (0, var watch) => Localizer[nameof(WorkbenchStrings.TelemetrySummaryWatch), watch],
        (var bad, 0) => Localizer[nameof(WorkbenchStrings.TelemetrySummaryBad), bad],
        (var bad, var watch) => Localizer[nameof(WorkbenchStrings.TelemetrySummaryBoth), bad, watch]
    };

    private DashboardLevel Worst(string groupKey) => Visible(groupKey).Select(card => card.Level).Where(level => level is DashboardLevel.Bad or DashboardLevel.Watch)
                                                                      .DefaultIfEmpty(DashboardLevel.Good).Max();

    private int SizeOf(DashboardCard card) => store.Sizes.TryGetValue(card.Key, out var size) ? size : card.DefaultSize;

    /// <summary>Keeps the new order of the cards that were moved, with every other card after them in the order it had.</summary>
    private void Reorder(IList<DashboardCard> items)
    {
        var moved = items.Select(card => card.Key).ToHashSet();
        store.SetOrder([.. items.Select(card => card.Key), .. Ordered.Select(card => card.Key).Where(key => moved.Contains(key) is false)]);
    }

    /// <summary>A link to a card in a folded group, or out of the area shown, opens it, and the browser scrolls to the card.</summary>
    private void OpenGroupOf(DashboardCard card)
    {
        if (store.ClosedGroups.Contains(card.GroupKey))
        {
            store.ToggleGroup(card.GroupKey);
        }

        if (store.Area is not "" && store.Area != card.GroupKey)
        {
            store.SetArea("");
        }
    }

    /// <summary>Copies a question about the card, with the environment and the period, for the customer's own AI.</summary>
    private async Task AskWhy(DashboardCard card)
    {
        var period = Localizer[Ranges.First(r => r.Range == store.Range).Label];
        await clipboard.WriteText(Localizer[nameof(WorkbenchStrings.TelemetryWhyPrompt), Localizer[card.TitleKey], store.Environment, period]);

        SnackBarService.Show(Localizer[nameof(WorkbenchStrings.TelemetryWhyCopied)], Localizer[nameof(WorkbenchStrings.TelemetryWhyCopiedLine)]);
    }

    /// <summary>In the app this opens how to connect an AI agent to bit Fabric's MCP; here it says so.</summary>
    private void ShowConnect() => SnackBarService.Show(Localizer[nameof(WorkbenchStrings.TelemetryConnect)], Localizer[nameof(WorkbenchStrings.TelemetryConnectNote)]);
}
