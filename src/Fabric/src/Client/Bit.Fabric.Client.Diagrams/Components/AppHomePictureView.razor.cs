namespace Bit.Fabric.Client.Diagrams.Components;

public partial class AppHomePictureView
{
    /// <summary>How long the light stays on each box as it walks through the picture, in seconds.</summary>
    private const double StepSeconds = 1.3;

    /// <summary>
    /// The canvas on the page's own background, laid out left to right in either direction, as the boxes are placed from
    /// the left.
    /// </summary>
    private const string CanvasStyle = "--bit-blow-bg:transparent;direction:ltr";

    private static readonly double[] Light = [0, 0, 1, 0, 0];

    private static readonly (string Id, BlowPosition Position)[] Sides =
    [
        ("l", BlowPosition.Left),
        ("r", BlowPosition.Right),
        ("t", BlowPosition.Top),
        ("b", BlowPosition.Bottom)
    ];

    // The padding leaves room for a line that runs around the boxes on a phone
    private readonly BlowFitViewOptions fitOptions = new() { Padding = 0.04 };

    private List<BlowNode> nodes = [];
    private List<BlowEdge> edges = [];
    private bool isAnswered;
    private bool isNarrow;
    private string? layoutKey;
    private BlowCanvas? canvas;

    [Inject] private IServiceProvider Services { get; set; } = default!;

    [Parameter, EditorRequired] public AppHomePicture Picture { get; set; } = default!;

    [Parameter] public BitDir? Dir { get; set; }

    private bool IsRtl => Dir is BitDir.Rtl;

    private string TextDir => IsRtl ? "rtl" : "ltr";

    private double height => isNarrow ? Picture.NarrowHeight : Picture.WideHeight;

    protected override void OnInitialized()
    {
        // In WebAssembly the app loads Bmotion lazily and registers nothing at startup; everywhere else it is registered
        if (OperatingSystem.IsBrowser())
        {
            Services.ConfigureBmotionServices(_ => { });
        }
    }

    protected override void OnParametersSet() => Build();

    /// <summary>Blow fits the picture once, so a window that gets wider or narrower would leave it cut off or small.</summary>
    private void FitToPane() => canvas?.FitView(fitOptions);

    private void ScreenChanged(bool narrow)
    {
        isAnswered = true;
        isNarrow = narrow;
        Build();
    }

    private void Build()
    {
        var key = $"{Picture.Id}-{isNarrow}-{IsRtl}";
        if (key == layoutKey)
            return;

        layoutKey = key;

        AppHomePictureArea AreaOf(AppHomePictureBox box) => isNarrow ? box.Narrow : box.Wide;

        var pictureWidth = Picture.Boxes.Where(b => b.ParentId is null).Max(b => AreaOf(b).X + AreaOf(b).Width);
        var boxes = Picture.Boxes.ToDictionary(b => b.Id);

        nodes = [.. Picture.Boxes.Select(box =>
        {
            var area = AreaOf(box);
            var outerWidth = box.ParentId is { } parent ? AreaOf(boxes[parent]).Width : pictureWidth;

            return new BlowNode
            {
                Id = box.Id,
                Type = "picture-box",
                Data = box,
                ParentId = box.ParentId,
                Position = new(IsRtl ? outerWidth - area.X - area.Width : area.X, area.Y),
                Width = area.Width,
                Height = area.Height,
                Label = box.Title,
                Style = "padding:0;border:0;background:transparent;box-shadow:none;border-radius:0"
            };
        })];

        edges = [.. Picture.Lines.Select(line => new BlowEdge
        {
            Id = $"{line.From}-{line.To}",
            Source = line.From,
            Target = line.To,
            SourceHandle = $"s-{Side(isNarrow ? line.NarrowFrom : line.WideFrom)}",
            TargetHandle = $"t-{Side(isNarrow ? line.NarrowTo : line.WideTo)}",
            Type = BlowEdgeType.SmoothStep,
            PathOptions = new() { Offset = 6 },
            Animated = line.Flows,
            Stroke = StrokeOf(line.Tone),
            StrokeWidth = 2,
            Selectable = false,
            Focusable = false,
            Style = line.Dashed ? "stroke-dasharray:6 6" : null,
            // On a phone the line runs along the edge of the screen, where its label would be cut in half
            Label = isNarrow ? null : line.Label,
            LabelStyle = $"fill:{StrokeOf(line.Tone)};font-weight:600;font-size:13px",
            LabelBgStyle = $"fill:var({BitCss.Var.Color.Background.Primary.Main})",
            LabelBgPaddingX = 6,
            LabelBgPaddingY = 3
        })];
    }

    private string Side(string side) => (IsRtl, side) switch
    {
        (true, "l") => "r",
        (true, "r") => "l",
        _ => side
    };

    private static string StrokeOf(AppHomePictureTone tone) => tone switch
    {
        AppHomePictureTone.Good => $"var({BitCss.Var.Color.Success.Main})",
        AppHomePictureTone.Bad => $"var({BitCss.Var.Color.Error.Main})",
        _ => $"var({BitCss.Var.Color.Foreground.Secondary.Main})"
    };

    private static BitColor? ColorOf(AppHomePictureTone tone) => tone switch
    {
        AppHomePictureTone.Good => BitColor.Success,
        AppHomePictureTone.Bad => BitColor.Error,
        _ => null
    };

    private static BmotionTransition EnterOf(AppHomePictureBox box) => Bm.Spring(stiffness: 120, damping: 20).WithDelay((box.Step ?? 0) * 0.12);

    /// <summary>The box lights up for its turn of the loop: the browser plays it, so a turn costs the page no render.</summary>
    private BmotionTransition LightOf(int step)
    {
        var count = Picture.Boxes.Max(b => b.Step ?? 0) + 1;

        return Bm.Tween(count * StepSeconds, BmotionEase.Linear, repeat: BmotionRepeat.Forever,
                        times: [0, Math.Max(step / (double)count, 0.001), (step + 0.5) / count, Math.Min((step + 1) / (double)count, 0.999), 1]);
    }
}
