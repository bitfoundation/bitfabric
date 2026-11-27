using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// Opens, grows and closes a project's incidents from its rounds (bitfabric-internal/specs/monitoring.md, Checks): a part
/// that fails in two rounds in a row opens one, against a single bad answer, and one closes after every part of it passed
/// two rounds in a row. A part that only misses its settings is no incident, and an app bit Fabric can't read the report
/// of is down. While the token can't read the report, the public /health stands in: Unhealthy there fails the app itself,
/// and since it names no part, an incident of parts waits for it to say Healthy before it closes. One automatic incident
/// is open at a time; parts that fail while it lasts join it.
/// </summary>
public partial class ProjectIncidentTracker
{
    /// <summary>A deployment this long before an incident began is named as its likely cause.</summary>
    public static readonly TimeSpan DeployWindow = TimeSpan.FromMinutes(30);

    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private ProjectTexts texts = default!;

    /// <summary>What a round changed, for <see cref="ProjectAlerter"/> to tell once the round is saved.</summary>
    public sealed record Change(ProjectIncident Incident, ProjectAlertKind Kind);

    public async Task<Change?> Track(ProjectMonitoring project, ProjectProbe probe, CancellationToken cancellationToken)
    {
        if (probe.Outcome is ProjectProbeOutcome.Unauthorized)
            return null;

        var previous = await dbContext.ProjectProbes.IgnoreQueryFilters()
            .Where(p => p.MonitoringId == project.Id && p.Region == probe.Region && p.At < probe.At && p.Outcome != ProjectProbeOutcome.Unauthorized)
            .OrderByDescending(p => p.At)
            .FirstOrDefaultAsync(cancellationToken);

        var failingNow = Failing(probe);
        var failingBefore = previous is null ? [] : Failing(previous);

        // Failing in both rounds.
        var confirmed = failingNow.Keys.Intersect(failingBefore.Keys).ToArray();

        var open = await dbContext.ProjectIncidents.IgnoreQueryFilters()
            .FirstOrDefaultAsync(incident => incident.MonitoringId == project.Id && incident.EndedAt == null && incident.IsAutomatic, cancellationToken);

        if (open is null)
        {
            if (confirmed.Length is 0)
                return null;

            open = await Open(project, previous!, probe, confirmed, failingNow, cancellationToken);
            return open.DuringMaintenance ? null : new(open, ProjectAlertKind.Opened);
        }

        // A part joins the incident once it is confirmed; the impact only grows.
        var joining = confirmed.Except(open.CheckKeys).ToArray();
        var impact = ImpactOf(failingNow.Where(part => confirmed.Contains(part.Key)).Select(part => part.Value));

        if (joining.Length > 0)
        {
            open.CheckKeys = [.. open.CheckKeys, .. joining];
            var titles = TitlesOf(probe, previous, joining);
            open.Updates.Add(new()
            {
                At = probe.At,
                Phase = ProjectIncidentPhase.Investigating,
                Text = texts.InEveryLanguage(localizer => localizer[nameof(AppStrings.MonitoringIncidentPartsJoined), ProjectTexts.Parts(localizer, titles)])
            });
        }

        if (impact > open.Impact)
        {
            open.Impact = impact;
            open.Title = TitleOf(project, open.Impact, TitlesOf(probe, previous, open.CheckKeys));
        }
        else if (joining.Length > 0)
        {
            open.Title = TitleOf(project, open.Impact, TitlesOf(probe, previous, open.CheckKeys));
        }

        // Over once none of its parts failed in either round; a round that names no part says so only when all is Healthy.
        if (open.CheckKeys.Any(key => failingNow.ContainsKey(key) || failingBefore.ContainsKey(key)))
            return null;

        if (open.CheckKeys.Any(key => key != ProjectRollup.ReportKey) && (NamesNoPart(probe) || (previous is not null && NamesNoPart(previous))))
            return null;

        open.EndedAt = previous!.At;
        var lasted = open.EndedAt.Value - open.StartedAt;
        var parts = TitlesOf(probe, previous, open.CheckKeys);

        open.Updates.Add(new()
        {
            At = probe.At,
            Phase = ProjectIncidentPhase.Resolved,
            Text = texts.InEveryLanguage(localizer => localizer[nameof(AppStrings.MonitoringIncidentResolvedUpdate), ProjectTexts.Parts(localizer, parts), ProjectTexts.Duration(localizer, lasted)])
        });
        open.NextEscalationAt = null;

        return open.DuringMaintenance ? null : new(open, ProjectAlertKind.Resolved);
    }

    private async Task<ProjectIncident> Open(ProjectMonitoring project, ProjectProbe previous, ProjectProbe probe, string[] confirmed, Dictionary<string, HealthCheckStatus> failing, CancellationToken cancellationToken)
    {
        var startedAt = previous.At;
        var impact = ImpactOf(failing.Where(part => confirmed.Contains(part.Key)).Select(part => part.Value));
        var titles = TitlesOf(probe, previous, confirmed);

        var deployedAt = await dbContext.ProjectStarts.IgnoreQueryFilters()
            .Where(start => start.MonitoringId == project.Id && start.IsDeploy && start.At <= startedAt && start.At >= startedAt - DeployWindow)
            .OrderByDescending(start => start.At)
            .Select(start => (DateTimeOffset?)start.At)
            .FirstOrDefaultAsync(cancellationToken);

        var windows = await dbContext.ProjectMaintenanceWindows.IgnoreQueryFilters()
            .Where(window => window.MonitoringId == project.Id && window.StartsAt <= startedAt)
            .ToListAsync(cancellationToken);

        ProjectIncident incident = new()
        {
            Id = Guid.CreateSequentialGuid(),
            MonitoringId = project.Id,
            TenantId = project.TenantId,
            IsAutomatic = true,
            Impact = impact,
            StartedAt = startedAt,
            CheckKeys = confirmed,
            AfterDeployAt = deployedAt,
            DuringMaintenance = windows.Any(window => window.Covers(startedAt)),
            Title = TitleOf(project, impact, titles)
        };

        incident.Updates.Add(new()
        {
            At = probe.At,
            Phase = ProjectIncidentPhase.Investigating,
            Text = texts.InEveryLanguage(localizer => deployedAt is { } deployed
                ? localizer[nameof(AppStrings.MonitoringIncidentOpenedAfterDeploy), ProjectTexts.Parts(localizer, titles), ProjectTexts.Duration(localizer, startedAt - deployed)]
                : localizer[nameof(AppStrings.MonitoringIncidentOpened), ProjectTexts.Parts(localizer, titles)])
        });

        await dbContext.ProjectIncidents.AddAsync(incident, cancellationToken);

        return incident;
    }

    /// <summary>A round of the public /health that wasn't Healthy, or of the front door alone, can't tell whether a part still fails.</summary>
    private static bool NamesNoPart(ProjectProbe probe)
        => probe.Outcome is ProjectProbeOutcome.Reached || (probe.Outcome is ProjectProbeOutcome.PublicHealth && probe.Status is not HealthCheckStatus.Healthy);

    /// <summary>The app itself counts as a part when its report couldn't be read, or its public /health says Unhealthy.</summary>
    private static Dictionary<string, HealthCheckStatus> Failing(ProjectProbe probe)
    {
        if (probe.Outcome is ProjectProbeOutcome.Unreachable or ProjectProbeOutcome.BadResponse
            || (probe.Outcome is ProjectProbeOutcome.PublicHealth && probe.Status is HealthCheckStatus.Unhealthy))
            return new() { [ProjectRollup.ReportKey] = HealthCheckStatus.Unhealthy };

        return probe.Checks.Where(check => check.IsNotConfigured is false && check.Status is not HealthCheckStatus.Healthy)
                           .ToDictionary(check => check.Key!, check => check.Status);
    }

    /// <summary>Down when a part the app can't work without fails or the app doesn't answer; Degraded otherwise.</summary>
    private static ProjectHealth ImpactOf(IEnumerable<HealthCheckStatus> statuses)
        => statuses.Any(status => status is HealthCheckStatus.Unhealthy) ? ProjectHealth.Down : ProjectHealth.Degraded;

    /// <summary>What the app calls the parts, from the newest round that names them; "the app" for the report itself.</summary>
    private static string[] TitlesOf(ProjectProbe probe, ProjectProbe? previous, IEnumerable<string> keys)
        => [.. keys.Where(key => key != ProjectRollup.ReportKey)
                   .Select(key => probe.Checks.FirstOrDefault(check => check.Key == key)?.Title
                                  ?? previous?.Checks.FirstOrDefault(check => check.Key == key)?.Title
                                  ?? key)];

    private List<ProjectText> TitleOf(ProjectMonitoring project, ProjectHealth impact, string[] titles)
        => texts.InEveryLanguage(localizer => titles.Length is 0
            ? localizer[nameof(AppStrings.MonitoringIncidentTitleNotAnswering), project.Name!]
            : impact is ProjectHealth.Down
                ? localizer[nameof(AppStrings.MonitoringIncidentTitleStoppedWorking), ProjectTexts.Parts(localizer, titles)]
                : localizer[nameof(AppStrings.MonitoringIncidentTitleNotWorkingProperly), ProjectTexts.Parts(localizer, titles)]);
}
