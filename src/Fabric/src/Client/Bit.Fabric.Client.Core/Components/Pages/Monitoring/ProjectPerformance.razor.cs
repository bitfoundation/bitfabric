using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// A project's Performance tab (bitfabric-internal/specs/monitoring.md, Performance shows every minute): a row per part of
/// the app on <see cref="BitResourceTimeline"/>, a bar per minute when zoomed in and per 15 minutes or hour from the
/// rollups beyond, its height the time the part took and its color how it did. Bars load for the window on screen, at the
/// width the zoom can draw, and load again at another width when the zoom crosses into it. Times are in the user's chosen
/// zone.
/// </summary>
public partial class ProjectPerformance
{
    [Parameter, EditorRequired] public ProjectDto Project { get; set; } = default!;

    /// <summary>The parts of the last report, which make the rows.</summary>
    [Parameter] public ProjectCheckDto[] Checks { get; set; } = [];

    /// <summary>Where the rounds run from, a home page row each.</summary>
    [Parameter] public string[] Regions { get; set; } = [];

    /// <summary>
    /// The one part to show, by its report key, as the map opens the tab on it (See ProjectTabLink): its row and the
    /// deployments and restarts beside it. Every row when null.
    /// </summary>
    [Parameter] public string? Check { get; set; }

    /// <summary>Shows every part again.</summary>
    [Parameter] public EventCallback OnShowEveryPart { get; set; }

    /// <summary>The part the tab shows alone, when the last report has it.</summary>
    private ProjectCheckDto? OnlyPart => Check is null ? null : Checks.FirstOrDefault(check => check.Key == Check && check.IsNotConfigured is false);

    [AutoInject] private IProjectsController projectsController = default!;

    private const string StartsRow = "starts";

    private BitResourceTimeline? timeline;
    private BitResourceTimelineConfig? config;
    private BitResourceTimelineOptions options = new();
    private HashSet<string> rows = [];

    /// <summary>The bar width the zoom asks for, and the one on screen (the server may widen it).</summary>
    private int wantedStep = 15;
    private int shownStep = 15;
    private double? shownDays = 1;
    private CancellationTokenSource? reloadDelay;

    private int TimelineHeight => Math.Clamp(56 + rows.Count * 30, 260, 640);

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();
        Build();
    }

    private void Build()
    {
        var now = DateTimeOffset.UtcNow;
        var regions = Regions;

        List<BitResourceTimelineResource> resources = OnlyPart is { } onlyPart
        ?
        [
            new() { Id = onlyPart.Key!, Name = ProjectChecks.TitleOf(onlyPart) },
            new() { Id = StartsRow, Name = Localizer[nameof(AppStrings.MonitoringDeploysAndRestarts)] }
        ]
        :
        [
            .. regions.Select(region => new BitResourceTimelineResource { Id = $"{ProjectRollupKeys.HomePage}|{region}", Name = Localizer[nameof(AppStrings.MonitoringHomePageRegion), MonitoringWords.Region(Localizer, region)] }),
            new() { Id = ProjectRollupKeys.Report, Name = Localizer[nameof(AppStrings.MonitoringHealthReport)] },
            // Right under the timings, so a slower stretch that starts at a deployment reads as its likely cause.
            new() { Id = StartsRow, Name = Localizer[nameof(AppStrings.MonitoringDeploysAndRestarts)] }
        ];

        // A part that only misses its settings has no history to draw (bitfabric-internal/specs/monitoring.md, Not
        // configured is a setup finding).
        foreach (var group in Checks.Where(check => check.IsNotConfigured is false && OnlyPart is null)
                                    .GroupBy(ProjectChecks.GroupOf)
                                    .OrderBy(group => ProjectChecks.OrderOf(group.Key)).ThenBy(group => group.Key))
        {
            resources.Add(new() { Id = $"group:{group.Key}", Name = Localizer[ProjectChecks.TitleOf(group.Key)] });

            // By the name the app gives them.
            foreach (var (check, name) in group.Select(check => (Check: check, Name: ProjectChecks.TitleOf(check))).OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                resources.Add(new() { Id = check.Key!, Name = name, ParentId = $"group:{group.Key}" });
            }
        }

        rows = [.. resources.Select(resource => resource.Id)];

        // The project's whole kept history, a day at least: rollups reach 13 months back.
        var start = new DateTimeOffset(Min(Project.CreatedOn, now.AddDays(-1)).UtcDateTime.Date, TimeSpan.Zero);
        config = new() { Resources = resources, StartDate = Max(start, now.AddDays(-395)), EndDate = now.AddHours(1), Allocations = [] };

        options = new()
        {
            // Html, since the bars take the theme's colors through its CSS variables, which a canvas can't read.
            Renderer = BitResourceTimelineRendererType.Html,
            TimeZone = IanaId(TimeZoneService.CurrentTimeZone),
            Locale = CultureInfo.CurrentUICulture.Name,
            ResourceAxisWidth = 190,
            ResourceAxisMinWidth = 120,
            ResourceHeight = 30,
            BarHeight = 14,
            MinBarWidth = 2,
            // A row is one series: two rounds in a minute show as one bar, never as a second lane.
            MaxStackLanes = 1,
            StackOnLabelCollision = false,
            StackLabelClearance = false,
            NonWorkingDays = [],
            MaxPixelsPerHour = 2400,
            AutoScrollToNow = true,
            PreserveScrollOnReload = true,
            WindowBufferFactor = 0.5,
            ShowTooltips = true,
            TooltipDelayMs = 150
        };
    }

    /// <summary>The bars of a window, at the width the zoom asked for (See OnViewChanged).</summary>
    private async Task<IReadOnlyList<BitResourceTimelineAllocation>> Load(BitResourceTimelineWindow window)
    {
        var performance = await projectsController.GetPerformance(new()
        {
            ProjectId = Project.Id,
            From = window.Start,
            To = window.End,
            StepMinutes = wantedStep
        }, CurrentCancellationToken);

        if (shownStep != performance.StepMinutes)
        {
            shownStep = performance.StepMinutes;
            StateHasChanged();
        }

        List<BitResourceTimelineAllocation> bars = [];

        foreach (var series in performance.Series)
        {
            var row = series.Key is ProjectRollupKeys.HomePage ? $"{series.Key}|{series.Region}" : series.Key!;

            // A part the last report no longer has keeps its history, but not a row.
            if (rows.Contains(row) is false)
                continue;

            var title = series.Key switch
            {
                ProjectRollupKeys.HomePage => Localizer[nameof(AppStrings.MonitoringHomePageRegion), MonitoringWords.Region(Localizer, series.Region!)].Value,
                ProjectRollupKeys.Report => Localizer[nameof(AppStrings.MonitoringHealthReport)].Value,
                _ => Checks.FirstOrDefault(check => check.Key == series.Key) is { } check ? ProjectChecks.TitleOf(check) : series.Key
            };

            bars.AddRange(series.Points.Select(point => Bar(row, title, point, performance.StepMinutes)));
        }

        bars.AddRange(performance.Starts.Select(start => new BitResourceTimelineAllocation
        {
            Id = $"start:{start.At.UtcTicks}:{start.IsDeploy}", // The same deploy in the next window replaces itself rather than drawing twice.
            ResourceId = StartsRow,
            StartTime = start.At,
            EndTime = start.At.AddMinutes(1),
            // Secondary: the one theme color no state uses, so a deploy never reads as a failure.
            Color = start.IsDeploy ? $"var({BitCss.Var.Color.Secondary.Main})" : $"color-mix(in srgb, var({BitCss.Var.Color.Secondary.Main}) 45%, transparent)",
            Height = 18,
            Tooltip = $"{(start.IsDeploy ? Localizer[nameof(AppStrings.MonitoringDeploy)] : Localizer[nameof(AppStrings.MonitoringRestart)])} · {Format(start.At, "g")}\n{start.ApplicationVersion}",
            Locked = true
        }));

        return bars;
    }

    private BitResourceTimelineAllocation Bar(string row, string title, ProjectPerformancePointDto point, int step)
    {
        var (tone, state) = point.Failed > 0 ? (BitCss.Var.Color.Error.Main, Localizer[nameof(AppStrings.MonitoringFailingOrNotAnsweringLower)].Value)
                          : point.Degraded > 0 ? (BitCss.Var.Color.Warning.Main, Localizer[nameof(AppStrings.MonitoringSlowOrPartlyWorkingLower)].Value)
                          : (BitCss.Var.Color.Success.Main, Localizer[nameof(AppStrings.MonitoringWorksLower)].Value);

        var end = point.Start.AddMinutes(step);
        var when = step is 1 ? Format(point.Start, "t") : $"{Format(point.Start, "g")} – {Format(end, "t")}";

        string[] lines = step is 1
            ? [$"{title} · {when}", point.AverageMs is { } ms ? Localizer[nameof(AppStrings.MonitoringBarMsState), ms, state] : Localizer[nameof(AppStrings.MonitoringBarNoAnswerState), state]]
            : [
                $"{title} · {when}",
                point.AverageMs is { } average ? Localizer[nameof(AppStrings.MonitoringBarAverageAndSlowest), average, point.MaxMs ?? average] : Localizer[nameof(AppStrings.MonitoringBarNoAnswer)],
                point.Failed + point.Degraded > 0
                    ? Localizer[nameof(AppStrings.MonitoringBarChecksFailedSlow), point.Count, point.Failed, point.Degraded]
                    : Localizer[nameof(AppStrings.MonitoringBarChecksAllFine), point.Count]
              ];

        return new()
        {
            Id = $"{row}|{point.Start.UtcTicks}",
            ResourceId = row,
            StartTime = point.Start,
            EndTime = end,
            Color = $"var({tone})",
            // No answer is drawn as tall as the slowest, so it stands out rather than vanishing.
            Height = HeightOf(point.AverageMs ?? 10_000),
            Tooltip = string.Join("\n", lines),
            Locked = true
        };
    }

    /// <summary>4 to 24 pixels on a log scale up to 10 seconds: 10 ms and 100 ms still look different, and so do 1 s and 5 s.</summary>
    private static int HeightOf(int ms) => (int)Math.Round(4 + 20 * Math.Min(1, Math.Log10(1 + ms) / 4));

    /// <summary>The bar width a scale can draw at three pixels or more: a minute, 15 minutes, an hour or six.</summary>
    private static int StepFor(double pixelsPerHour) => pixelsPerHour switch
    {
        >= 180 => 1,
        >= 12 => 15,
        >= 3 => 60,
        _ => 360
    };

    /// <summary>When the zoom crosses into another bar width, the bars load again at it, once the zoom settles.</summary>
    private async Task OnViewChanged(BitResourceTimelineView view)
    {
        var step = StepFor(view.PixelsPerHour);
        if (step == wantedStep)
            return;

        wantedStep = step;

        reloadDelay?.Cancel();
        reloadDelay = new();
        var token = reloadDelay.Token;

        try
        {
            await Task.Delay(300, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (wantedStep != shownStep && config is not null)
        {
            // A new Config in windowed mode loads the window on screen again; PreserveScrollOnReload keeps the view.
            config = new() { Resources = config.Resources, StartDate = config.StartDate, EndDate = config.EndDate, Allocations = [] };
            StateHasChanged();
        }
    }

    private async Task Show(double days)
    {
        shownDays = days;
        if (timeline is null)
            return;

        await timeline.ZoomToDaysAsync(days);
        await timeline.GoToTodayAsync();
    }

    private async Task ZoomIn()
    {
        shownDays = null;
        if (timeline is not null) await timeline.ZoomInAsync();
    }

    private async Task ZoomOut()
    {
        shownDays = null;
        if (timeline is not null) await timeline.ZoomOutAsync();
    }

    private async Task GoToNow()
    {
        if (timeline is not null) await timeline.GoToTodayAsync();
    }

    private IEnumerable<(double Days, string Label)> Ranges()
    {
        yield return (0.25, Localizer[nameof(AppStrings.Monitoring6Hours)]);
        yield return (1, Localizer[nameof(AppStrings.Monitoring1Day)]);
        yield return (3, Localizer[nameof(AppStrings.Monitoring3Days)]);
        yield return (14, Localizer[nameof(AppStrings.Monitoring2Weeks)]);
    }

    private string StepText => shownStep switch
    {
        1 => Localizer[nameof(AppStrings.MonitoringBarIsOneMinute)],
        15 => Localizer[nameof(AppStrings.MonitoringBarIs15Minutes)],
        60 => Localizer[nameof(AppStrings.MonitoringBarIsAnHour)],
        _ => Localizer[nameof(AppStrings.MonitoringBarIsHours), shownStep / 60]
    };

    private string Format(DateTimeOffset at, string format) => TimeZoneService.ToLocalTime(at).ToString(format, CultureInfo.CurrentUICulture);

    /// <summary>The timeline draws in an IANA zone; a Blazor Server circuit on Windows holds a Windows id.</summary>
    private static string IanaId(TimeZoneInfo zone)
        => zone.HasIanaId ? zone.Id : TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : "UTC";

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        reloadDelay?.Cancel();
        reloadDelay?.Dispose();
        await base.DisposeAsync(disposing);
    }
}
