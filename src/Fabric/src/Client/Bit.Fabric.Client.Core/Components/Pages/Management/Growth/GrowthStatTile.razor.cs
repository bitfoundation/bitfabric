namespace Bit.Fabric.Client.Core.Components.Pages.Management.Growth;

/// <summary>
/// A stat tile: the figure's name, its value, what the period added and how that compares with the period before, and a
/// line of how it moved over the period, drawn in the figure's color.
/// </summary>
public partial class GrowthStatTile
{
    private BitChartData sparkData = new();

    private readonly BitChartOptions sparkOptions = new()
    {
        Sparkline = true,
        MaintainAspectRatio = false,
        Layout = new BitChartLayoutOptions { Padding = 2 },
        Plugins = new BitChartPluginOptions { Tooltip = new BitChartTooltipOptions { Enabled = false } }
    };

    [Parameter, EditorRequired] public string Label { get; set; } = default!;

    [Parameter] public string HeadingElement { get; set; } = "h3";

    [Parameter] public string? Value { get; set; }

    /// <summary>What the period added, like "+212".</summary>
    [Parameter] public string? Added { get; set; }

    [Parameter] public GrowthDelta? Delta { get; set; }

    /// <summary>One short line under the value.</summary>
    [Parameter] public string? Note { get; set; }

    [Parameter] public double?[]? Series { get; set; }

    /// <summary>The theme color the line is drawn in, as a CSS variable's name.</summary>
    [Parameter] public string Color { get; set; } = BitCss.Var.Color.Primary.Main;

    [Parameter] public bool IsLoading { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        var series = Series ?? [];

        sparkData = new()
        {
            Labels = [.. series.Select(_ => "")],
            Datasets =
            {
                new BitChartDataset
                {
                    Data = [.. series],
                    BorderColor = $"var({Color})",
                    BackgroundColor = $"color-mix(in srgb, var({Color}) 12%, transparent)",
                    Fill = BitChartFillMode.Origin,
                    BorderWidth = 2,
                    PointRadius = 0,
                    Tension = 0.35,
                    SpanGaps = true
                }
            }
        };
    }
}
