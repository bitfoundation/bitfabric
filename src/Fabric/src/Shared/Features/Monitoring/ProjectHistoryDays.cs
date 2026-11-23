using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Shared.Features.Monitoring;

/// <summary>How a day of a project went, as History's day bars color it: down beats degraded beats maintenance.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectDayTone>))]
public enum ProjectDayTone { Ok, Degraded, Down, Maintenance, NoData }

/// <summary>
/// A day in the time zone it is drawn in, from its midnight (<c>Start</c>, as an instant) to the next (<c>End</c>);
/// <c>Uptime</c> is the share of its watched time it wasn't down, as a percentage, and null for a day not watched.
/// </summary>
public sealed record ProjectHistoryDay(DateOnly Date, DateTimeOffset Start, DateTimeOffset End, ProjectDayTone Tone, double? Uptime, int DownMinutes, int DegradedMinutes, bool Maintenance);

/// <summary>
/// A project's days in a time zone, summed up from what its History holds (bitfabric-internal/specs/monitoring.md, Times
/// follow the viewer's time zone): the app's from its incidents, a part's from its periods, and the time before the project
/// was watched as no data, not uptime. The History tab draws them in the viewer's zone and the AI tools give them in the
/// zone they're asked for, from this one code.
/// </summary>
public static class ProjectHistoryDays
{
    /// <summary>The last <paramref name="count"/> days in <paramref name="zone"/> up to today, oldest first, not summed up yet.</summary>
    public static ProjectHistoryDay[] Last(int count, TimeZoneInfo zone, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

        return [.. Enumerable.Range(0, count).Select(index =>
        {
            var date = today.AddDays(index - count + 1);
            return new ProjectHistoryDay(date, StartOf(date, zone), StartOf(date.AddDays(1), zone), ProjectDayTone.NoData, null, 0, 0, false);
        })];
    }

    /// <summary>The app as a whole, from its incidents: a Down one is down, any other degraded.</summary>
    public static ProjectHistoryDay[] OfApp(ProjectHistoryDto history, IEnumerable<ProjectHistoryDay> days, DateTimeOffset now)
    {
        return [.. days.Select(day => Summarize(day, history,
            history.Incidents.Where(incident => incident.Impact is ProjectHealth.Down).Select(incident => (incident.StartedAt, incident.EndedAt)),
            history.Incidents.Where(incident => incident.Impact is not ProjectHealth.Down).Select(incident => (incident.StartedAt, incident.EndedAt)),
            now))];
    }

    /// <summary>
    /// A part of the app, from its periods, over the time the app reported it: Unhealthy is down, Degraded degraded, and a
    /// day before the part was added to the report, or after it was taken out, has no data rather than a healthy day.
    /// </summary>
    public static ProjectHistoryDay[] OfPart(ProjectHistoryDto history, ProjectHistoryPartDto part, IEnumerable<ProjectHistoryDay> days, DateTimeOffset now)
    {
        var periods = history.Periods.Where(period => period.CheckKey == part.Key).ToArray();

        return [.. days.Select(day => Summarize(day, history,
            periods.Where(period => period.Status is HealthCheckStatus.Unhealthy).Select(period => (period.StartedAt, period.EndedAt)),
            periods.Where(period => period.Status is HealthCheckStatus.Degraded).Select(period => (period.StartedAt, period.EndedAt)),
            now,
            part.ReportedFrom,
            part.ReportedTo))];
    }

    /// <summary>The average of the days that were watched; null when none was.</summary>
    public static double? UptimeOf(IEnumerable<ProjectHistoryDay> days)
    {
        var counted = days.Where(day => day.Uptime is not null).ToArray();
        return counted.Length is 0 ? null : counted.Average(day => day.Uptime!.Value);
    }

    /// <summary>A day's state from the stretches that overlap it: down beats degraded beats maintenance.</summary>
    private static ProjectHistoryDay Summarize(ProjectHistoryDay day,
        ProjectHistoryDto history,
        IEnumerable<(DateTimeOffset From, DateTimeOffset? To)> down,
        IEnumerable<(DateTimeOffset From, DateTimeOffset? To)> degraded,
        DateTimeOffset now,
        DateTimeOffset? reportedFrom = null,
        DateTimeOffset? reportedTo = null)
    {
        var countedFrom = Max(Max(day.Start, history.WatchedSince), reportedFrom ?? DateTimeOffset.MinValue);
        var countedTo = Min(Min(day.End, now), reportedTo ?? DateTimeOffset.MaxValue);

        if (countedTo <= countedFrom)
            return day with { Tone = ProjectDayTone.NoData };

        var counted = (countedTo - countedFrom).TotalMinutes;
        var downMinutes = Minutes(down, countedFrom, countedTo, now);
        var degradedMinutes = Minutes(degraded, countedFrom, countedTo, now);
        var maintenance = history.Maintenance.Any(window => Overlaps(window, day.Start, day.End));

        var tone = downMinutes > 0 ? ProjectDayTone.Down : degradedMinutes > 0 ? ProjectDayTone.Degraded : maintenance ? ProjectDayTone.Maintenance : ProjectDayTone.Ok;

        return day with
        {
            Tone = tone,
            Uptime = 100 * Math.Max(0, counted - downMinutes) / counted,
            DownMinutes = (int)Math.Round(downMinutes),
            DegradedMinutes = (int)Math.Round(degradedMinutes),
            Maintenance = maintenance
        };
    }

    /// <summary>Minutes the stretches cover between two times, overlaps counted once; one still open lasts until now.</summary>
    private static double Minutes(IEnumerable<(DateTimeOffset From, DateTimeOffset? To)> stretches, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now)
    {
        var clipped = stretches.Select(stretch => (From: Max(stretch.From, from), To: Min(stretch.To ?? now, to)))
                               .Where(stretch => stretch.To > stretch.From)
                               .OrderBy(stretch => stretch.From);

        double total = 0;
        DateTimeOffset? end = null;

        foreach (var (start, stop) in clipped)
        {
            var effectiveStart = end is { } previous && previous > start ? previous : start;
            if (stop > effectiveStart)
            {
                total += (stop - effectiveStart).TotalMinutes;
                end = stop;
            }
        }

        return total;
    }

    /// <summary>A window, or for a weekly one any of its occurrences, overlapping a span.</summary>
    private static bool Overlaps(ProjectMaintenanceWindowDto window, DateTimeOffset from, DateTimeOffset to)
    {
        if (window.StartsAt < to && window.EndsAt > from)
            return true;

        if (window.RepeatsWeekly is false || window.StartsAt >= to)
            return false;

        var week = TimeSpan.FromDays(7);
        var weeks = Math.Max(0, (int)((from - window.StartsAt).Ticks / week.Ticks));
        var start = window.StartsAt + week * weeks;
        var length = window.EndsAt - window.StartsAt;

        for (; start < to; start += week)
        {
            if (start + length > from)
                return true;
        }

        return false;
    }

    /// <summary>A day's start in the zone, in UTC; a midnight a clock change skips starts at the hour after it.</summary>
    public static DateTimeOffset StartOf(DateOnly date, TimeZoneInfo zone)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        while (zone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddHours(1);
        }

        return new(TimeZoneInfo.ConvertTimeToUtc(midnight, zone), TimeSpan.Zero);
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
