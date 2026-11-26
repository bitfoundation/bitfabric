using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// Every minute, alerts the next step of every open incident nobody said they're on
/// (bitfabric-internal/specs/monitoring.md, Alerts): one alert per step rather than a flood, until someone presses
/// "I'm on it" or it's over.
/// </summary>
public partial class ProjectEscalationJobRunner
{
    public const string RecurringJobId = nameof(ProjectEscalationJobRunner);

    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private ProjectAlerter alerter = default!;
    [AutoInject] private TimeProvider timeProvider = default!;
    [AutoInject] private ILogger<ProjectEscalationJobRunner> logger = default!;

    [DisableConcurrentExecution(timeoutInSeconds: 50), AutomaticRetry(Attempts = 0)]
    public async Task Escalate(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var due = await dbContext.ProjectIncidents.IgnoreQueryFilters()
            .Include(incident => incident.Monitoring)
            .Where(incident => incident.EndedAt == null && incident.AcknowledgedAt == null && incident.DuringMaintenance == false
                               && incident.NextEscalationAt != null && incident.NextEscalationAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var incident in due)
        {
            try
            {
                var routes = await dbContext.ProjectAlertRoutes.IgnoreQueryFilters()
                    .Where(route => route.MonitoringId == incident.MonitoringId)
                    .ToListAsync(cancellationToken);

                if (ProjectAlerter.NextStep(routes, incident.EscalationStep) is not { } next)
                {
                    incident.NextEscalationAt = null;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    continue;
                }

                incident.EscalationStep = next.Step;
                await alerter.Notify(incident.Monitoring!, incident, ProjectAlertKind.Escalated, cancellationToken);
            }
            catch (Exception exp) when (exp is not OperationCanceledException)
            {
                logger.LogError(exp, "Escalating incident {IncidentId} failed.", incident.Id);
            }
        }
    }
}
