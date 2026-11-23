using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// A project's last 90 days (bitfabric-internal/specs/monitoring.md, A report of how things went): a bar a day for the app
/// and for each part, drawn in the viewer's time zone at view time from the stored periods and incidents, so every zone's
/// days add up (Times follow the viewer's time zone); a click opens that day with its incidents, deployments and the parts'
/// stretches.
/// Time before the project was watched is no data, not uptime. The days are summed up by ProjectHistoryDays, which the AI
/// tools give them with too.
/// </summary>
public partial class ProjectHistory
{
    private const int DayCount = 90;

    /// <summary>The row of the app as a whole, drawn from its incidents.</summary>
    private const string AppRow = "$app";

    [Parameter, EditorRequired] public ProjectDto Project { get; set; } = default!;

    [AutoInject] private IProjectIncidentsController incidentsController = default!;

    private ProjectHistoryDto? history;
    private ProjectHistoryDay[] days = [];
    private (string Key, string Title, ProjectHistoryDay[] Days, double? Uptime)[] rows = [];
    private DateOnly? selectedDay;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        days = ProjectHistoryDays.Last(DayCount, TimeZoneService.CurrentTimeZone, DateTimeOffset.UtcNow);

        history = await incidentsController.GetHistory(new() { ProjectId = Project.Id, From = days[0].Start, To = DateTimeOffset.UtcNow }, CurrentCancellationToken);

        var now = DateTimeOffset.UtcNow;
        var appDays = ProjectHistoryDays.OfApp(history, days, now);

        // The parts as the server lists and names them (bitfabric-internal/specs/monitoring.md, A part counts only while the
        // app reports it).
        rows =
        [
            (AppRow, Project.Name ?? "", appDays, ProjectHistoryDays.UptimeOf(appDays)),
            .. history.Parts.OrderBy(part => part.Title, StringComparer.CurrentCultureIgnoreCase).Select(part =>
            {
                var partDays = ProjectHistoryDays.OfPart(history, part, days, now);
                return (part.Key!, part.Title ?? part.Key!, partDays, ProjectHistoryDays.UptimeOf(partDays));
            })
        ];
    }

    private void Select(ProjectHistoryDay day) => selectedDay = selectedDay == day.Date ? null : day.Date;

    private ProjectHistoryDay? Selected => selectedDay is { } date ? days.FirstOrDefault(day => day.Date == date) : null;

    private static string ToneClass(ProjectDayTone tone) => ProjectDayWords.ToneClass(tone);

    /// <summary>A part's day with no data while the project was watched is one the report didn't have it: added later, or taken out.</summary>
    private string DayLabel(string key, string title, ProjectHistoryDay day)
        => key != AppRow && day.Tone is ProjectDayTone.NoData && history is not null && day.End > history.WatchedSince
            ? Localizer[nameof(AppStrings.MonitoringDayNotInTheReport), title, day.Date.ToString("d", CultureInfo.CurrentUICulture)]
            : ProjectDayWords.Label(Localizer, title, day.Tone, day.Date, day.DownMinutes, day.DegradedMinutes);

    private string LocalTime(DateTimeOffset at, string format) => TimeZoneService.ToLocalTime(at).ToString(format, CultureInfo.CurrentUICulture);

    private static string Percent(double? value) => value is { } percent ? $"{percent.ToString(percent >= 99.995 ? "0" : "0.00", CultureInfo.CurrentUICulture)}%" : "—";
}
