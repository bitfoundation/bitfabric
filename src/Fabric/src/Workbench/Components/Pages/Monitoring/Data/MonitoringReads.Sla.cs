using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

public static partial class MonitoringReads
{
    /// <summary>Months before the last 90 days are counted from what is kept of them: their figures, not their stretches.</summary>
    private static readonly TimeSpan detailKept = TimeSpan.FromDays(90);

    private sealed record SlaCount(double Counted, double Down, double Partial, double Degraded, double Maintenance, double NoData, double NotWatched, ProjectSlaMaintenanceDto[] Windows, double BudgetBase);

    /// <summary>
    /// A month's SLA report, in the project's time zone, counted the way the formula of bitfabric-internal/specs/monitoring.md
    /// counts it: the app's down minutes in full, a part failing at its weight, a slow part at its own, maintenance and the time
    /// nothing was read left out.
    /// </summary>
    public static ProjectSlaReportDto SlaReport(SampleProject project, int? year, int? month, DateTimeOffset now)
    {
        var zone = ZoneOf(project.TimeZoneId);
        var (thisYear, thisMonth) = ProjectSlaMonths.MonthOf(now, zone);
        var (y, m) = (year ?? thisYear, month ?? thisMonth);
        var settings = project.Sla;

        var from = ProjectSlaMonths.StartOf(y, m, zone);
        var (nextYear, nextMonth) = ProjectSlaMonths.Add(y, m, 1);
        var to = ProjectSlaMonths.StartOf(nextYear, nextMonth, zone);
        var count = Count(project, from, to, now, null);
        var app = Row(null, project.Name, count, settings);
        var budget = count.BudgetBase * (100 - settings.TargetPercent) / 100;

        return new()
        {
            MonitoringId = project.Id,
            ProjectName = project.Name,
            Address = project.Address,
            TimeZoneId = project.TimeZoneId,
            Year = y,
            Month = m,
            From = from,
            To = to,
            IsCurrent = now >= from && now < to,
            GeneratedAt = now,
            Settings = settings,
            App = app,
            Parts = [.. project.Checks.Where(check => check.IsNotConfigured is false)
                                      .OrderBy(check => check.Title, StringComparer.CurrentCultureIgnoreCase)
                                      .Select(check => Row(check.Key, check.Title, Count(project, from, to, now, check.Key), settings))],
            BudgetMinutes = budget,
            LeftMinutes = Math.Max(0, budget - app.WeightedMinutes),
            Credit = app.Availability is { } availability ? settings.Credits.OrderByDescending(credit => credit.BelowPercent).LastOrDefault(credit => availability < credit.BelowPercent) : null,
            Months = [.. Enumerable.Range(-11, 12).Select(offset =>
            {
                var (monthYear, monthNumber) = ProjectSlaMonths.Add(thisYear, thisMonth, offset);
                var monthFrom = ProjectSlaMonths.StartOf(monthYear, monthNumber, zone);
                var (followingYear, followingMonth) = ProjectSlaMonths.Add(monthYear, monthNumber, 1);
                var row = Row(null, project.Name, Count(project, monthFrom, ProjectSlaMonths.StartOf(followingYear, followingMonth, zone), now, null), settings);

                return new ProjectSlaMonthDto { Year = monthYear, Month = monthNumber, Availability = row.Availability, IsCurrent = offset is 0 };
            })],
            Maintenance = count.Windows,
            MaintenanceMinutes = count.Maintenance,
            NoDataMinutes = count.NoData,
            NotWatchedMinutes = count.NotWatched
        };
    }

    private static ProjectSlaRowDto Row(string? key, string? title, SlaCount count, ProjectSlaSettingsDto settings)
    {
        var weighted = count.Down + count.Partial * settings.PartialWeight + count.Degraded * settings.DegradedWeight;

        return new()
        {
            Key = key,
            Title = title,
            Availability = count.Counted >= 1 ? Math.Clamp(100 * (1 - weighted / count.Counted), 0, 100) : null,
            CountedMinutes = count.Counted,
            DownMinutes = count.Down,
            PartialMinutes = count.Partial,
            DegradedMinutes = count.Degraded,
            WeightedMinutes = weighted
        };
    }

    /// <summary>A month's minutes for the app, or for one of its parts, from its stretches, or from what's kept of an older month.</summary>
    private static SlaCount Count(SampleProject project, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now, string? partKey)
    {
        var watchedFrom = Max(from, project.CreatedOn);
        var end = Min(to, now);
        var notWatched = Math.Max(0, (Min(watchedFrom, end) - from).TotalMinutes);

        if (end <= watchedFrom)
            return new(0, 0, 0, 0, 0, 0, notWatched, [], 0);

        var windows = Occurrences(project, watchedFrom, end).ToArray();
        var maintenance = windows.Sum(window => window.Minutes);
        var noData = project.ConnectionStatus is ProjectConnectionStatus.Connected || project.CheckedAt is not { } lastRead ? 0 : Math.Max(0, (end - Max(lastRead, watchedFrom)).TotalMinutes);
        var counted = Math.Max(0, (end - watchedFrom).TotalMinutes - maintenance - noData);
        var budgetBase = Math.Max(0, (to - watchedFrom).TotalMinutes - maintenance);

        double down, partial, degraded;

        if (end < now - detailKept)
        {
            var seed = from.UtcDateTime.Year * 12 + from.UtcDateTime.Month;
            var n = MonitoringNoise.Of($"{project.Key}:sla:{partKey}", seed);
            down = Math.Round(n * n * 38);
            partial = partKey is null ? Math.Round(MonitoringNoise.Of($"{project.Key}:sla-partial", seed) * 95) : 0;
            degraded = Math.Round(MonitoringNoise.Of($"{project.Key}:sla-degraded:{partKey}", seed) * 140);
        }
        else
        {
            var counting = project.Incidents.Where(incident => incident.Kind is ProjectIncidentKind.Incident && incident.DuringMaintenance is false).ToArray();
            down = Minutes(counting.Where(incident => incident.Impact is ProjectHealth.Down).Select(incident => (incident.StartedAt, incident.EndedAt)), watchedFrom, end, now);
            partial = partKey is null ? Minutes(counting.Where(incident => incident.Impact is not ProjectHealth.Down).Select(incident => (incident.StartedAt, incident.EndedAt)), watchedFrom, end, now) : 0;

            // Maintenance doesn't count, for a part either.
            var periods = project.Periods.Where(period => (partKey is null || period.CheckKey == partKey)
                                                          && windows.Any(window => window.StartsAt < (period.EndedAt ?? now) && window.EndsAt > period.StartedAt) is false).ToArray();
            degraded = Minutes(periods.Where(period => period.Status is HealthCheckStatus.Degraded).Select(period => (period.StartedAt, period.EndedAt)), watchedFrom, end, now);

            if (partKey is not null)
            {
                down += Minutes(periods.Where(period => period.Status is HealthCheckStatus.Unhealthy).Select(period => (period.StartedAt, period.EndedAt)), watchedFrom, end, now);
            }
        }

        return new(counted, down, partial, degraded, maintenance, noData, notWatched, windows, budgetBase);
    }

    /// <summary>Each occurrence of a window between two times, weekly ones every week, with the minutes of it that passed.</summary>
    private static IEnumerable<ProjectSlaMaintenanceDto> Occurrences(SampleProject project, DateTimeOffset from, DateTimeOffset to)
    {
        foreach (var window in project.Maintenance)
        {
            var length = window.EndsAt - window.StartsAt;

            for (var start = window.StartsAt; start < to; start = start.AddDays(7))
            {
                var minutes = (Min(start + length, to) - Max(start, from)).TotalMinutes;

                if (minutes > 0)
                {
                    yield return new() { Title = window.Title, StartsAt = start, EndsAt = start + length, RepeatsWeekly = window.RepeatsWeekly, Minutes = minutes };
                }

                if (window.RepeatsWeekly is false)
                    break;
            }
        }
    }
}
