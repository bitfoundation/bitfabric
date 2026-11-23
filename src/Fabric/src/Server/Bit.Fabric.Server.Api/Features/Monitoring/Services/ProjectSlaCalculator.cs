using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// The SLA formula (bitfabric-internal/specs/monitoring.md, SLA reports), a pure function of what bit Fabric stored
/// about a month:
/// <c>availability = (counted − down − partial × w₁ − degraded × w₂) ÷ counted</c>.
/// <list type="bullet">
/// <item>Counted is the month bit Fabric watched and could read, outside maintenance: time before the project was added,
/// in a maintenance window (weekly ones too), in an incident that began in one, and without a reading (no round, or only
/// refused ones, such as while it needed reconnecting) are left out.</item>
/// <item>The app's own row comes from its incidents: a Down one is down, a Degraded one (a part failing while the app
/// works) is a partial outage.</item>
/// <item>A part's row comes from its periods: Unhealthy is down, Degraded a partial outage, and the app's Down incidents
/// take every part down with it, since a visitor can't use any part of an app that doesn't work.</item>
/// <item>Slow but working (w₂) has nothing to count yet: the template's report has no status for it, and bit Fabric
/// doesn't guess one from timings, so its minutes are zero until the report says so.</item>
/// </list>
/// </summary>
public static class ProjectSlaCalculator
{
    public readonly record struct Span(DateTimeOffset From, DateTimeOffset To)
    {
        public double Minutes => (To - From).TotalMinutes;
    }

    /// <summary>What a month's figures are counted from.</summary>
    public sealed record Inputs
    {
        /// <summary>Midnight of the month's first day in the project's time zone.</summary>
        public required DateTimeOffset From { get; init; }

        /// <summary>Midnight of the next month's first day.</summary>
        public required DateTimeOffset To { get; init; }

        public required DateTimeOffset Now { get; init; }

        /// <summary>When the project was added; nothing before it is counted.</summary>
        public required DateTimeOffset WatchedSince { get; init; }

        /// <summary>When bit Fabric had a reading of the app: its report, or the app not answering, which is a reading too.</summary>
        public IReadOnlyList<Span> Readings { get; init; } = [];

        public IReadOnlyList<ProjectMaintenanceWindow> Maintenance { get; init; } = [];

        public IReadOnlyList<ProjectIncident> Incidents { get; init; } = [];

        /// <summary>The parts' periods overlapping the month; none counts only the app's own row.</summary>
        public IReadOnlyList<ProjectCheckPeriod> Periods { get; init; } = [];
    }

    /// <summary>Minutes of a month, for the app or one part.</summary>
    public sealed record Figure(double CountedMinutes, double DownMinutes, double PartialMinutes, double DegradedMinutes)
    {
        public double Weighted(double partialWeight, double degradedWeight)
            => DownMinutes + PartialMinutes * partialWeight + DegradedMinutes * degradedWeight;

        /// <summary>A percentage, or null when no minute counted.</summary>
        public double? Availability(double partialWeight, double degradedWeight)
            => CountedMinutes <= 0 ? null : Math.Max(0, 100 * (CountedMinutes - Weighted(partialWeight, degradedWeight)) / CountedMinutes);
    }

    public sealed record Occurrence(ProjectMaintenanceWindow Window, Span Span, double CountedOutMinutes);

    public sealed record Result
    {
        public required Figure App { get; init; }

        public IReadOnlyList<(string Key, Figure Figure)> Parts { get; init; } = [];

        /// <summary>
        /// The minutes the whole month is expected to count, which the error budget is a share of: the month from when the
        /// project was added, less its maintenance (planned ones ahead included) and the time already without a reading.
        /// </summary>
        public double ExpectedMinutes { get; init; }

        /// <summary>Maintenance left out of the month so far, the incidents that began in it included.</summary>
        public double MaintenanceMinutes { get; init; }

        public double NoDataMinutes { get; init; }

        public double NotWatchedMinutes { get; init; }

        /// <summary>The maintenance windows' occurrences in the month that have begun.</summary>
        public IReadOnlyList<Occurrence> Maintenance { get; init; } = [];
    }

    public static Result Count(Inputs month)
    {
        var end = Min(month.To, month.Now);
        var watchedFrom = Max(month.From, month.WatchedSince);
        List<Span> watched = watchedFrom < end ? [new(watchedFrom, end)] : [];

        var occurrences = month.Maintenance.SelectMany(window => OccurrencesOf(window, month.From, month.To).Select(span => (Window: window, Span: span))).ToList();

        // An incident that began in a window alerted nobody and isn't held against the app (See ProjectIncident.DuringMaintenance).
        var leftOut = Normalize([.. occurrences.Select(occurrence => occurrence.Span),
                                 .. month.Incidents.Where(incident => incident.DuringMaintenance).Select(incident => SpanOf(incident.StartedAt, incident.EndedAt, month.Now))]);

        var readings = Normalize(month.Readings);
        var noData = Except(Except(watched, readings), leftOut);
        var counted = Except(Intersect(watched, readings), leftOut);

        var downIncidents = Normalize(month.Incidents.Where(incident => incident.DuringMaintenance is false && incident.Impact is ProjectHealth.Down)
                                                     .Select(incident => SpanOf(incident.StartedAt, incident.EndedAt, month.Now)));
        var partialIncidents = Normalize(month.Incidents.Where(incident => incident.DuringMaintenance is false && incident.Impact is ProjectHealth.Degraded)
                                                        .Select(incident => SpanOf(incident.StartedAt, incident.EndedAt, month.Now)));

        var appDown = Intersect(counted, downIncidents);
        var appPartial = Except(Intersect(counted, partialIncidents), appDown);

        List<(string Key, Figure Figure)> parts = [];

        foreach (var part in month.Periods.Where(period => period.CheckKey is not null).GroupBy(period => period.CheckKey!))
        {
            // A part is counted while the report had it; a part added or dropped during the month counts its own stretch.
            var partCounted = Intersect(counted, Normalize(part.Select(period => SpanOf(period.StartedAt, period.EndedAt, month.Now))));
            var unhealthy = part.Where(period => period.Status is HealthCheckStatus.Unhealthy).Select(period => SpanOf(period.StartedAt, period.EndedAt, month.Now));
            var degraded = Normalize(part.Where(period => period.Status is HealthCheckStatus.Degraded).Select(period => SpanOf(period.StartedAt, period.EndedAt, month.Now)));

            var partDown = Intersect(partCounted, Normalize([.. unhealthy, .. downIncidents]));
            var partPartial = Except(Intersect(partCounted, degraded), partDown);

            parts.Add((part.Key, new(Minutes(partCounted), Minutes(partDown), Minutes(partPartial), 0)));
        }

        List<Span> expected = watchedFrom < month.To ? [new(watchedFrom, month.To)] : [];

        return new()
        {
            App = new(Minutes(counted), Minutes(appDown), Minutes(appPartial), 0),
            Parts = parts,
            ExpectedMinutes = Minutes(Except(Except(expected, leftOut), noData)),
            MaintenanceMinutes = Minutes(Intersect(watched, leftOut)),
            NoDataMinutes = Minutes(noData),
            NotWatchedMinutes = end > month.From ? (end - month.From).TotalMinutes - Minutes(watched) : 0,
            Maintenance = [.. occurrences.Where(occurrence => occurrence.Span.From < end)
                                         .OrderBy(occurrence => occurrence.Span.From)
                                         .Select(occurrence => new Occurrence(occurrence.Window, occurrence.Span, Minutes(Intersect(watched, [occurrence.Span]))))]
        };
    }

    /// <summary>The tier the month falls into: the lowest threshold it is below, which gives the most back.</summary>
    public static ProjectSlaCreditDto? CreditOf(double? availability, IEnumerable<ProjectSlaCreditDto> credits)
        => availability is { } value
            ? credits.Where(credit => value < credit.BelowPercent).OrderBy(credit => credit.BelowPercent).FirstOrDefault()
            : null;

    /// <summary>A window's occurrences overlapping a span: one, or for a weekly one each week's from its first on.</summary>
    public static IEnumerable<Span> OccurrencesOf(ProjectMaintenanceWindow window, DateTimeOffset from, DateTimeOffset to)
    {
        var length = window.EndsAt - window.StartsAt;
        if (length <= TimeSpan.Zero)
            yield break;

        if (window.RepeatsWeekly is false)
        {
            if (window.StartsAt < to && window.EndsAt > from)
                yield return new(window.StartsAt, window.EndsAt);

            yield break;
        }

        var week = TimeSpan.FromDays(7);
        var start = window.StartsAt;

        if (from - length > start)
        {
            start += week * ((from - length - start).Ticks / week.Ticks);
        }

        for (; start < to; start += week)
        {
            if (start + length > from)
                yield return new(start, start + length);
        }
    }

    private static Span SpanOf(DateTimeOffset from, DateTimeOffset? to, DateTimeOffset now) => new(from, to ?? Max(from, now));

    /// <summary>Sorted, without empty spans, overlapping and touching ones merged.</summary>
    public static List<Span> Normalize(IEnumerable<Span> spans)
    {
        List<Span> merged = [];

        foreach (var span in spans.Where(span => span.To > span.From).OrderBy(span => span.From))
        {
            if (merged.Count > 0 && span.From <= merged[^1].To)
            {
                merged[^1] = merged[^1] with { To = Max(merged[^1].To, span.To) };
            }
            else
            {
                merged.Add(span);
            }
        }

        return merged;
    }

    /// <summary>Where both normalized lists have time.</summary>
    public static List<Span> Intersect(IReadOnlyList<Span> a, IReadOnlyList<Span> b)
    {
        List<Span> result = [];

        for (int i = 0, j = 0; i < a.Count && j < b.Count;)
        {
            var from = Max(a[i].From, b[j].From);
            var to = Min(a[i].To, b[j].To);

            if (to > from)
            {
                result.Add(new(from, to));
            }

            if (a[i].To < b[j].To) i++;
            else j++;
        }

        return result;
    }

    /// <summary>The time of normalized <paramref name="a"/> outside normalized <paramref name="b"/>.</summary>
    public static List<Span> Except(IReadOnlyList<Span> a, IReadOnlyList<Span> b)
    {
        List<Span> result = [];
        var j = 0;

        foreach (var span in a)
        {
            var from = span.From;

            while (j < b.Count && b[j].To <= from)
            {
                j++;
            }

            for (var k = j; k < b.Count && b[k].From < span.To; k++)
            {
                if (b[k].From > from)
                {
                    result.Add(new(from, b[k].From));
                }

                from = Max(from, b[k].To);
            }

            if (span.To > from)
            {
                result.Add(new(from, span.To));
            }
        }

        return result;
    }

    public static double Minutes(IEnumerable<Span> spans) => spans.Sum(span => span.Minutes);

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
