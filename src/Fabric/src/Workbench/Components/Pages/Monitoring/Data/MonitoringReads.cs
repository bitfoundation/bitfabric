using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// A sample project as the app's DTOs, counted the way bit Fabric's server counts them: the card's days from the same code
/// History uses, the health from the open incidents and the report, a part's day as no data outside the time the app
/// reported it. Pure functions of the project and the time, so the stand-in controllers and the export endpoints agree.
/// </summary>
public static partial class MonitoringReads
{
    public static TimeZoneInfo ZoneOf(string? id)
        => id is not null && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : TimeZoneInfo.Utc;

    public static ProjectHealth HealthOf(SampleProject project)
    {
        if (project.ConnectionStatus is not ProjectConnectionStatus.Connected || project.CheckedAt is null)
            return ProjectHealth.NoData;

        if (OpenIncidents(project).Any(incident => incident.Impact is ProjectHealth.Down))
            return ProjectHealth.Down;

        return project.Checks.Any(check => check.IsNotConfigured is false && check.Status is not HealthCheckStatus.Healthy)
            ? ProjectHealth.Degraded
            : ProjectHealth.Operational;
    }

    public static IEnumerable<SampleIncident> OpenIncidents(SampleProject project)
        => project.Incidents.Where(incident => incident.Kind is ProjectIncidentKind.Incident && incident.EndedAt is null).OrderByDescending(incident => incident.StartedAt);

    public static ProjectDto Project(SampleProject project, TimeZoneInfo zone, DateTimeOffset now)
    {
        var days = ProjectHistoryDays.Last(30, zone, now);
        var appDays = ProjectHistoryDays.OfApp(History(project, days[0].Start, now, now), days, now);
        var ongoing = OpenIncidents(project).FirstOrDefault();
        var isRead = project.ConnectionStatus is not ProjectConnectionStatus.Disconnected;
        var hourly = Hourly(project, now);

        return new()
        {
            Id = project.Id,
            Name = project.Name,
            Address = project.Address,
            ServerAddress = project.ServerAddress,
            TemplateVersion = project.TemplateVersion,
            IconId = project.IconId,
            Health = HealthOf(project),
            ConnectionStatus = project.ConnectionStatus,
            ConnectionProblem = ProjectConnectionProblems.Describe(project.ConnectionProblem),
            LastCheckedAt = project.CheckedAt,
            CreatedOn = project.CreatedOn,
            Uptime30Days = project.CheckedAt is null ? null : ProjectHistoryDays.UptimeOf(appDays),
            MedianMs = isRead && hourly.Any(ms => ms > 0) ? Median(hourly.Where(ms => ms > 0)) : null,
            P95Ms = isRead && hourly.Any(ms => ms > 0) ? (int)Math.Round(hourly.Where(ms => ms > 0).Max() * 1.35) : null,
            ResponseLast24Hours = hourly,
            Last30Days = [.. appDays.Select(day => new ProjectDayDto { Date = day.Date, Tone = day.Tone, Uptime = day.Uptime, DownMinutes = day.DownMinutes, DegradedMinutes = day.DegradedMinutes })],
            FailingChecks = project.ConnectionStatus is ProjectConnectionStatus.Connected
                ? [.. project.Checks.Where(check => check.IsNotConfigured is false && check.Status is not HealthCheckStatus.Healthy).Select(check => check.Title ?? check.Key!)]
                : [],
            OngoingIncidentId = ongoing?.Id,
            OngoingIncident = ongoing is null ? null : MonitoringTexts.InCurrent(ongoing.Title),
            OngoingIncidentSince = ongoing?.StartedAt,
            HasCacheWarning = project.HasCacheWarning,
            Locations = project.Locations,
            Scans = ScanSummaries(project),
            Version = project.Version
        };
    }

    /// <summary>The last 24 hours' hourly medians of the home page, oldest first: 0 for an hour nothing was read in.</summary>
    private static int[] Hourly(SampleProject project, DateTimeOffset now)
    {
        var downs = project.Incidents.Where(incident => incident.Kind is ProjectIncidentKind.Incident && incident.Impact is ProjectHealth.Down).ToArray();

        return [.. Enumerable.Range(0, 24).Select(index =>
        {
            var from = now.AddHours(index - 24);
            var to = from.AddHours(1);

            if (to <= project.CreatedOn || (project.ConnectionStatus is ProjectConnectionStatus.Disconnected && from >= project.CheckedAt))
                return 0;

            var hourOfDay = from.UtcDateTime.Hour;
            var busy = 1 + 0.25 * Math.Max(0, Math.Sin((hourOfDay - 8) / 24.0 * 2 * Math.PI));
            var ms = project.BaseMs * busy * (0.85 + 0.3 * MonitoringNoise.Of($"{project.Key}:hour", from.ToUnixTimeSeconds() / 3600));

            if (downs.Any(incident => incident.StartedAt < to && (incident.EndedAt ?? now) > from))
            {
                ms *= 3.2;
            }

            return (int)Math.Round(ms);
        })];
    }

    private static int Median(IEnumerable<int> values)
    {
        var sorted = values.Order().ToArray();
        return sorted[sorted.Length / 2];
    }

    public static ProjectDetailsDto Details(SampleProject project, TimeZoneInfo zone, DateTimeOffset now)
    {
        var configured = project.Checks.Where(check => check.IsNotConfigured is false).ToArray();

        return new()
        {
            Project = Project(project, zone, now),
            Checks = [.. project.Checks],
            CheckedAt = project.CheckedAt,
            ReportMs = project.CheckedAt is null || configured.Length is 0 ? null : configured.Max(check => check.DurationMs) + 38,
            HomePageMs = project.CheckedAt is null ? null : project.HomePageMs,
            ApplicationVersion = project.ApplicationVersion,
            LastDeployedAt = project.Starts.Where(start => start.IsDeploy).Select(start => (DateTimeOffset?)start.At).Max(),
            LastRestartedAt = project.Starts.Where(start => start.IsDeploy is false).Select(start => (DateTimeOffset?)start.At).Max(),
            Regions = ["Germany", .. NetworkLocations.All.Where(location => project.Locations.Contains(location.Code)).Select(location => location.Name)],
            NetworkReadings = project.ConnectionStatus is ProjectConnectionStatus.Disconnected
                ? []
                : [.. project.Readings.Where(reading => project.Locations.Contains(reading.Location)).Take(project.ReadingsIn ?? int.MaxValue)]
        };
    }

    /// <summary>The newest scan of each kind, as the card's scans row shows it.</summary>
    public static ProjectScanSummaryDto[] ScanSummaries(SampleProject project)
        => [.. ScanFindings.AvailableKinds.Select(kind =>
        {
            var latest = project.ScanRuns.FirstOrDefault(run => run.Kind == kind);

            return new ProjectScanSummaryDto
            {
                ScanId = latest?.Id,
                Kind = kind,
                IsRunning = project.PendingScans.Any(scan => scan.Kind == kind),
                FinishedAt = latest?.FinishedAt,
                Outcome = latest?.Outcome,
                Verdict = latest?.Verdict,
                Score = latest?.Score,
                Grade = latest?.Grade,
                ExpiresAt = latest?.ExpiresAt,
                FindingCount = latest?.Findings.Length ?? 0,
                TopFinding = latest?.Findings.FirstOrDefault()
            };
        })];

    public static ProjectIncidentDto Incident(SampleProject project, SampleIncident incident) => new()
    {
        Id = incident.Id,
        ProjectId = project.Id,
        Kind = incident.Kind,
        Impact = incident.Impact,
        Title = MonitoringTexts.InCurrent(incident.Title),
        StartedAt = incident.StartedAt,
        EndedAt = incident.EndedAt,
        CheckKeys = incident.CheckKeys,
        CheckTitles = [.. incident.CheckKeys.Select(project.TitleOf)],
        IsAutomatic = incident.IsAutomatic,
        AfterDeployAt = incident.AfterDeployAt,
        DuringMaintenance = incident.DuringMaintenance,
        AcknowledgedBy = incident.AcknowledgedBy,
        AcknowledgedAt = incident.AcknowledgedAt,
        Updates = [.. incident.Updates.Select(update => new ProjectIncidentUpdateDto { At = update.At, Phase = update.Phase, Text = MonitoringTexts.InCurrent(update.Text), Author = update.Author })],
        Alerts = [.. incident.Alerts],
        AgentPrompt = incident.AgentPrompt,
        Version = incident.Version
    };

    /// <summary>The open incidents and those of the last 90 days, newest first; the status page's announcements aren't incidents.</summary>
    public static ProjectIncidentDto[] Incidents(SampleProject project, DateTimeOffset now)
        => [.. project.Incidents.Where(incident => incident.Kind is ProjectIncidentKind.Incident && (incident.EndedAt is null || incident.EndedAt > now.AddDays(-90)))
                                .OrderBy(incident => incident.EndedAt is null ? 0 : 1)
                                .ThenByDescending(incident => incident.StartedAt)
                                .Select(incident => Incident(project, incident))];

    /// <summary>The periods, incidents, deployments and maintenance overlapping a span, and the parts History draws.</summary>
    public static ProjectHistoryDto History(SampleProject project, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now)
    {
        bool Overlaps(DateTimeOffset start, DateTimeOffset? end) => start < to && (end ?? now) > from;

        var periods = project.Periods.Where(period => Overlaps(period.StartedAt, period.EndedAt)).ToArray();
        var reported = project.Checks.Where(check => check.IsNotConfigured is false).Select(check => check.Key!).ToHashSet();

        return new()
        {
            Periods = periods,
            Incidents = [.. project.Incidents.Where(incident => incident.Kind is ProjectIncidentKind.Incident && Overlaps(incident.StartedAt, incident.EndedAt)).Select(incident => Incident(project, incident))],
            Starts = [.. project.Starts.Where(start => start.At >= from && start.At < to)],
            Maintenance = [.. project.Maintenance.Where(window => window.StartsAt < to && (window.RepeatsWeekly || window.EndsAt > from))],
            WatchedSince = project.CreatedOn,
            Parts =
            [
                .. project.Checks.Where(check => check.IsNotConfigured is false).Select(check => new ProjectHistoryPartDto
                {
                    Key = check.Key,
                    Title = check.Title,
                    ReportedFrom = project.PartSpans.TryGetValue(check.Key!, out var span) && span.From > from ? span.From : null
                }),
                // A part the latest report no longer has, while it wasn't healthy within the span.
                .. periods.Select(period => period.CheckKey!).Distinct().Where(key => reported.Contains(key) is false).Select(key => new ProjectHistoryPartDto
                {
                    Key = key,
                    Title = MonitoringParts.TitleOf(key),
                    ReportedFrom = project.PartSpans.TryGetValue(key, out var span) && span.From > from ? span.From : null,
                    ReportedTo = project.PartSpans.TryGetValue(key, out var removed) ? removed.To : null
                })
            ]
        };
    }

    public static ProjectAlertSettingsDto AlertSettings(SampleProject project) => new()
    {
        ProjectId = project.Id,
        Routes = [.. project.Routes],
        Members = [.. MonitoringSamples.Team.Select(member => new ProjectTeamMemberDto { UserId = member.UserId, Name = member.Name, HasEmail = member.Email is not null, HasPhoneNumber = member.PhoneNumber is not null })]
    };

    public static ProjectSettingsDto Settings(SampleProject project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        TimeZoneId = project.TimeZoneId,
        ApprovedBy = project.ConnectionStatus is ProjectConnectionStatus.Disconnected ? null : project.ApprovedBy,
        ApprovedOn = project.ConnectionStatus is ProjectConnectionStatus.Disconnected ? null : project.ApprovedOn,
        Reads = $"{project.ServerAddress ?? project.Address}/healthz",
        Version = project.Version
    };
}
