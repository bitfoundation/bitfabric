using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// Beside the application map (See ProjectMap): the picked part's status, its last run and its last 24 hours, with the
/// way into its history, its performance and its incident; with nothing picked, how many parts work.
/// </summary>
public partial class ProjectMapDetails
{
    /// <summary>The project page's tabs this panel opens (See ProjectPage).</summary>
    public const string HistoryTab = "history";
    public const string IncidentsTab = "incidents";
    public const string PerformanceTab = "performance";

    private static readonly ProjectMapPartTone[] Tones = [ProjectMapPartTone.Failing, ProjectMapPartTone.Degraded, ProjectMapPartTone.Healthy, ProjectMapPartTone.NotConfigured];

    /// <summary>The picked part; null for the summary of them all.</summary>
    [Parameter] public ProjectMapPartDto? Part { get; set; }

    [Parameter] public ProjectMapPartDto[] Parts { get; set; } = [];

    [Parameter] public ProjectMapPartTone AppTone { get; set; }

    [Parameter] public EventCallback<string> OnOpenTab { get; set; }

    private int Count(ProjectMapPartTone tone) => Parts.Count(part => ProjectMapTones.ToneOf(part) == tone);

    private (string Label, int? Ms)[] Stats(ProjectMapPartDto part) =>
    [
        (Localizer[nameof(AppStrings.MonitoringAppMapLastRun)], part.LastMs),
        (Localizer[nameof(AppStrings.MonitoringAppMapTypical)], part.TypicalMs),
        (Localizer[nameof(AppStrings.MonitoringAppMapSlowest)], part.SlowestMs)
    ];

    private string TroubleText(ProjectMapPartDto part) => part.ProblemRounds is 0
        ? Localizer[nameof(AppStrings.MonitoringAppMapNoTrouble), part.Rounds]
        : Localizer[nameof(AppStrings.MonitoringAppMapTrouble), part.ProblemRounds, part.Rounds];

    private string StatusText(ProjectMapPartTone tone) => tone switch
    {
        ProjectMapPartTone.Failing => Localizer[nameof(AppStrings.MonitoringFailing)],
        ProjectMapPartTone.Degraded => Localizer[nameof(AppStrings.MonitoringDegraded)],
        ProjectMapPartTone.NotConfigured => Localizer[nameof(AppStrings.MonitoringNotConfigured)],
        _ => Localizer[nameof(AppStrings.MonitoringWorks)]
    };

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

    private string? Ms(int? ms) => ms switch
    {
        null => null,
        < 1 => Localizer[nameof(AppStrings.MonitoringLessThanOneMs)],
        < 1000 => Localizer[nameof(AppStrings.AppHomeMs), ms],
        _ => Localizer[nameof(AppStrings.AppHomeS), (ms.Value / 1000.0).ToString("N1", CultureInfo.CurrentUICulture)]
    };
}
