using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

public partial class MonitoringsService
{
    /// <summary>
    /// The project's application map (bitfabric-internal/specs/monitoring.md, Application map), for its tab and the AI tools
    /// alike: the parts its last report names, as the Overview reads them, each with its last 24 hours from the rollups and
    /// the ongoing incident about it. Nothing is guessed: a part is what the app's own report calls it.
    /// </summary>
    public async Task<ProjectMapDto> GetMap(Guid id, CancellationToken cancellationToken)
    {
        var details = await GetMonitoring(id, cancellationToken);

        var since24Hours = timeProvider.GetUtcNow().AddHours(-24);
        string[] keys = [.. details.Checks.Select(check => check.Key!).Where(key => key is not null)];

        var day = (await dbContext.ProjectRollups.AsNoTracking()
            .Where(rollup => rollup.MonitoringId == id && rollup.BucketStart >= since24Hours && keys.Contains(rollup.CheckKey!))
            .GroupBy(rollup => rollup.CheckKey!)
            .Select(group => new
            {
                Key = group.Key,
                Rounds = group.Sum(rollup => rollup.Count),
                Problems = group.Sum(rollup => rollup.FailedCount + rollup.DegradedCount),
                Timed = group.Sum(rollup => rollup.TimedCount),
                TotalMs = group.Sum(rollup => rollup.TotalMs),
                SlowestMs = group.Max(rollup => rollup.MaxMs)
            })
            .ToListAsync(cancellationToken))
            .ToDictionary(entry => entry.Key);

        // An incident names the parts it is about; a part in two at once points to the older, as the page header does.
        var ongoing = await dbContext.ProjectIncidents.AsNoTracking()
            .Where(incident => incident.MonitoringId == id && incident.EndedAt == null)
            .OrderBy(incident => incident.StartedAt)
            .Select(incident => new { incident.Id, incident.CheckKeys })
            .ToListAsync(cancellationToken);

        return new()
        {
            CheckedAt = details.CheckedAt,
            HomePageMs = details.HomePageMs,
            Parts = [.. details.Checks.Select(check =>
            {
                day.TryGetValue(check.Key ?? "", out var stats);
                var lastRun = check.Recent.LastOrDefault(round => round.DurationMs is not null);

                return new ProjectMapPartDto
                {
                    Key = check.Key,
                    Title = string.IsNullOrWhiteSpace(check.Title) ? check.Key : check.Title,
                    Category = check.Category,
                    IconName = check.IconName,
                    About = check.About,
                    Provider = check.Provider,
                    Host = check.Host,
                    Model = check.Model,
                    Status = check.Status,
                    IsNotConfigured = check.IsNotConfigured,
                    Since = check.Since,
                    CheckedAt = check.VerifiedAt ?? details.CheckedAt,
                    LastMs = lastRun?.DurationMs ?? check.DurationMs,
                    TypicalMs = stats is { Timed: > 0 } ? (int)Math.Round(stats.TotalMs / (double)stats.Timed) : null,
                    SlowestMs = stats is { Timed: > 0 } ? stats.SlowestMs : null,
                    Rounds = stats?.Rounds ?? 0,
                    ProblemRounds = stats?.Problems ?? 0,
                    OngoingIncidentId = ongoing.FirstOrDefault(incident => incident.CheckKeys.Contains(check.Key))?.Id
                };
            })]
        };
    }
}
