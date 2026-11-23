using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Status;

/// <summary>
/// A status page's 90 days, a bar a day in the reader's time zone, from a component's stretches down or degraded
/// (bitfabric-internal/specs/monitoring.md, Times follow the viewer's time zone). Time before the project was watched is
/// no data, not uptime.
/// </summary>
public static class StatusDays
{
    public enum Tone { Ok, Degraded, Down, Maintenance, NoData }

    public sealed record Day(DateOnly Date, Tone Tone, double? Uptime, int DownMinutes, int DegradedMinutes);

    public static Day[] Of(PublicStretchDto[] stretches, PublicMaintenanceDto[] maintenance, DateTimeOffset watchedSince, TimeZoneInfo zone, int count = 90)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

        return [.. Enumerable.Range(0, count).Select(index =>
        {
            var date = today.AddDays(index - count + 1);
            var start = StartOf(date, zone);
            var end = StartOf(date.AddDays(1), zone);
            var countedFrom = start > watchedSince ? start : watchedSince;
            var countedTo = end < now ? end : now;

            if (countedTo <= countedFrom)
                return new Day(date, Tone.NoData, null, 0, 0);

            var counted = (countedTo - countedFrom).TotalMinutes;
            var down = Minutes(stretches.Where(stretch => stretch.Impact is ProjectHealth.Down), countedFrom, countedTo);
            var degraded = Minutes(stretches.Where(stretch => stretch.Impact is ProjectHealth.Degraded), countedFrom, countedTo);
            var inMaintenance = maintenance.Any(window => Overlaps(window, start, end));

            var tone = down > 0 ? Tone.Down : degraded > 0 ? Tone.Degraded : inMaintenance ? Tone.Maintenance : Tone.Ok;
            return new Day(date, tone, 100 * Math.Max(0, counted - down) / counted, (int)Math.Round(down), (int)Math.Round(degraded));
        })];
    }

    public static double? UptimeOf(Day[] days)
    {
        var counted = days.Where(day => day.Uptime is not null).ToArray();
        return counted.Length is 0 ? null : counted.Average(day => day.Uptime!.Value);
    }

    private static double Minutes(IEnumerable<PublicStretchDto> stretches, DateTimeOffset from, DateTimeOffset to)
    {
        double total = 0;
        DateTimeOffset? end = null;

        foreach (var (start, stop) in stretches.Select(stretch => (From: stretch.From > from ? stretch.From : from, To: (stretch.To ?? DateTimeOffset.UtcNow) < to ? stretch.To ?? DateTimeOffset.UtcNow : to))
                                               .Where(stretch => stretch.To > stretch.From)
                                               .OrderBy(stretch => stretch.From))
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

    private static bool Overlaps(PublicMaintenanceDto window, DateTimeOffset from, DateTimeOffset to)
    {
        if (window.StartsAt < to && window.EndsAt > from)
            return true;

        if (window.RepeatsWeekly is false || window.StartsAt >= to)
            return false;

        var week = TimeSpan.FromDays(7);
        var start = window.StartsAt + week * Math.Max(0, (int)((from - window.StartsAt).Ticks / week.Ticks));

        for (; start < to; start += week)
        {
            if (start + (window.EndsAt - window.StartsAt) > from)
                return true;
        }

        return false;
    }

    private static DateTimeOffset StartOf(DateOnly date, TimeZoneInfo zone)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        while (zone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddHours(1);
        }

        return new(TimeZoneInfo.ConvertTimeToUtc(midnight, zone), TimeSpan.Zero);
    }
}
