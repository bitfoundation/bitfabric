using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

public static partial class MonitoringReads
{
    /// <summary>
    /// A sample project's application map, as the server builds it (ProjectsService.GetMap): its report's parts, each with
    /// its last 24 hours from the sample's recent rounds, which stand in for the rollups, and the ongoing incident about it.
    /// </summary>
    public static ProjectMapDto Map(SampleProject project, DateTimeOffset now)
    {
        var ongoing = project.Incidents.Where(incident => incident.EndedAt is null).OrderBy(incident => incident.StartedAt).ToArray();

        return new()
        {
            CheckedAt = project.CheckedAt,
            HomePageMs = project.CheckedAt is null ? null : project.HomePageMs,
            Parts = [.. project.Checks.Select(check =>
            {
                var timed = check.Recent.Where(round => round.DurationMs is not null).Select(round => round.DurationMs!.Value).ToArray();
                // A round a minute over the day; a part failing now has failed every minute since it began, one that works had
                // the troubles its recent strip shows.
                var rounds = check.IsNotConfigured ? 0 : 1440;
                var problems = check.IsNotConfigured ? 0
                    : check.Status is not Bit.Fabric.Shared.Features.Diagnostic.HealthCheckStatus.Healthy && check.Since is { } since
                        ? Math.Clamp((int)(now - since).TotalMinutes, 1, rounds)
                        : check.Recent.Count(round => round.Status is not Bit.Fabric.Shared.Features.Diagnostic.HealthCheckStatus.Healthy);

                return new ProjectMapPartDto
                {
                    Key = check.Key,
                    Title = check.Title,
                    Category = check.Category,
                    IconName = check.IconName,
                    About = check.About,
                    Status = check.Status,
                    IsNotConfigured = check.IsNotConfigured,
                    Since = check.Since,
                    CheckedAt = check.VerifiedAt ?? project.CheckedAt,
                    LastMs = timed.Length is 0 ? check.DurationMs : timed[^1],
                    TypicalMs = timed.Length is 0 ? (check.IsNotConfigured ? null : check.DurationMs) : (int)Math.Round(timed.Average()),
                    // The day's slowest round is slower than the last half hour's, as a day has its busy minutes.
                    SlowestMs = timed.Length is 0 ? (check.IsNotConfigured ? null : check.DurationMs) : (int)Math.Round(timed.Max() * 1.8),
                    Rounds = rounds,
                    ProblemRounds = problems,
                    OngoingIncidentId = ongoing.FirstOrDefault(incident => incident.CheckKeys.Contains(check.Key))?.Id
                };
            })]
        };
    }
}
