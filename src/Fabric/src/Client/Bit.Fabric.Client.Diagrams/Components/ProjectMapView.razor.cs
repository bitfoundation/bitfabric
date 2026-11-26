namespace Bit.Fabric.Client.Diagrams.Components;

public partial class ProjectMapView
{
    // Sizes in canvas pixels; the canvas fits them to the pane, never larger than they are.
    private const double AppWidth = 210;
    private const double AppHeight = 150;
    private const double GroupWidth = 236;
    private const double GroupHead = 46;
    private const double PartHeight = 40;
    /// <summary>A part with what lies behind it under its name.</summary>
    private const double TwoLinePartHeight = 52;
    private const double GroupFoot = 10;
    private const double ColumnGap = 76;
    private const double RowGap = 20;
    private const double NarrowWidth = 320;
    private const double NarrowIndent = 28;
    private const double NarrowAppHeight = 150;

    /// <summary>The canvas on the page's own background, laid out left to right in either direction, as the boxes are placed from the left.</summary>
    private const string CanvasStyle = "--bit-blow-bg:transparent;direction:ltr";

    private static readonly (string Id, BlowPosition Position)[] Sides =
    [
        ("l", BlowPosition.Left),
        ("r", BlowPosition.Right),
        ("t", BlowPosition.Top),
        ("b", BlowPosition.Bottom)
    ];

    private readonly BlowFitViewOptions fitOptions = new() { Padding = 0.03 };

    private List<BlowNode> nodes = [];
    private List<BlowEdge> edges = [];
    private bool isAnswered;
    private bool isNarrow;
    private string? layoutKey;
    private double height = 480;
    private BlowCanvas? canvas;

    [Parameter, EditorRequired] public ProjectMapPicture Picture { get; set; } = default!;

    /// <summary>The part whose details the page shows, drawn as picked.</summary>
    [Parameter] public string? SelectedKey { get; set; }

    [Parameter] public EventCallback<string> OnSelect { get; set; }

    [Parameter] public BitDir? Dir { get; set; }

    private bool IsRtl => Dir is BitDir.Rtl;

    private string TextDir => IsRtl ? "rtl" : "ltr";

    protected override void OnParametersSet() => Build();

    /// <summary>Blow fits the map once, so a window that gets wider or narrower would leave it cut off or small.</summary>
    private void FitToPane() => canvas?.FitView(fitOptions);

    private void ScreenChanged(bool narrow)
    {
        isAnswered = true;
        isNarrow = narrow;
        Build();
    }

    private static double GroupHeight(ProjectMapGroup group) => GroupHead + group.Parts.Sum(PartHeightOf) + GroupFoot;

    private static double PartHeightOf(ProjectMapPart part) => part.Behind is null ? PartHeight : TwoLinePartHeight;

    private void Build()
    {
        if (isAnswered is false)
            return;

        // The picture's own words and tones are part of the key, so a newer reading redraws the map.
        var key = $"{isNarrow}-{IsRtl}-{string.Join('|', Picture.Groups.Select(g => $"{g.Name}:{g.Tone}:{string.Join(',', g.Parts.Select(p => $"{p.Key}{p.Tone}{p.Measure}{p.Flag}{p.Behind}"))}"))}-{Picture.AppTone}{Picture.AppMeasure}";
        if (key == layoutKey)
            return;

        layoutKey = key;

        if (isNarrow)
        {
            BuildNarrow();
        }
        else
        {
            BuildWide();
        }
    }

    /// <summary>The app in the middle, the categories in two columns beside it, each as tall as the other as far as they allow.</summary>
    private void BuildWide()
    {
        var left = new List<ProjectMapGroup>();
        var right = new List<ProjectMapGroup>();
        double leftHeight = 0, rightHeight = 0;

        foreach (var group in Picture.Groups)
        {
            if (leftHeight <= rightHeight)
            {
                left.Add(group);
                leftHeight += GroupHeight(group) + RowGap;
            }
            else
            {
                right.Add(group);
                rightHeight += GroupHeight(group) + RowGap;
            }
        }

        var tallest = Math.Max(Math.Max(leftHeight, rightHeight) - RowGap, AppHeight);
        var width = GroupWidth * 2 + AppWidth + ColumnGap * 2;
        var appX = GroupWidth + ColumnGap;

        double X(double x, double w) => IsRtl ? width - x - w : x;

        nodes = [new BlowNode
        {
            Id = "app",
            Type = "map-app",
            Data = Picture,
            Position = new(X(appX, AppWidth), (tallest - AppHeight) / 2),
            Width = AppWidth,
            Height = AppHeight,
            Label = Picture.AppTitle,
            Style = NodeStyle
        }];
        edges = [];

        void Column(List<ProjectMapGroup> column, double x, double columnHeight, bool isLeft)
        {
            var y = (tallest - (columnHeight - RowGap)) / 2;

            foreach (var group in column)
            {
                nodes.Add(GroupNode(group, X(x, GroupWidth), y));

                // Visually on the left of the app, whatever the direction, so the line leaves from the app's near side.
                var onTheLeft = isLeft != IsRtl;
                edges.Add(EdgeTo(group, onTheLeft ? "s-l" : "s-r", onTheLeft ? "t-r" : "t-l"));
                y += GroupHeight(group) + RowGap;
            }
        }

        Column(left, 0, leftHeight, isLeft: true);
        Column(right, appX + AppWidth + ColumnGap, rightHeight, isLeft: false);

        height = Math.Clamp(tallest + 32, 320, 1400);
    }

    /// <summary>On a phone: the app on top, the categories below it, each hanging from one line down the side.</summary>
    private void BuildNarrow()
    {
        var width = NarrowWidth + NarrowIndent;
        double X(double x, double w) => IsRtl ? width - x - w : x;

        nodes = [new BlowNode
        {
            Id = "app",
            Type = "map-app",
            Data = Picture,
            Position = new(X(NarrowIndent / 2, NarrowWidth), 0),
            Width = NarrowWidth,
            Height = NarrowAppHeight,
            Label = Picture.AppTitle,
            Style = NodeStyle
        }];
        edges = [];

        var y = NarrowAppHeight + 28;
        foreach (var group in Picture.Groups)
        {
            nodes.Add(GroupNode(group, X(NarrowIndent, NarrowWidth - NarrowIndent / 2), y, NarrowWidth - NarrowIndent / 2));
            edges.Add(EdgeTo(group, IsRtl ? "s-r" : "s-l", IsRtl ? "t-r" : "t-l", offset: 10));
            y += GroupHeight(group) + RowGap;
        }

        height = y - RowGap + 16;
    }

    private const string NodeStyle = "padding:0;border:0;background:transparent;box-shadow:none;border-radius:0";

    private static BlowNode GroupNode(ProjectMapGroup group, double x, double y, double width = GroupWidth) => new()
    {
        Id = $"g-{group.Name}",
        Type = "map-group",
        Data = group,
        Position = new(x, y),
        Width = width,
        Height = GroupHeight(group),
        Label = group.Title,
        Style = NodeStyle
    };

    /// <summary>Its colour is the category's worst part's; a working line carries a flow, a failing one stops.</summary>
    private static BlowEdge EdgeTo(ProjectMapGroup group, string sourceHandle, string targetHandle, double offset = 20) => new()
    {
        Id = $"app-{group.Name}",
        Source = "app",
        Target = $"g-{group.Name}",
        SourceHandle = sourceHandle,
        TargetHandle = targetHandle,
        Type = BlowEdgeType.SmoothStep,
        PathOptions = new() { Offset = offset, BorderRadius = 12 },
        Animated = group.Tone is ProjectMapTone.Healthy or ProjectMapTone.Degraded,
        Stroke = $"var({ToneCssVar(group.Tone)})",
        StrokeWidth = group.Tone is ProjectMapTone.Failing ? 2.5 : 1.75,
        Selectable = false,
        Focusable = false,
        // A working line stays quiet beside the boxes; one with trouble takes the eye.
        Style = group.Tone switch
        {
            ProjectMapTone.Failing or ProjectMapTone.NotConfigured => "stroke-dasharray:6 5",
            ProjectMapTone.Healthy => "opacity:.55",
            _ => null
        }
    };

    private static string ToneClass(ProjectMapTone tone) => $"pmv-{tone.ToString().ToLowerInvariant()}";

    private static string ToneIcon(ProjectMapTone tone) => tone switch
    {
        ProjectMapTone.Healthy => BitIconName.CompletedSolid,
        ProjectMapTone.Degraded => BitIconName.WarningSolid,
        ProjectMapTone.Failing => BitIconName.StatusErrorFull,
        ProjectMapTone.NotConfigured => BitIconName.Settings,
        _ => BitIconName.Info
    };

    private static BitColor ToneColor(ProjectMapTone tone) => tone switch
    {
        ProjectMapTone.Healthy => BitColor.Success,
        ProjectMapTone.Degraded => BitColor.Warning,
        ProjectMapTone.Failing => BitColor.Error,
        ProjectMapTone.NotConfigured => BitColor.Info,
        _ => BitColor.SecondaryForeground
    };

    private static string ToneCssVar(ProjectMapTone tone) => tone switch
    {
        ProjectMapTone.Healthy => BitCss.Var.Color.Success.Main,
        ProjectMapTone.Degraded => BitCss.Var.Color.Warning.Main,
        ProjectMapTone.Failing => BitCss.Var.Color.Error.Main,
        _ => BitCss.Var.Color.Border.Secondary.Main
    };
}
