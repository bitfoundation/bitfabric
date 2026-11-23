using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

public static partial class MonitoringReads
{
    private static readonly int[] steps = [1, 15, 60, 360, 1440];

    /// <summary>How many points a row gets at most: the server makes the bars wider rather than send more than a row can draw.</summary>
    private const int MaxPointsPerRow = 800;

    /// <summary>
    /// How long each part took over a span, a point per bar: the home page from Germany and each location, the report as a
    /// whole, and each part the report has, slower and failing in their stretches, with the deployments and restarts.
    /// </summary>
    public static ProjectPerformanceDto Performance(SampleProject project, ProjectPerformanceRequestDto request, DateTimeOffset now)
    {
        var from = request.From > project.CreatedOn ? request.From : project.CreatedOn;
        var to = request.To < now ? request.To : now;

        var step = steps.FirstOrDefault(minutes => minutes >= request.StepMinutes, 1440);
        while (step < 1440 && (to - from).TotalMinutes / step > MaxPointsPerRow)
        {
            step = steps[Array.IndexOf(steps, step) + 1];
        }

        if (to <= from)
            return new() { StepMinutes = step };

        var stepTicks = TimeSpan.FromMinutes(step).Ticks;
        var first = new DateTimeOffset(from.UtcTicks / stepTicks * stepTicks, TimeSpan.Zero);
        var downs = project.Incidents.Where(incident => incident.Kind is ProjectIncidentKind.Incident && incident.Impact is ProjectHealth.Down).ToArray();
        // Nothing is read past the moment the approval stopped working, or once disconnected; the Network still times the home page until then.
        var reportUntil = project.ConnectionStatus is ProjectConnectionStatus.Connected ? now : project.CheckedAt ?? project.CreatedOn;
        var homeUntil = project.ConnectionStatus is ProjectConnectionStatus.Disconnected ? reportUntil : now;

        List<ProjectPerformanceSeriesDto> series = [];

        foreach (var region in new[] { "Germany" }.Concat(NetworkLocations.All.Where(location => project.Locations.Contains(location.Code)).Select(location => location.Name)))
        {
            var offset = MonitoringSamples.RegionOffsets.GetValueOrDefault(NetworkLocations.FindByName(region)?.Code ?? region, 0);
            series.Add(Series(ProjectRollupKeys.HomePage, region, at =>
            {
                var busy = 1 + 0.25 * Math.Max(0, Math.Sin((at.UtcDateTime.Hour - 8) / 24.0 * 2 * Math.PI));
                return project.BaseMs * busy + offset;
            }, (start, end) => (Minutes(downs.Select(incident => (incident.StartedAt, incident.EndedAt)), start, end, now), 0), homeUntil));
        }

        series.Add(Series(ProjectRollupKeys.Report, "Germany", _ => project.Checks.Where(check => check.IsNotConfigured is false).Select(check => (double)check.DurationMs).DefaultIfEmpty(40).Max() + 38,
            (start, end) => (Minutes(downs.Select(incident => (incident.StartedAt, incident.EndedAt)), start, end, now),
                             Minutes(project.Periods.Where(period => period.Status is HealthCheckStatus.Degraded).Select(period => (period.StartedAt, period.EndedAt)), start, end, now)), reportUntil));

        foreach (var check in project.Checks.Where(check => check.IsNotConfigured is false))
        {
            var part = MonitoringParts.All.FirstOrDefault(part => part.Key == check.Key);
            var periods = project.Periods.Where(period => period.CheckKey == check.Key).ToArray();
            var addedAt = project.PartSpans.TryGetValue(check.Key!, out var span) ? span.From : null;

            series.Add(Series(check.Key!, "Germany", _ => Math.Max(1, part?.TypicalMs ?? check.DurationMs),
                (start, end) => (Minutes(periods.Where(period => period.Status is HealthCheckStatus.Unhealthy).Select(period => (period.StartedAt, period.EndedAt)), start, end, now),
                                 Minutes(periods.Where(period => period.Status is HealthCheckStatus.Degraded).Select(period => (period.StartedAt, period.EndedAt)), start, end, now)),
                reportUntil, addedAt));
        }

        return new()
        {
            StepMinutes = step,
            Series = [.. series],
            Starts = [.. project.Starts.Where(start => start.At >= request.From && start.At < request.To)]
        };

        ProjectPerformanceSeriesDto Series(string key, string region, Func<DateTimeOffset, double> typicalMs, Func<DateTimeOffset, DateTimeOffset, (double Failed, double Degraded)> trouble, DateTimeOffset until, DateTimeOffset? since = null)
        {
            List<ProjectPerformancePointDto> points = [];

            for (var start = first; start < to && start < until; start = start.AddMinutes(step))
            {
                var end = start.AddMinutes(step);
                var countedFrom = Max(Max(start, project.CreatedOn), since ?? DateTimeOffset.MinValue);
                var countedTo = Min(Min(end, now), until);

                if (countedTo <= countedFrom)
                    continue;

                var count = Math.Max(1, (int)Math.Round((countedTo - countedFrom).TotalMinutes));
                var (failedMinutes, degradedMinutes) = trouble(countedFrom, countedTo);
                var failed = Math.Min(count, (int)Math.Ceiling(failedMinutes));
                var degraded = Math.Min(count - failed, (int)Math.Ceiling(degradedMinutes));
                var noise = MonitoringNoise.Of($"{project.Key}:{key}:{region}", start.ToUnixTimeSeconds() / 60);
                var average = typicalMs(start) * (0.8 + 0.4 * noise) * (degraded > 0 ? 2.6 : 1);

                points.Add(new()
                {
                    Start = start,
                    Count = count,
                    Failed = failed,
                    Degraded = degraded,
                    AverageMs = failed == count ? null : (int)Math.Round(average),
                    MaxMs = failed == count ? null : (int)Math.Round(average * (1.2 + (step is 1 ? 0 : 0.9 * MonitoringNoise.Of($"{project.Key}:{key}:max", start.ToUnixTimeSeconds() / 60))))
                });
            }

            return new() { Key = key, Region = region, Points = [.. points] };
        }
    }

    /// <summary>Minutes the stretches cover between two times, overlaps counted once; one still open lasts until now.</summary>
    private static double Minutes(IEnumerable<(DateTimeOffset From, DateTimeOffset? To)> stretches, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now)
    {
        double total = 0;
        DateTimeOffset? end = null;

        foreach (var (start, stop) in stretches.Select(stretch => (From: Max(stretch.From, from), To: Min(stretch.To ?? now, to))).Where(stretch => stretch.To > stretch.From).OrderBy(stretch => stretch.From))
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

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
