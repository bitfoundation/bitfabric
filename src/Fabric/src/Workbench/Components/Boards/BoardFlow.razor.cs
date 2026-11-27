using Bit.Blow;

namespace Bit.Fabric.Workbench.Components.Boards;

/// <summary>
/// A process told on the app's own pages: its screens, each a page of the catalog live in its device, joined by arrows labeled
/// with what the user does, laid out from the first screen on (BoardFlowLayout). It pans and zooms, and Present walks through
/// the screens one at a time, the shown one usable. A block of a board (BoardBlock), as the boards skill describes.
/// </summary>
public partial class BoardFlow
{
    private const double LabelFontSize = 30;

    private BlowCanvas? canvas;
    // The screens shown: each id once, the first screen with it kept.
    private BoardFlowScreen[] shown = [];
    // The screen being presented, or -1.
    private int presentIndex = -1;
    // A finger's drag scrolls the board's page on a phone, rather than panning the canvas under it.
    private bool isNarrow;
    private readonly List<BlowNode> nodes = [];
    private readonly List<BlowEdge> edges = [];
    // Each screen's place in the flow, from 1, shown on its card.
    private readonly Dictionary<string, int> numbers = [];
    private readonly BlowFitViewOptions fitOptions = new() { Padding = 0.12, MaxZoom = 1 };

    [Parameter, EditorRequired] public IReadOnlyList<BoardFlowScreen> Screens { get; set; } = [];

    [Parameter] public IReadOnlyList<BoardFlowLink> Links { get; set; } = [];

    /// <summary>What the flow shows, for screen readers, such as "From the list to the form".</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The canvas's height on a wide screen, a CSS length; a phone's is shorter.</summary>
    [Parameter] public string? Height { get; set; }

    private bool IsPresenting => presentIndex >= 0 && presentIndex < shown.Length;

    private string? CurrentScreenId => IsPresenting ? shown[presentIndex].Id : null;

    private bool IsRtl => CurrentDir is BitDir.Rtl;

    private string? HeightStyle => Height is null ? null : $"--board-flow-height:{Height}";

    /// <summary>The pages the flow goes through, in order, each once.</summary>
    private string PathText => string.Join(IsRtl ? " ← " : " → ", shown.Select(TitleOf).Distinct());

    protected override void OnParametersSet()
    {
        Build();

        if (presentIndex >= shown.Length)
        {
            presentIndex = -1;
        }

        base.OnParametersSet();
    }

    /// <summary>
    /// The canvas's nodes and edges from the screens and links, keeping the node objects Blow already knows, so their
    /// measured sizes survive a render.
    /// </summary>
    private void Build()
    {
        shown = [.. Screens.DistinctBy(screen => screen.Id)];
        numbers.Clear();

        for (var index = 0; index < shown.Length; index++)
        {
            numbers[shown[index].Id] = index + 1;
        }

        var links = Links.Where(link => numbers.ContainsKey(link.From) && numbers.ContainsKey(link.To)).DistinctBy(link => (link.From, link.To)).ToArray();
        var positions = BoardFlowLayout.Arrange([.. shown.Select(screen => (screen.Id, ScenarioOf(screen)))], [.. links.Select(link => (link.From, link.To))], IsRtl);
        var existing = nodes.ToDictionary(node => node.Id);

        nodes.Clear();

        foreach (var screen in shown)
        {
            var (x, y) = positions[screen.Id];
            var node = existing.GetValueOrDefault(screen.Id) ?? new() { Id = screen.Id, Type = "screen", Draggable = false, Connectable = false, Deletable = false };

            node.Data = screen;
            node.Position = new(x, y);
            node.AriaLabel = TitleOf(screen);
            nodes.Add(node);
        }

        edges.Clear();
        edges.AddRange(links.Select(link => new BlowEdge
        {
            Id = $"{link.From}-{link.To}",
            Source = link.From,
            Target = link.To,
            SourceHandle = "out",
            TargetHandle = "in",
            Label = link.Label,
            StrokeWidth = 4,
            MarkerWidth = 22,
            MarkerHeight = 22,
            LabelStyle = FormattableString.Invariant($"font-size:{LabelFontSize}px;font-weight:600;fill:var(--bit-clr-fg-pri)"),
            LabelBgStyle = "fill:var(--bit-clr-bg-pri);stroke:var(--bit-clr-brd-sec)",
            LabelBgPaddingX = LabelPlatePadding(link.Label),
            LabelBgPaddingY = 20,
            LabelBgBorderRadius = 14,
            Deletable = false,
            Reconnectable = false
        }));
    }

    /// <summary>
    /// Blow sizes a label's plate for its own 11px labels, whatever font size LabelStyle sets, so the padding makes up the
    /// difference and the line doesn't run through a larger label (specs/workarounds.md).
    /// </summary>
    private static double LabelPlatePadding(string? label) => string.IsNullOrEmpty(label)
        ? 16
        : (BlowTextMetrics.EstimateWidth(label, LabelFontSize) * 1.08 - BlowTextMetrics.EstimateWidth(label)) / 2 + 16;

    private static WorkbenchScenario ScenarioOf(BoardFlowScreen screen) => screen.Scenario ?? new();

    private string TitleOf(BoardFlowScreen screen) => WorkbenchPageCatalog.Find(screen.PageKey) is { } page ? Localizer[page.TitleKey].Value : screen.PageKey;

    /// <summary>The screen's page alone, in its scenario, with its state when it has one; never framed for a store.</summary>
    private static string SourceOf(BoardFlowScreen screen)
    {
        var snapshotId = string.IsNullOrWhiteSpace(screen.State) ? null : $"{Pages.PreviewPage.SavedStatePrefix}{BoardFlowStates.Add(screen.State)}";

        return WorkbenchUrls.Preview(screen.PageKey, ScenarioOf(screen) with { Backdrop = null, Caption = null }, snapshotId: snapshotId);
    }

    /// <summary>The screen's page in the Pages section, in its scenario.</summary>
    private static string OpenUrlOf(BoardFlowScreen screen) => $"{WorkbenchUrls.Page(screen.PageKey)}{ScenarioOf(screen).ToQueryString()}";

    /// <summary>Zooms to one screen and makes its page usable.</summary>
    private async Task Present(int index)
    {
        if (shown.Length == 0)
            return;

        presentIndex = Math.Clamp(index, 0, shown.Length - 1);

        if (canvas is not null && nodes.FirstOrDefault(node => node.Id == shown[presentIndex].Id) is { } node)
        {
            await canvas.FitViewAsync(new BlowFitViewOptions { Nodes = [node], Padding = 0.08, MaxZoom = 1, Duration = 450 });
        }
    }

    private Task PresentScreen(BlowNodeMouseEvent args)
    {
        var index = Array.FindIndex(shown, screen => screen.Id == args.Node.Id);

        return index < 0 ? Task.CompletedTask : Present(index);
    }

    private async Task StopPresenting()
    {
        presentIndex = -1;

        if (canvas is not null)
        {
            await canvas.FitViewAsync(new BlowFitViewOptions { Padding = fitOptions.Padding, MaxZoom = 1, Duration = 450 });
        }
    }

    private void OnNarrowChanged(bool narrow)
    {
        isNarrow = narrow;
        StateHasChanged();
    }
}
