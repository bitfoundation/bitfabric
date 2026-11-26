using Bit.Fabric.Shared.Features.Growth;
using Bit.Fabric.Shared.Features.Growth.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Management.Growth;

/// <summary>
/// The growth page (bitfabric-internal/specs/growth.md): how many projects are made, how many of their Trials become a
/// License and how fast, licensed projects in production, and the teams behind them, every team's together, as totals
/// only, for global admins. It reads every day's counts once (<see cref="IGrowthController.GetData"/>) and builds the
/// figures of the period picked from them (<see cref="GrowthReport.Compute"/>), so another period needs no request.
/// </summary>
public partial class GrowthPage
{
    private static readonly string ProjectsColor = BitCss.Var.Color.Primary.Main;

    private static readonly string LicensedColor = BitCss.Var.Color.Success.Main;

    private static readonly string ProductionColor = BitCss.Var.Color.Info.Main;

    private static readonly (GrowthPeriod Period, string Key)[] Periods =
    [
        (GrowthPeriod.Week, nameof(AppStrings.GrowthWeek)),
        (GrowthPeriod.Month, nameof(AppStrings.GrowthMonth)),
        (GrowthPeriod.Quarter, nameof(AppStrings.GrowthQuarter)),
        (GrowthPeriod.Year, nameof(AppStrings.GrowthYear)),
        (GrowthPeriod.All, nameof(AppStrings.GrowthAll))
    ];

    [AutoInject] private IGrowthController growthController = default!;

    /// <summary>The period picked at the top; the last 30 days at first.</summary>
    private GrowthPeriod period = GrowthPeriod.Month;

    private GrowthDataDto? data;
    private GrowthReport? report;
    private bool loadFailed;

    private BitChartData overTimeData = new();
    private BitChartData eachData = new();
    private BitChartData daysData = new();
    private BitChartData teamsData = new();
    private BitChartOptions lineOptions = new();
    private BitChartOptions barOptions = new();
    private BitChartOptions daysOptions = new();
    private BitChartOptions teamsOptions = new();

    /// <summary>The page's sections sit under its h1, the app header's or its own, and a card or a tile under those.</summary>
    private const string SectionHeading = "h2";

    private const string CardHeading = "h3";

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        await Load();
    }

    private async Task Load()
    {
        loadFailed = false;
        report = null;

        try
        {
            data = await growthController.GetData(TimeZoneService.CurrentTimeZone.Id, CurrentCancellationToken);
        }
        catch (Exception exp) when (exp is not OperationCanceledException)
        {
            // The page says it in one line, with Try again, rather than leaving its placeholders up.
            loadFailed = true;
            return;
        }

        Compute();
    }

    private Task Retry() => Load();

    /// <summary>A period picked at the top: the figures are built again from the counts already read.</summary>
    private void PickPeriod(string? key)
    {
        if (Enum.TryParse<GrowthPeriod>(key, out var picked) is false || picked == period)
            return;

        period = picked;

        Compute();
    }

    private void Compute()
    {
        if (data is null)
            return;

        var calendar = CultureInfo.CurrentUICulture.Calendar;

        report = GrowthReport.Compute(data, period, calendar);

        // The big number's curve runs from the first project, whatever the period, so the whole climb shows, a point a week
        // that ends today, so no month cut short flattens its end.
        var sinceStart = GrowthReport.Compute(data, GrowthPeriod.All, calendar, GrowthBucket.Week);

        BuildCharts(report, sinceStart);
    }

    /// <summary>The line under the title: what the period is, and what its changes are compared with.</summary>
    private string WindowText => report is null ? "" : period switch
    {
        GrowthPeriod.Week => Localizer[nameof(AppStrings.GrowthWindowWeek)].Value,
        GrowthPeriod.Month => Localizer[nameof(AppStrings.GrowthWindowMonth)].Value,
        GrowthPeriod.Quarter => Localizer[nameof(AppStrings.GrowthWindowQuarter)].Value,
        GrowthPeriod.Year => Localizer[nameof(AppStrings.GrowthWindowYear)].Value,
        _ => Localizer[nameof(AppStrings.GrowthWindowAll), FirstDayText].Value
    };

    private string FirstDayText => report is null ? "" : report.FirstDay.ToDateTime(TimeOnly.MinValue).ToString("d MMMM yyyy", CultureInfo.CurrentUICulture);

    private string EachTitle => report?.Bucket switch
    {
        GrowthBucket.Week => Localizer[nameof(AppStrings.GrowthEachWeek)].Value,
        GrowthBucket.Month => Localizer[nameof(AppStrings.GrowthEachMonth)].Value,
        _ => Localizer[nameof(AppStrings.GrowthEachDay)].Value
    };

    private string? ConversionNote => report switch
    {
        null or { Conversion: null } => null,
        { ConversionIsSoFar: true } => Localizer[nameof(AppStrings.GrowthConversionSoFar), GrowthFormat.Count(report.ConversionOf)].Value,
        _ => Localizer[nameof(AppStrings.GrowthConversionOf), GrowthFormat.Count(report.ConversionOf)].Value
    };

    /// <summary>A tile's line over the period, from where the data starts.</summary>
    private static double?[]? Line(GrowthReport? r, int[]? values) => r is null || values is null ? null : [.. values.Skip(r.EmptyLead).Select(value => (double?)value)];

    private static double?[]? Line(GrowthReport? r, double?[]? values) => r is null || values is null ? null : [.. values.Skip(r.EmptyLead)];

    /// <summary>Half the Licenses come within this: the same day, a day, or a number of days.</summary>
    private string MedianText(double median) => median switch
    {
        0 => Localizer[nameof(AppStrings.GrowthSameDay)].Value,
        1 => Localizer[nameof(AppStrings.GrowthOneDay)].Value,
        _ => Localizer[nameof(AppStrings.GrowthDays), GrowthFormat.Days(median)].Value
    };

    /// <summary>The share of the licensed projects that are live, under the count of those in production.</summary>
    private string? InProductionNote => report is { LicensedProjects: > 0 }
        ? Localizer[nameof(AppStrings.GrowthInProductionNote), GrowthFormat.Share(report.InProduction / (double)report.LicensedProjects)].Value
        : null;

    private string? OverTimeDescription => report is null ? null
        : Localizer[nameof(AppStrings.GrowthOverTimeDescription), GrowthFormat.Count(report.Projects), GrowthFormat.Count(report.LicensedProjects)].Value;

    private string? EachDescription => report is null ? null
        : Localizer[nameof(AppStrings.GrowthEachDescription), GrowthFormat.Count(report.ProjectsMade.Now), GrowthFormat.Count(report.ProjectsLicensed.Now)].Value;

    private void BuildCharts(GrowthReport r, GrowthReport sinceStart)
    {
        var labels = Labels(r);

        var culture = CultureInfo.CurrentUICulture;

        var totalLabels = Labels(sinceStart, isTotal: true);

        overTimeData = new()
        {
            // Three labels: a month and its year, as Persian writes them, is long on a phone.
            Labels = Spaced(totalLabels, 3),
            Datasets =
            {
                Line(Localizer[nameof(AppStrings.GrowthSeriesProjects)], sinceStart.ProjectsTotal, ProjectsColor),
                Line(Localizer[nameof(AppStrings.GrowthSeriesLicensed)], sinceStart.LicensedTotal, LicensedColor)
            }
        };

        eachData = new()
        {
            Labels = Spaced(labels),
            Datasets =
            {
                Bars(Localizer[nameof(AppStrings.GrowthSeriesMade)], r.MadePerBucket, ProjectsColor, r.LastBucketIsPartial),
                Bars(Localizer[nameof(AppStrings.GrowthSeriesNewlyLicensed)], r.LicensedPerBucket, LicensedColor, r.LastBucketIsPartial)
            }
        };

        daysData = r.DaysToLicense.Sum() is 0 ? new() : new()
        {
            Labels =
            [
                Localizer[nameof(AppStrings.GrowthSameDay)],
                Localizer[nameof(AppStrings.GrowthDays1To7)],
                Localizer[nameof(AppStrings.GrowthDays8To14)],
                Localizer[nameof(AppStrings.GrowthDays15To30)],
                Localizer[nameof(AppStrings.GrowthAfterTrial)]
            ],
            Datasets = { Bars(Localizer[nameof(AppStrings.GrowthSeriesNewlyLicensed)], r.DaysToLicense, LicensedColor, false) }
        };

        teamsData = r.TeamsByProjects.Sum() is 0 ? new() : new()
        {
            Labels = ["1", "2", Localizer[nameof(AppStrings.GrowthProjects3To5)], Localizer[nameof(AppStrings.GrowthProjects6Plus)]],
            Datasets = { Bars(Localizer[nameof(AppStrings.GrowthSeriesTeams)], r.TeamsByProjects, ProjectsColor, false) }
        };

        lineOptions = new()
        {
            MaintainAspectRatio = false,
            Culture = culture,
            Interaction = new BitChartInteractionOptions { Mode = BitChartInteractionMode.Index, Intersect = false },
            Plugins = new BitChartPluginOptions { Legend = new BitChartLegendOptions { Placement = BitPlacement.Bottom }, Tooltip = TitledBy(totalLabels) },
            Scales =
            {
                ["x"] = new BitChartScaleOptions { Id = "x", Type = BitChartScaleType.Category, Grid = new BitChartGridOptions { Display = false }, Ticks = new BitChartTickOptions { AutoSkip = false, MaxRotation = 0 } },
                ["y"] = new BitChartScaleOptions { Id = "y", Type = BitChartScaleType.Linear, BeginAtZero = true, SuggestedMax = 4, Ticks = new BitChartTickOptions { MaxTicksLimit = 5, Precision = 0 } }
            }
        };

        barOptions = new()
        {
            MaintainAspectRatio = false,
            Culture = culture,
            Interaction = new BitChartInteractionOptions { Mode = BitChartInteractionMode.Index, Intersect = false },
            Plugins = new BitChartPluginOptions { Legend = new BitChartLegendOptions { Placement = BitPlacement.Bottom }, Tooltip = TitledBy(labels) },
            Scales =
            {
                ["x"] = new BitChartScaleOptions { Id = "x", Type = BitChartScaleType.Category, Grid = new BitChartGridOptions { Display = false }, Ticks = new BitChartTickOptions { AutoSkip = false, MaxRotation = 0 } },
                ["y"] = new BitChartScaleOptions { Id = "y", Type = BitChartScaleType.Linear, BeginAtZero = true, SuggestedMax = 4, Ticks = new BitChartTickOptions { MaxTicksLimit = 5, Precision = 0 } }
            }
        };

        daysOptions = new()
        {
            MaintainAspectRatio = false,
            Culture = culture,
            Plugins = new BitChartPluginOptions { Legend = new BitChartLegendOptions { Display = false } },
            Scales =
            {
                ["x"] = new BitChartScaleOptions { Id = "x", Type = BitChartScaleType.Category, Grid = new BitChartGridOptions { Display = false }, Ticks = new BitChartTickOptions { MaxRotation = 0 } },
                ["y"] = new BitChartScaleOptions { Id = "y", Type = BitChartScaleType.Linear, BeginAtZero = true, SuggestedMax = 4, Ticks = new BitChartTickOptions { MaxTicksLimit = 5, Precision = 0 } }
            }
        };

        teamsOptions = new()
        {
            MaintainAspectRatio = false,
            Culture = culture,
            Plugins = new BitChartPluginOptions { Legend = new BitChartLegendOptions { Display = false } },
            Scales =
            {
                ["x"] = new BitChartScaleOptions { Id = "x", Type = BitChartScaleType.Category, Grid = new BitChartGridOptions { Display = false } },
                ["y"] = new BitChartScaleOptions { Id = "y", Type = BitChartScaleType.Linear, BeginAtZero = true, SuggestedMax = 4, Ticks = new BitChartTickOptions { MaxTicksLimit = 5, Precision = 0 } }
            }
        };
    }

    /// <summary>
    /// The labels of the bars or points: a bar of a month not over yet says so, a running total doesn't need to, as its
    /// last point is today's.
    /// </summary>
    private List<string> Labels(GrowthReport r, bool isTotal = false)
    {
        var withYear = r.Bucket is GrowthBucket.Month && r.BucketStarts.Length > 12;

        // A long running total is read by its months, though it is drawn a point a week.
        var byMonth = isTotal && r.Bucket is GrowthBucket.Week && r.BucketStarts.Length > 26;

        return [.. r.BucketStarts.Select((start, index) =>
        {
            var label = byMonth ? GrowthFormat.BucketLabel(start, GrowthBucket.Month, withYear: true) : GrowthFormat.BucketLabel(start, r.Bucket, withYear);

            return isTotal is false && index == r.BucketStarts.Length - 1 && r.LastBucketIsPartial ? Localizer[nameof(AppStrings.GrowthSoFar), label].Value : label;
        })];
    }

    /// <summary>
    /// In a right-to-left language a chart's legend and tooltip read from the right, while time keeps running left to right
    /// in the plot, as in most Persian charts of figures (bitfabric-internal/specs/growth.md).
    /// </summary>
    private static BitDir? ChartDir => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? BitDir.Rtl : null;

    /// <summary>
    /// A few labels under a time axis, evenly spaced back from the last one, which is today's; the rest are left blank. The
    /// chart's own skipping keeps the last label and can lay it over the one before it (specs/workarounds.md).
    /// </summary>
    private static List<string> Spaced(List<string> labels, int count = 5)
    {
        var step = Math.Max(1, (int)Math.Ceiling((labels.Count - 1) / (double)(count - 1)));

        return [.. labels.Select((label, index) => (labels.Count - 1 - index) % step is 0 ? label : "")];
    }

    /// <summary>The tooltip's title is the full label of the point, which the axis may leave blank.</summary>
    private static BitChartTooltipOptions TitledBy(List<string> labels) => new()
    {
        Callbacks = new BitChartTooltipCallbacks { Title = items => items.Count > 0 && items[0].DataIndex < labels.Count ? labels[items[0].DataIndex] : "" }
    };

    /// <summary>A running total, drawn as a 2px line over a light wash of its color.</summary>
    private static BitChartDataset Line(string label, int[] values, string color) => new()
    {
        Label = label,
        Data = [.. values.Select(value => (double?)value)],
        BorderColor = $"var({color})",
        BackgroundColor = $"color-mix(in srgb, var({color}) 10%, transparent)",
        Fill = BitChartFillMode.Origin,
        BorderWidth = 2,
        PointRadius = 0,
        Tension = 0.3
    };

    /// <summary>Thin bars with rounded tops; a month not over yet is drawn lighter, so it doesn't read as a drop.</summary>
    private static BitChartDataset Bars(string label, int[] values, string color, bool lastIsPartial)
    {
        var dataset = new BitChartDataset
        {
            Label = label,
            Data = [.. values.Select(value => (double?)value)],
            BackgroundColor = $"var({color})",
            BorderRadius = 4,
            MaxBarThickness = 24
        };

        if (lastIsPartial)
        {
            dataset.BackgroundColors = [.. values.Select((_, index) => index == values.Length - 1 ? $"color-mix(in srgb, var({color}) 45%, transparent)" : $"var({color})")];
        }

        return dataset;
    }
}
