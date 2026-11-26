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
    /// <summary>The separator between the parts of a line, which in Persian isn't a middle dot (See MonitoringWords.Dot).</summary>
    private string Dot => MonitoringWords.Dot(Localizer);

    private const int DayCount = 90;

    /// <summary>The row of the app as a whole, drawn from its incidents.</summary>
    private const string AppRow = "$app";

    [Parameter, EditorRequired] public MonitoringDto Project { get; set; } = default!;

    /// <summary>The one part to show, by its report key, as the map opens the tab on it (See ProjectTabLink); every part when null.</summary>
    [Parameter] public string? Check { get; set; }

    /// <summary>Shows every part again.</summary>
    [Parameter] public EventCallback OnShowEveryPart { get; set; }

    [AutoInject] private IProjectIncidentsController incidentsController = default!;

    private ProjectHistoryDto? history;
    private ProjectHistoryDay[] days = [];
    private (string Key, string Title, ProjectHistoryDay[] Days, double? Uptime)[] rows = [];
    private DateOnly? selectedDay;

    /// <summary>The part the tab shows alone, when the history has it; null shows them all.</summary>
    private ProjectHistoryPartDto? onlyPart;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        days = ProjectHistoryDays.Last(DayCount, TimeZoneService.CurrentTimeZone, DateTimeOffset.UtcNow);

        history = await incidentsController.GetHistory(new() { MonitoringId = Project.Id, From = days[0].Start, To = DateTimeOffset.UtcNow }, CurrentCancellationToken);

        var now = DateTimeOffset.UtcNow;
        var appDays = ProjectHistoryDays.OfApp(history, days, now);
        onlyPart = Check is null ? null : history.Parts.FirstOrDefault(part => part.Key == Check);

        // The parts as the server lists and names them (bitfabric-internal/specs/monitoring.md, A part counts only while the
        // app reports it).
        rows =
        [
            (AppRow, Project.Name ?? "", appDays, ProjectHistoryDays.UptimeOf(appDays)),
            .. history.Parts.Where(part => onlyPart is null || part.Key == onlyPart.Key)
                            .OrderBy(part => part.Title, StringComparer.CurrentCultureIgnoreCase).Select(part =>
            {
                var partDays = ProjectHistoryDays.OfPart(history, part, days, now);
                return (part.Key!, part.Title ?? part.Key!, partDays, ProjectHistoryDays.UptimeOf(partDays));
            })
        ];
    }

    private void Select(ProjectHistoryDay day) => selectedDay = selectedDay == day.Date ? null : day.Date;

    /// <summary>On one part, a day's incidents are those about it, and its stretches its own.</summary>
    private bool IsAboutTheShownPart(ProjectIncidentDto incident) => onlyPart is null || incident.CheckKeys.Contains(onlyPart.Key);

    private bool IsAboutTheShownPart(ProjectPeriodDto period) => onlyPart is null || period.CheckKey == onlyPart.Key;

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
