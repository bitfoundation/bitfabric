using Bit.Fabric.Workbench.Components.Pages.Growth.Data;

namespace Bit.Fabric.Workbench.Components.Pages.Growth;

/// <summary>
/// A global admin page of projects and licenses over time: how many projects are made, how many of their Trials become a
/// License and how fast, and the accounts behind them, as totals only. Its figures come from a made-up sample world
/// (<see cref="GrowthSamples"/>), counted the way the server will (<see cref="GrowthReport.Compute"/>); the History switch
/// picks months of growth, a first day or a slow week, and the data state an empty start, loading or an error.
/// </summary>
public partial class GrowthPage
{
    public const string PageKey = "growth";

    private static readonly string ProjectsColor = BitCss.Var.Color.Primary.Main;

    private static readonly string LicensedColor = BitCss.Var.Color.Success.Main;

    private static readonly string ProductionColor = BitCss.Var.Color.Info.Main;

    private static readonly (GrowthPeriod Period, string Key)[] Periods =
    [
        (GrowthPeriod.Week, nameof(WorkbenchStrings.GrowthWeek)),
        (GrowthPeriod.Month, nameof(WorkbenchStrings.GrowthMonth)),
        (GrowthPeriod.Quarter, nameof(WorkbenchStrings.GrowthQuarter)),
        (GrowthPeriod.Year, nameof(WorkbenchStrings.GrowthYear)),
        (GrowthPeriod.All, nameof(WorkbenchStrings.GrowthAll))
    ];

    /// <summary>The page's switch for the period, which its buttons change too, so a link or a screenshot shows the one picked.</summary>
    public const string PeriodSwitch = "period";

    private GrowthPeriod Period => Enum.TryParse<GrowthPeriod>(Scenario.GetSwitch(PeriodSwitch), ignoreCase: true, out var period) ? period : GrowthPeriod.Month;

    private GrowthSampleWorld? world;
    private string? worldHistory;
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

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (IsPrerendering)
            return;

        ScenarioState.Changed += OnScenarioChanged;

        await Load();
    }

    /// <summary>The inspector picked another history or period: the page follows without a reload.</summary>
    private void OnScenarioChanged() => _ = InvokeAsync(async () =>
    {
        if (world is not null && worldHistory != Scenario.GetSwitch(GrowthSamples.HistorySwitch))
        {
            await Load();
        }
        else if (report is not null && report.Period != Period)
        {
            Compute();
        }

        StateHasChanged();
    });

    public override async ValueTask DisposeAsync()
    {
        ScenarioState.Changed -= OnScenarioChanged;

        await base.DisposeAsync();
    }

    private async Task Load()
    {
        loadFailed = false;
        report = null;

        try
        {
            var history = Scenario.GetSwitch(GrowthSamples.HistorySwitch);

            worldHistory = history;
            world = await ScenarioState.Load("growth", () => GrowthSamples.Build(history), () => GrowthSampleWorld.Empty(GrowthSamples.Today), CurrentCancellationToken);
        }
        catch (WorkbenchSimulatedException)
        {
            loadFailed = true;
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Compute();
    }

    private Task Retry() => Load();

    /// <summary>A period picked on the page: the view's switch follows, a month being its default.</summary>
    private async Task PickPeriod(string? key)
    {
        if (Enum.TryParse<GrowthPeriod>(key, out var period) is false || period == Period)
            return;

        await ReportScenario(Scenario.WithSwitch(PeriodSwitch, period is GrowthPeriod.Month ? null : period.ToString().ToLowerInvariant()));

        Compute();
    }

    private void Compute()
    {
        if (world is null)
            return;

        var calendar = CultureInfo.CurrentCulture.Calendar;

        report = GrowthReport.Compute(world, Period, calendar);

        // The big number's curve runs from the first project, whatever the period, so the whole climb shows, a point a week
        // that ends today, so no month cut short flattens its end.
        var sinceStart = GrowthReport.Compute(world, GrowthPeriod.All, calendar, GrowthBucket.Week);

        BuildCharts(report, sinceStart);
    }

    /// <summary>The line under the title: what the period is, and what its changes are compared with.</summary>
    private string WindowText => report is null ? "" : Period switch
    {
        GrowthPeriod.Week => Localizer[nameof(WorkbenchStrings.GrowthWindowWeek)].Value,
        GrowthPeriod.Month => Localizer[nameof(WorkbenchStrings.GrowthWindowMonth)].Value,
        GrowthPeriod.Quarter => Localizer[nameof(WorkbenchStrings.GrowthWindowQuarter)].Value,
        GrowthPeriod.Year => Localizer[nameof(WorkbenchStrings.GrowthWindowYear)].Value,
        _ => Localizer[nameof(WorkbenchStrings.GrowthWindowAll), FirstDayText].Value
    };

    private string FirstDayText => world?.Projects is { Length: > 0 } projects
        ? projects.Min(project => project.MadeOn).ToDateTime(TimeOnly.MinValue).ToString("d MMMM yyyy", CultureInfo.CurrentCulture)
        : "";

    private string EachTitle => report?.Bucket switch
    {
        GrowthBucket.Week => Localizer[nameof(WorkbenchStrings.GrowthEachWeek)].Value,
        GrowthBucket.Month => Localizer[nameof(WorkbenchStrings.GrowthEachMonth)].Value,
        _ => Localizer[nameof(WorkbenchStrings.GrowthEachDay)].Value
    };

    private string? ConversionNote => report switch
    {
        null or { Conversion: null } => null,
        { ConversionIsSoFar: true } => Localizer[nameof(WorkbenchStrings.GrowthConversionSoFar), GrowthFormat.Count(report.ConversionOf)].Value,
        _ => Localizer[nameof(WorkbenchStrings.GrowthConversionOf), GrowthFormat.Count(report.ConversionOf)].Value
    };

    /// <summary>A tile's line over the period, from where the data starts.</summary>
    private static double?[]? Line(GrowthReport? r, int[]? values) => r is null || values is null ? null : [.. values.Skip(r.EmptyLead).Select(value => (double?)value)];

    private static double?[]? Line(GrowthReport? r, double?[]? values) => r is null || values is null ? null : [.. values.Skip(r.EmptyLead)];

    /// <summary>Half the Licenses come within this: the same day, a day, or a number of days.</summary>
    private string MedianText(double median) => median switch
    {
        0 => Localizer[nameof(WorkbenchStrings.GrowthSameDay)].Value,
        1 => Localizer[nameof(WorkbenchStrings.GrowthOneDay)].Value,
        _ => Localizer[nameof(WorkbenchStrings.GrowthDays), GrowthFormat.Days(median)].Value
    };

    /// <summary>The share of the licensed projects that are live, under the count of those in production.</summary>
    private string? InProductionNote => report is { LicensedProjects: > 0 }
        ? Localizer[nameof(WorkbenchStrings.GrowthInProductionNote), GrowthFormat.Share(report.InProduction / (double)report.LicensedProjects)].Value
        : null;

    private string? OverTimeDescription => report is null ? null
        : Localizer[nameof(WorkbenchStrings.GrowthOverTimeDescription), GrowthFormat.Count(report.Projects), GrowthFormat.Count(report.LicensedProjects)].Value;

    private string? EachDescription => report is null ? null
        : Localizer[nameof(WorkbenchStrings.GrowthEachDescription), GrowthFormat.Count(report.ProjectsMade.Now), GrowthFormat.Count(report.ProjectsLicensed.Now)].Value;

    private void BuildCharts(GrowthReport r, GrowthReport sinceStart)
    {
        var labels = Labels(r);

        var culture = CultureInfo.CurrentCulture;

        var totalLabels = Labels(sinceStart, isTotal: true);

        overTimeData = new()
        {
            // Three labels: a month and its year, as Persian writes them, is long on a phone.
            Labels = Spaced(totalLabels, 3),
            Datasets =
            {
                Line(Localizer[nameof(WorkbenchStrings.GrowthSeriesProjects)], sinceStart.ProjectsTotal, ProjectsColor),
                Line(Localizer[nameof(WorkbenchStrings.GrowthSeriesLicensed)], sinceStart.LicensedTotal, LicensedColor)
            }
        };

        eachData = new()
        {
            Labels = Spaced(labels),
            Datasets =
            {
                Bars(Localizer[nameof(WorkbenchStrings.GrowthSeriesMade)], r.MadePerBucket, ProjectsColor, r.LastBucketIsPartial),
                Bars(Localizer[nameof(WorkbenchStrings.GrowthSeriesNewlyLicensed)], r.LicensedPerBucket, LicensedColor, r.LastBucketIsPartial)
            }
        };

        daysData = r.DaysToLicense.Sum() is 0 ? new() : new()
        {
            Labels =
            [
                Localizer[nameof(WorkbenchStrings.GrowthSameDay)],
                Localizer[nameof(WorkbenchStrings.GrowthDays1To7)],
                Localizer[nameof(WorkbenchStrings.GrowthDays8To14)],
                Localizer[nameof(WorkbenchStrings.GrowthDays15To30)],
                Localizer[nameof(WorkbenchStrings.GrowthAfterTrial)]
            ],
            Datasets = { Bars(Localizer[nameof(WorkbenchStrings.GrowthSeriesNewlyLicensed)], r.DaysToLicense, LicensedColor, false) }
        };

        teamsData = r.TeamsByProjects.Sum() is 0 ? new() : new()
        {
            Labels = ["1", "2", Localizer[nameof(WorkbenchStrings.GrowthProjects3To5)], Localizer[nameof(WorkbenchStrings.GrowthProjects6Plus)]],
            Datasets = { Bars(Localizer[nameof(WorkbenchStrings.GrowthSeriesTeams)], r.TeamsByProjects, ProjectsColor, false) }
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

            return isTotal is false && index == r.BucketStarts.Length - 1 && r.LastBucketIsPartial ? Localizer[nameof(WorkbenchStrings.GrowthSoFar), label].Value : label;
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
