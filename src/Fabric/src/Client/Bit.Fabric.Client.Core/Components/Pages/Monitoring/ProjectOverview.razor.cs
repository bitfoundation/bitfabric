using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// A project's parts as its last report shows them, laid out like bit Fabric's own Operations page
/// (bitfabric-internal/specs/monitoring.md, the project page's cards take after it): a ring of the parts by status, the
/// counts that filter them, the parts that need attention, a search, and a card per part with how long it took against
/// its limit and its last results. Everything a card says about a part comes from the app's report, in the app's own
/// words; a part that only misses its settings is a setup finding, not a failure.
/// </summary>
public partial class ProjectOverview
{
    [Parameter, EditorRequired] public ProjectDetailsDto Details { get; set; } = default!;

    private string? searchText;
    private Filter statusFilter = Filter.All;
    private BitChartConfig ringChart = default!;

    private enum Filter { All, Attention, Failing, Degraded, NotConfigured, Healthy }

    private enum Tone { Healthy, Degraded, Failing, NotConfigured, Neutral }

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        ringChart = new()
        {
            Type = BitChartType.Doughnut,
            Options = new()
            {
                MaintainAspectRatio = false,
                CutoutPercentage = 76,
                CircumferenceDegrees = 360,
                // Starts at 12 o'clock; the chart measures its rotation from 3 o'clock.
                RotationDegrees = -90,
                Plugins = new()
                {
                    Legend = new() { Display = false },
                    Tooltip = new() { LabelFormatter = (_, value) => Localizer[nameof(AppStrings.MonitoringPartsCount), $"{value:N0}"] }
                }
            }
        };
    }

    protected override async Task OnParamsSetAsync()
    {
        await base.OnParamsSetAsync();

        var shown = Stats().Where(stat => Count(stat.Filter) > 0).ToArray();

        ringChart.Data = new()
        {
            Labels = [.. shown.Select(stat => stat.Label)],
            Datasets =
            [
                new()
                {
                    Data = [.. shown.Select(stat => (double?)Count(stat.Filter))],
                    BackgroundColors = [.. shown.Select(stat => $"var({ToneCssVar(stat.Tone)})")],
                    BorderWidth = 0,
                    SpacingArc = shown.Length > 1 ? 3 : 0
                }
            ]
        };
    }

    private (Filter Filter, Tone Tone, string Label)[] Stats() =>
    [
        (Filter.Failing, Tone.Failing, Localizer[nameof(AppStrings.MonitoringFailing)]),
        (Filter.Degraded, Tone.Degraded, Localizer[nameof(AppStrings.MonitoringDegraded)]),
        (Filter.NotConfigured, Tone.NotConfigured, Localizer[nameof(AppStrings.MonitoringNotConfigured)]),
        (Filter.Healthy, Tone.Healthy, Localizer[nameof(AppStrings.MonitoringWorks)])
    ];

    private (Filter Filter, Tone Tone, string Label)[] Chips() =>
    [
        (Filter.All, Tone.Neutral, Localizer[nameof(AppStrings.All)]),
        (Filter.Attention, Tone.Neutral, Localizer[nameof(AppStrings.MonitoringNeedsAttention)]),
        .. Stats()
    ];

    private static Tone ToneOf(ProjectCheckDto check) => check.IsNotConfigured ? Tone.NotConfigured : ToneOf(check.Status);

    private static Tone ToneOf(HealthCheckStatus status) => status switch
    {
        HealthCheckStatus.Healthy => Tone.Healthy,
        HealthCheckStatus.Degraded => Tone.Degraded,
        _ => Tone.Failing
    };

    /// <summary>Failing parts first, a part that only misses its settings after the ones that work less well.</summary>
    private static int Rank(Tone tone) => tone switch
    {
        Tone.Failing => 0,
        Tone.Degraded => 1,
        Tone.NotConfigured => 2,
        _ => 3
    };

    private bool Matches(ProjectCheckDto check, Filter filter) => filter switch
    {
        Filter.Attention => ToneOf(check) is not Tone.Healthy,
        Filter.Failing => ToneOf(check) is Tone.Failing,
        Filter.Degraded => ToneOf(check) is Tone.Degraded,
        Filter.NotConfigured => ToneOf(check) is Tone.NotConfigured,
        Filter.Healthy => ToneOf(check) is Tone.Healthy,
        _ => true
    };

    private int Count(Filter filter) => Details.Checks.Count(check => Matches(check, filter));

    private IEnumerable<ProjectCheckDto> Visible() => Details.Checks.Where(check => Matches(check, statusFilter)
        && (string.IsNullOrWhiteSpace(searchText)
            || ProjectChecks.TitleOf(check).Contains(searchText, StringComparison.CurrentCultureIgnoreCase)
            || check.Key?.Contains(searchText, StringComparison.OrdinalIgnoreCase) is true
            || check.About?.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) is true
            || check.Description?.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) is true));

    private void ClearFilters() => (statusFilter, searchText) = (Filter.All, null);

    private void ShowOnly(Filter filter) => (statusFilter, searchText) = (filter, null);

    private Tone OverallTone => Count(Filter.Failing) > 0 ? Tone.Failing
        : Count(Filter.Degraded) > 0 ? Tone.Degraded
        : Count(Filter.NotConfigured) > 0 ? Tone.NotConfigured
        : Tone.Healthy;

    /// <summary>
    /// bit Fabric can't read the app right now, as the page's banner says: the hero says so too, and that the parts below
    /// are its last report's, rather than a state the app may no longer be in.
    /// </summary>
    private bool IsStale => Details.Project?.ConnectionStatus is ProjectConnectionStatus.ReconnectNeeded or ProjectConnectionStatus.Disconnected;

    private Tone HeroTone => IsStale ? Tone.Neutral : OverallTone;

    private string OverallHeadline => (IsStale, Details.Project?.ConnectionStatus, OverallTone) switch
    {
        (true, ProjectConnectionStatus.Disconnected, _) => Localizer[nameof(AppStrings.MonitoringDisconnected)],
        (true, _, _) => Localizer[nameof(AppStrings.MonitoringCannotReadHealth)],
        (_, _, Tone.Healthy or Tone.NotConfigured) => Localizer[nameof(AppStrings.MonitoringAllSystemsOperational)],
        (_, _, Tone.Degraded) => Localizer[nameof(AppStrings.MonitoringRunningWithDegradedParts)],
        _ => Localizer[nameof(AppStrings.MonitoringSomePartsAreFailing)]
    };

    private string OverallExplanation => (IsStale, Details.CheckedAt, OverallTone) switch
    {
        (true, { } lastRead, _) => Localizer[nameof(AppStrings.MonitoringLastReportSaid), LocalTime(lastRead, "g")],
        (_, _, Tone.Healthy) => Localizer[nameof(AppStrings.MonitoringEveryPartPassed)],
        (_, _, Tone.NotConfigured) => Localizer[nameof(AppStrings.MonitoringEveryConfiguredPartPassed)],
        (_, _, Tone.Degraded) => Localizer[nameof(AppStrings.MonitoringDegradedExplanation)],
        _ => Localizer[nameof(AppStrings.MonitoringFailingExplanation)]
    };

    private string StatusText(Tone tone) => tone switch
    {
        Tone.Failing => Localizer[nameof(AppStrings.MonitoringFailing)],
        Tone.Degraded => Localizer[nameof(AppStrings.MonitoringDegraded)],
        Tone.NotConfigured => Localizer[nameof(AppStrings.MonitoringNotConfigured)],
        _ => Localizer[nameof(AppStrings.MonitoringWorks)]
    };

    /// <summary>How close a part came to its limit, in percent; a part without one can't be judged.</summary>
    private static double? TimeoutUsage(ProjectCheckDto check)
        => check.TimeoutMs is > 0 and var timeout ? Math.Min(100, 100.0 * check.DurationMs / timeout) : null;

    private string LocalTime(DateTimeOffset at, string format) => TimeZoneService.ToLocalTime(at).ToString(format, CultureInfo.CurrentUICulture);

    private string Ago(DateTimeOffset at)
    {
        var elapsed = DateTimeOffset.UtcNow - at;

        return elapsed.TotalSeconds switch
        {
            < 5 => Localizer[nameof(AppStrings.MonitoringJustNow)],
            < 60 => Localizer[nameof(AppStrings.MonitoringSecondsAgo), (int)elapsed.TotalSeconds],
            < 3600 => Localizer[nameof(AppStrings.MonitoringMinutesAgo), (int)elapsed.TotalMinutes],
            < 86400 => Localizer[nameof(AppStrings.MonitoringHoursAgo), (int)elapsed.TotalHours],
            _ => LocalTime(at, "g")
        };
    }

    private string FormatMs(int ms) => ms switch
    {
        < 1 => Localizer[nameof(AppStrings.MonitoringLessThanOneMs)],
        < 1000 => Localizer[nameof(AppStrings.AppHomeMs), ms],
        _ => Localizer[nameof(AppStrings.AppHomeS), (ms / 1000.0).ToString("N1", CultureInfo.CurrentUICulture)]
    };

    /// <summary>A report is as slow as its slowest part; a part that times out takes 10 to 20 seconds.</summary>
    private static BitColor DurationColor(int ms) => ms switch
    {
        < 2000 => BitColor.Success,
        < 8000 => BitColor.Warning,
        _ => BitColor.Error
    };

    private static BitColor UsageColor(double? usage) => usage switch
    {
        null => BitColor.SecondaryForeground,
        < 50 => BitColor.Success,
        < 80 => BitColor.Warning,
        _ => BitColor.Error
    };

    private static string ToneIcon(Tone tone) => tone switch
    {
        Tone.Healthy => BitIconName.CompletedSolid,
        Tone.Degraded => BitIconName.WarningSolid,
        Tone.Failing => BitIconName.StatusErrorFull,
        Tone.NotConfigured => BitIconName.Settings,
        _ => BitIconName.Info
    };

    private static BitColor ToneColor(Tone tone) => tone switch
    {
        Tone.Healthy => BitColor.Success,
        Tone.Degraded => BitColor.Warning,
        Tone.Failing => BitColor.Error,
        Tone.NotConfigured => BitColor.Info,
        _ => BitColor.SecondaryForeground
    };

    private static string ToneCssVar(Tone tone) => tone switch
    {
        Tone.Healthy => BitCss.Var.Color.Success.Main,
        Tone.Degraded => BitCss.Var.Color.Warning.Main,
        Tone.Failing => BitCss.Var.Color.Error.Main,
        Tone.NotConfigured => BitCss.Var.Color.Info.Main,
        _ => BitCss.Var.Color.Foreground.Secondary.Main
    };

    private static string ToneClass(Tone tone) => $"tone-{tone.ToString().ToLowerInvariant()}";

    private static string StatusClass(HealthCheckStatus status) => ToneClass(ToneOf(status));
}
