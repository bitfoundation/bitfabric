using Bit.Fabric.Shared.Features.Monitoring;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>How a day's bar reads, on a project's History tab and on its card alike.</summary>
public static class ProjectDayWords
{
    /// <summary>The tone a day's bar takes (See _monitoring.scss).</summary>
    public static string ToneClass(ProjectDayTone tone) => tone switch
    {
        ProjectDayTone.Down => "tone-down",
        ProjectDayTone.Degraded => "tone-degraded",
        ProjectDayTone.Maintenance => "tone-maintenance",
        ProjectDayTone.NoData => "tone-nodata",
        _ => "tone-ok"
    };

    /// <summary>"Email, 11/12/2026: 12 min down", a bar's tooltip and its name for a screen reader.</summary>
    public static string Label(IStringLocalizer localizer, string title, ProjectDayTone tone, DateOnly date, int downMinutes, int degradedMinutes)
    {
        var day = date.ToString("d", CultureInfo.CurrentUICulture);

        return tone switch
        {
            ProjectDayTone.NoData => localizer[nameof(AppStrings.MonitoringDayNotWatchedYet), title, day],
            ProjectDayTone.Down => localizer[nameof(AppStrings.MonitoringDayDown), title, day, Minutes(localizer, downMinutes)],
            ProjectDayTone.Degraded => localizer[nameof(AppStrings.MonitoringDayDegraded), title, day, Minutes(localizer, degradedMinutes)],
            ProjectDayTone.Maintenance => localizer[nameof(AppStrings.MonitoringDayMaintenance), title, day],
            _ => localizer[nameof(AppStrings.MonitoringDayWorkedAllDay), title, day]
        };
    }

    private static string Minutes(IStringLocalizer localizer, int minutes) => MonitoringWords.Duration(localizer, TimeSpan.FromMinutes(Math.Max(1, minutes)));
}
