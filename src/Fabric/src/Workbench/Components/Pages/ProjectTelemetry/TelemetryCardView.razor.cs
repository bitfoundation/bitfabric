using System.Globalization;
using Bit.Fabric.Workbench.Components.Pages.ProjectTelemetry.Data;

namespace Bit.Fabric.Workbench.Components.Pages.ProjectTelemetry;

/// <summary>
/// One card of a project's telemetry dashboard: its title, its number with how it moved since the period before, the
/// sentence that says what it means and, from wide up, its line over the period; or a short list. A card the customer's
/// AI added shows what they asked for and can be deleted; a ready one can only be hidden. While the layout is being
/// arranged it shows its drag handle, its three sizes and a button that hides it.
/// </summary>
public partial class TelemetryCardView
{
    private static readonly (int Size, string Label)[] Sizes =
    [
        (1, nameof(WorkbenchStrings.TelemetrySizeSmall)),
        (2, nameof(WorkbenchStrings.TelemetrySizeWide)),
        (4, nameof(WorkbenchStrings.TelemetrySizeFull))
    ];

    private List<BitMenuButtonItem> menuItems = [];

    [Parameter, EditorRequired] public DashboardCard Card { get; set; } = default!;

    /// <summary>1 small, 2 wide, 4 full width.</summary>
    [Parameter] public int Size { get; set; } = 1;

    [Parameter] public bool IsEditing { get; set; }

    [Parameter] public EventCallback<int> OnResize { get; set; }

    [Parameter] public EventCallback OnHide { get; set; }

    [Parameter] public EventCallback OnDelete { get; set; }

    /// <summary>Copies a question about the card for the customer's AI.</summary>
    [Parameter] public EventCallback OnAskWhy { get; set; }

    private string LevelText => Card.Level switch
    {
        DashboardLevel.Good => nameof(WorkbenchStrings.TelemetryGood),
        DashboardLevel.Watch => nameof(WorkbenchStrings.TelemetryWorthWatching),
        _ => nameof(WorkbenchStrings.TelemetryNeedsLook)
    };

    protected override void OnParametersSet()
    {
        menuItems =
        [
            new() { Key = "why", Text = Localizer[nameof(WorkbenchStrings.TelemetryAskWhy)], IconName = BitIconName.Robot, OnClick = item => _ = OnAskWhy.InvokeAsync() },
            .. Sizes.Where(s => s.Size != Size).Select(s => new BitMenuButtonItem { Key = $"size-{s.Size}", Text = Localizer[s.Label], IconName = SizeIcon(s.Size), OnClick = item => _ = OnResize.InvokeAsync(s.Size) }),
            new() { Key = "hide", Text = Localizer[nameof(WorkbenchStrings.TelemetryHide)], IconName = BitIconName.Hide3, OnClick = item => _ = OnHide.InvokeAsync() }
        ];
    }

    private bool IsBetter(double change) => Card.LowerIsBetter ? change <= 0 : change >= 0;

    private static string FormatChange(double change) => Math.Abs(change).ToString("P0", CultureInfo.CurrentCulture);

    private static string SizeIcon(int size) => size switch { 1 => BitIconName.BackToWindow, 2 => BitIconName.MiniExpand, _ => BitIconName.FullScreen };

    private static string SizeGlyph(int size) => size switch { 1 => "S", 2 => "M", _ => "L" };

    private static string LineOf(double[] series) => string.Join(' ', Points(series).Select((p, i) => $"{(i == 0 ? 'M' : 'L')}{p}"));

    private static string AreaOf(double[] series) => $"{LineOf(series)} L100,32 L0,32 Z";

    private static IEnumerable<string> Points(double[] series)
    {
        var max = Math.Max(series.Max() * 1.1, 0.0001);
        var step = 100.0 / (series.Length - 1);

        return series.Select((value, i) => string.Create(CultureInfo.InvariantCulture, $"{i * step:0.##},{30 - 28 * value / max:0.##}"));
    }
}
