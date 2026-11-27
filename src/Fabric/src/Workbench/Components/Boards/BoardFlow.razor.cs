using Bit.Blow;

namespace Bit.Fabric.Workbench.Components.Boards;

/// <summary>
/// A process told on the app's own pages: its screens, each a page of the catalog live in its device, joined by arrows labeled
/// with what the user does, laid out from the first screen on (BoardFlowLayout). It shows one screen at a time, usable, and
/// steps to the previous or next one, or to the one clicked; it zooms, pans and goes full screen. A block of a board
/// (BoardBlock), as the boards skill describes.
/// </summary>
public partial class BoardFlow
{
    private const double LabelFontSize = 30;

    [AutoInject] private Bit.Butil.Document document = default!;

    private BlowCanvas? canvas;
    private ElementReference flowElement;
    private ButilSubscription? fullScreenChange;
    // The screens shown: each id once, the first screen with it kept.
    private BoardFlowScreen[] shown = [];
    private int currentIndex;
    // Full screen is the browser's, or the window's where the browser refuses it.
    private bool isFullScreen;
    private bool isBrowserFullScreen;
    private bool fillsWindow;
    private bool isPopoverPending;
    // A finger's drag scrolls the board's page on a phone, rather than panning the canvas under it.
    private bool isNarrow;
    private readonly List<BlowNode> nodes = [];
    private readonly List<BlowEdge> edges = [];
    // Each screen's place in the flow, from 1, shown on its card.
    private readonly Dictionary<string, int> numbers = [];

    [Parameter, EditorRequired] public IReadOnlyList<BoardFlowScreen> Screens { get; set; } = [];

    [Parameter] public IReadOnlyList<BoardFlowLink> Links { get; set; } = [];

    /// <summary>What the flow shows, for screen readers, such as "From the list to the form".</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The canvas's height on a wide screen, a CSS length; a phone's is shorter.</summary>
    [Parameter] public string? Height { get; set; }

    private string? CurrentScreenId => shown.Length > 0 ? shown[currentIndex].Id : null;

    private bool IsRtl => CurrentDir is BitDir.Rtl;

    private string? HeightStyle => Height is null ? null : $"--board-flow-height:{Height}";

    private string FullScreenLabel => Localizer[isFullScreen ? nameof(WorkbenchStrings.ExitFullScreen) : nameof(WorkbenchStrings.FullScreen)];

    protected override void OnParametersSet()
    {
        Build();

        currentIndex = Math.Clamp(currentIndex, 0, Math.Max(shown.Length - 1, 0));

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

    /// <summary>Fits the canvas to the current screen, easing there over the duration in milliseconds.</summary>
    private BlowFitViewOptions FitCurrent(int duration) => new()
    {
        Nodes = [.. nodes.Where(node => node.Id == CurrentScreenId)],
        Padding = 0.08,
        MaxZoom = 1,
        Duration = duration
    };

    /// <summary>Zooms to a screen and makes its page usable.</summary>
    private async Task Show(int index)
    {
        if (shown.Length == 0)
            return;

        currentIndex = Math.Clamp(index, 0, shown.Length - 1);

        if (canvas is not null)
        {
            await canvas.FitViewAsync(FitCurrent(duration: 450));
        }
    }

    private Task ShowScreen(BlowNode node)
    {
        var index = Array.FindIndex(shown, screen => screen.Id == node.Id);

        return index < 0 ? Task.CompletedTask : Show(index);
    }

    /// <summary>Fits the current screen again once the canvas changes size, as on going full screen or back.</summary>
    private async Task KeepCurrentInView(BlowDimensions size)
    {
        if (canvas is not null)
        {
            await canvas.FitViewAsync(FitCurrent(duration: 0));
        }
    }

    /// <summary>
    /// Takes the flow full screen, or over the whole window where the browser refuses, as a phone's Safari does; Esc, or the
    /// browser's own control, ends the browser's full screen and the flow's with it.
    /// </summary>
    private async Task ToggleFullScreen()
    {
        if (isFullScreen)
        {
            // Without its popover attribute, the flow leaves the window's top layer.
            isFullScreen = false;
            fillsWindow = false;

            if (isBrowserFullScreen)
            {
                await document.ExitFullscreen();
            }

            return;
        }

        isFullScreen = true;

        try
        {
            fullScreenChange ??= await document.SubscribeFullscreenChange(fullScreen =>
            {
                if (fullScreen)
                {
                    isBrowserFullScreen = isFullScreen;
                    return;
                }

                if (isBrowserFullScreen)
                {
                    isBrowserFullScreen = false;
                    isFullScreen = false;
                    InvokeAsync(StateHasChanged);
                }
            });

            await flowElement.RequestFullScreen(new());
        }
        catch (JSException)
        {
            fillsWindow = true;
            isPopoverPending = true;
        }
    }

    /// <summary>Shows the flow over the page once it has rendered as a popover, in the top layer, which no block around it clips.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (isPopoverPending)
        {
            isPopoverPending = false;
            await flowElement.ShowPopover();
        }
    }

    private void OnNarrowChanged(bool narrow)
    {
        isNarrow = narrow;
        StateHasChanged();
    }

    public override async ValueTask DisposeAsync()
    {
        if (fullScreenChange is not null)
        {
            await fullScreenChange.DisposeAsync();
        }

        await base.DisposeAsync();
    }
}
