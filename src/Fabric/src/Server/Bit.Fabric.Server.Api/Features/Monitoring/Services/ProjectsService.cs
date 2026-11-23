using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// What the projects page and a project's Overview read (bitfabric-internal/specs/monitoring.md), for
/// <see cref="ProjectsController"/> and the AI tools (<see cref="ProjectMonitoringMcpTools"/>) alike, so a card says the
/// same in the app as to an AI. Every query goes through the tenant filter: another team's project is simply not found.
/// </summary>
public partial class ProjectsService(AppDbContext dbContext,
    TimeProvider timeProvider,
    ServerApiSettings settings,
    IEnumerable<IProjectScanner> scanners,
    IStringLocalizer<AppStrings> localizer)
{
    /// <summary>How many of a part's last results its card shows.</summary>
    private const int RecentRounds = 30;

    /// <summary>How many days a card's bars and uptime cover, today included.</summary>
    public const int CardDays = 30;

    /// <summary>
    /// Every project of the tenant as its card shows it, failing ones first, its last 30 days drawn in
    /// <paramref name="zone"/>, the viewer's.
    /// </summary>
    public async Task<List<ProjectDto>> GetProjects(TimeZoneInfo zone, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var since24Hours = now.AddHours(-24);
        var recently = now.AddMinutes(-10);

        var projects = await dbContext.Projects.AsNoTracking().ToListAsync(cancellationToken);
        Guid[] projectIds = [.. projects.Select(project => project.Id)];

        // The 30 days from the report's rollups, summed up a day at a time by the database: the same few thousand rows a
        // project the card's uptime always read rather than 43,200 rounds, and 30 rows a project back. The days' midnights
        // come from the zone, so a clock change is a 23 or 25 hour day; every zone's offset is a multiple of 15 minutes, so
        // each rollup falls in one day whole.
        var days = ProjectHistoryDays.Last(CardDays, zone, now);
        DateTimeOffset[] midnights = [.. days.Select(day => day.Start), days[^1].End];
        var since = days[0].Start;

        var rounds = (await dbContext.Database.SqlQuery<CardDayRow>($"""
            SELECT "ProjectId", width_bucket("BucketStart", {midnights}) AS "Day",
                   SUM("Count") AS "Count", SUM("FailedCount") AS "Failed", SUM("DegradedCount") AS "Degraded"
            FROM "ProjectRollups"
            WHERE "ProjectId" = ANY({projectIds}) AND "CheckKey" = {ProjectRollup.ReportKey} AND "BucketStart" >= {since}
            GROUP BY 1, 2
            """).ToListAsync(cancellationToken))
            .ToLookup(row => row.ProjectId);

        var cacheWarnings = await dbContext.ProjectProbes
            .Where(probe => projectIds.Contains(probe.ProjectId) && probe.At >= recently && probe.HasCacheWarning)
            .Select(probe => probe.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // The percentiles need every round of the day; the project ids keep it on the (ProjectId, At) index.
        var responseTimes = (await dbContext.ProjectProbes
            .Where(probe => projectIds.Contains(probe.ProjectId) && probe.At >= since24Hours && probe.ResponseMs != null)
            .Select(probe => new { probe.ProjectId, probe.At, Ms = probe.ResponseMs!.Value })
            .ToListAsync(cancellationToken))
            .ToLookup(row => row.ProjectId);

        var failing = (await dbContext.ProjectCheckPeriods
            .Where(period => projectIds.Contains(period.ProjectId) && period.EndedAt == null && period.Status != HealthCheckStatus.Healthy)
            .Select(period => new { period.ProjectId, Title = period.Title ?? period.CheckKey })
            .ToListAsync(cancellationToken))
            .ToLookup(row => row.ProjectId, row => row.Title!);

        var ongoing = (await dbContext.ProjectIncidents
            .Where(incident => projectIds.Contains(incident.ProjectId) && incident.EndedAt == null)
            .ToListAsync(cancellationToken))
            .ToLookup(incident => incident.ProjectId);

        ProjectScanKind[] kindsRun = [.. scanners.Where(scanner => scanner.IsAvailable).Select(scanner => scanner.Kind)];
        var scans = await ProjectScanReader.SummariesOf(dbContext, projectIds, kindsRun, now, cancellationToken);

        return [.. projects.Select(project =>
        {
            var dto = project.Map();

            var projectDays = rounds[project.Id].Where(row => row.Day is >= 1 and <= CardDays).ToDictionary(row => row.Day);

            dto.Last30Days = [.. days.Select((day, index) => projectDays.TryGetValue(index + 1, out var row) && row.Count > 0
                ? new ProjectDayDto
                {
                    Date = day.Date,
                    Tone = row.Failed > 0 ? ProjectDayTone.Down : row.Degraded > 0 ? ProjectDayTone.Degraded : ProjectDayTone.Ok,
                    Uptime = 100.0 * (row.Count - row.Failed) / row.Count,
                    // A round a minute: a round found it down, a minute it was.
                    DownMinutes = (int)row.Failed,
                    DegradedMinutes = (int)row.Degraded
                }
                : new ProjectDayDto { Date = day.Date, Tone = ProjectDayTone.NoData })];

            var total = projectDays.Values.Sum(row => row.Count);
            if (total > 0)
            {
                dto.Uptime30Days = 100.0 * (total - projectDays.Values.Sum(row => row.Failed)) / total;
            }

            dto.HasCacheWarning = cacheWarnings.Contains(project.Id);

            var times = responseTimes[project.Id].Select(row => row.Ms).Order().ToArray();
            if (times.Length > 0)
            {
                dto.MedianMs = times[times.Length / 2];
                dto.P95Ms = times[Math.Min(times.Length - 1, (int)(times.Length * 0.95))];
            }

            dto.ResponseLast24Hours = [.. Enumerable.Range(0, 24).Select(hour =>
            {
                var from = since24Hours.AddHours(hour);
                var inHour = responseTimes[project.Id].Where(row => row.At >= from && row.At < from.AddHours(1)).Select(row => row.Ms).Order().ToArray();
                return inHour.Length is 0 ? 0 : inHour[inHour.Length / 2];
            })];

            dto.FailingChecks = [.. failing[project.Id].Order()];

            dto.Scans = scans.TryGetValue(project.Id, out var projectScans) ? projectScans : [];

            if (ongoing[project.Id].OrderBy(incident => incident.StartedAt).FirstOrDefault() is { } incident)
            {
                dto.OngoingIncidentId = incident.Id;
                dto.OngoingIncident = ProjectTexts.In(incident.Title, CultureInfo.CurrentUICulture.Name);
                dto.OngoingIncidentSince = incident.StartedAt;
            }

            return dto;
        })
        .OrderBy(AttentionRank)
        .ThenBy(dto => dto.Name)];
    }

    /// <summary>A project's day of report rollups, the <c>Day</c>th of the card's 30 (See GetProjects).</summary>
    internal sealed class CardDayRow
    {
        public Guid ProjectId { get; set; }

        public int Day { get; set; }

        public long Count { get; set; }

        public long Failed { get; set; }

        public long Degraded { get; set; }
    }

    /// <summary>Failing first, the way the projects page orders its cards.</summary>
    private static int AttentionRank(ProjectDto project) => (project.Health, project.ConnectionStatus) switch
    {
        (ProjectHealth.Down, _) => 0,
        (_, ProjectConnectionStatus.ReconnectNeeded) => 1,
        (ProjectHealth.Degraded, _) => 2,
        (_, ProjectConnectionStatus.FirstChecks) => 3,
        _ => 4
    };

    /// <summary>A project's Overview: the project and its last report, entry by entry, its builds and where it's checked from.</summary>
    public async Task<ProjectDetailsDto> GetProject(Guid id, CancellationToken cancellationToken)
    {
        var project = await FindProject(id, cancellationToken);

        // The last rounds that read a report: the newest is what the page shows, all of them each part's strip of recent results.
        var recentRounds = await dbContext.ProjectProbes.AsNoTracking()
            .Where(probe => probe.ProjectId == id && probe.Outcome == ProjectProbeOutcome.Read)
            .OrderByDescending(probe => probe.At)
            .Take(RecentRounds)
            .ToListAsync(cancellationToken);

        var lastReport = recentRounds.FirstOrDefault();

        var since = await dbContext.ProjectCheckPeriods
            .Where(period => period.ProjectId == id && period.EndedAt == null)
            .ToDictionaryAsync(period => period.CheckKey!, period => period.StartedAt, cancellationToken);

        // The newest deployment and the newest restart (See ProjectStart.IsDeploy).
        var lastStarts = await dbContext.ProjectStarts
            .Where(start => start.ProjectId == id)
            .GroupBy(start => start.IsDeploy)
            .Select(group => new { IsDeploy = group.Key, At = group.Max(start => start.At) })
            .ToListAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();

        var regions = (await dbContext.ProjectProbes
            .Where(probe => probe.ProjectId == id && probe.At >= now.AddMinutes(-10))
            .Select(probe => probe.Region!)
            .Distinct()
            .ToArrayAsync(cancellationToken))
            .OrderBy(RegionOrder)
            .ToArray();

        if (regions.Length is 0)
        {
            regions = [settings.Monitoring.Region]; // Before its first round, the project is checked from here.
        }

        var dto = project.Map();

        if (await dbContext.ProjectIncidents.AsNoTracking().Where(incident => incident.ProjectId == id && incident.EndedAt == null)
                                           .OrderBy(incident => incident.StartedAt).FirstOrDefaultAsync(cancellationToken) is { } ongoing)
        {
            dto.OngoingIncidentId = ongoing.Id;
            dto.OngoingIncident = ProjectTexts.In(ongoing.Title, CultureInfo.CurrentUICulture.Name);
            dto.OngoingIncidentSince = ongoing.StartedAt;
        }

        // Each location's newest reading of the last hour, with the data center it really ran in.
        var networkReadings = (await dbContext.ProjectProbes
            .Where(probe => probe.ProjectId == id && probe.Colo != null && probe.At >= now.AddHours(-1))
            .Select(probe => new { probe.Region, probe.Colo, probe.At, probe.ResponseMs })
            .ToListAsync(cancellationToken))
            .GroupBy(reading => reading.Region)
            .Select(location => location.MaxBy(reading => reading.At)!)
            .Where(reading => NetworkLocations.FindByName(reading.Region) is not null)
            .OrderBy(reading => RegionOrder(reading.Region!))
            .Select(reading => new ProjectNetworkReadingDto
            {
                Location = NetworkLocations.FindByName(reading.Region)!.Code,
                Colo = reading.Colo,
                At = reading.At,
                ResponseMs = reading.ResponseMs
            })
            .ToArray();

        return new()
        {
            Project = dto,
            Checks = [.. (lastReport?.Checks ?? []).Select(check =>
            {
                var checkDto = check.Map();
                checkDto.Since = since.TryGetValue(check.Key!, out var startedAt) ? startedAt : null;
                checkDto.Recent = [.. recentRounds.AsEnumerable().Reverse()
                    .Select(round => (round.At, Check: round.Checks.FirstOrDefault(c => c.Key == check.Key)))
                    .Where(round => round.Check is not null)
                    .Select(round => new ProjectCheckRoundDto { At = round.At, Status = round.Check!.Status, DurationMs = round.Check.IsReused ? null : round.Check.DurationMs })];
                return checkDto;
            })],
            CheckedAt = lastReport?.At,
            ReportMs = lastReport?.ReportMs,
            HomePageMs = lastReport?.ResponseMs,
            ApplicationVersion = lastReport?.ApplicationVersion,
            LastDeployedAt = lastStarts.FirstOrDefault(start => start.IsDeploy)?.At,
            LastRestartedAt = lastStarts.FirstOrDefault(start => start.IsDeploy is false)?.At,
            Regions = regions,
            NetworkReadings = networkReadings
        };
    }

    /// <summary>A project of the tenant, untracked; another team's is not found.</summary>
    public async Task<Project> FindProject(Guid id, CancellationToken cancellationToken)
        => await dbContext.Projects.AsNoTracking().FirstOrDefaultAsync(project => project.Id == id, cancellationToken)
           ?? throw new ResourceNotFoundException(localizer[nameof(AppStrings.ResourceNotFoundException)]);

    /// <summary>bit Fabric's own server first, then the bitplatform Network's locations in the order NetworkLocations lists them.</summary>
    private int RegionOrder(string region) => region == settings.Monitoring.Region
        ? -1
        : Array.FindIndex(NetworkLocations.All, location => location.Name == region) is var index and >= 0 ? index : int.MaxValue;
}
