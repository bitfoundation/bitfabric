using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Shared.Features.Monitoring;

/// <summary>
/// A project's history, incidents, maintenance windows, alerts and settings (bitfabric-internal/specs/monitoring.md), all
/// through the current tenant.
/// </summary>
[Route("api/v1/[controller]/[action]/"), AuthorizedApi]
public interface IProjectIncidentsController : IAppController
{
    /// <summary>The periods, incidents, deployments and maintenance overlapping a span, for History.</summary>
    [HttpPost]
    Task<ProjectHistoryDto> GetHistory(ProjectHistoryRequestDto request, CancellationToken cancellationToken);

    /// <summary>The open incidents and those of the last 90 days, newest first.</summary>
    [HttpGet("{monitoringId}")]
    Task<ProjectIncidentDto[]> GetIncidents(Guid monitoringId, CancellationToken cancellationToken);

    /// <summary>"I'm on it": stops the escalation and tells everyone already alerted who has it.</summary>
    [HttpPost("{id}")]
    Task<ProjectIncidentDto> Acknowledge(Guid id, CancellationToken cancellationToken);

    /// <summary>An update the team writes, such as what they found.</summary>
    [HttpPost]
    Task<ProjectIncidentDto> PostUpdate(ProjectIncidentUpdateRequestDto request, CancellationToken cancellationToken);

    [HttpGet("{monitoringId}")]
    Task<ProjectMaintenanceWindowDto[]> GetMaintenanceWindows(Guid monitoringId, CancellationToken cancellationToken);

    [HttpPost]
    Task<ProjectMaintenanceWindowDto> AddMaintenanceWindow(ProjectMaintenanceWindowDto window, CancellationToken cancellationToken);

    [HttpDelete("{id}/{version}")]
    Task DeleteMaintenanceWindow(Guid id, long version, CancellationToken cancellationToken);

    [HttpGet("{monitoringId}")]
    Task<ProjectAlertSettingsDto> GetAlertSettings(Guid monitoringId, CancellationToken cancellationToken);

    /// <summary>Replaces the project's routes with these.</summary>
    [HttpPost]
    Task<ProjectAlertSettingsDto> SaveAlertSettings(ProjectAlertSettingsDto settings, CancellationToken cancellationToken);

    /// <summary>Sends every route a test alert, so the team sees where alerts land.</summary>
    [HttpPost("{monitoringId}")]
    Task<ProjectAlertDto[]> SendTestAlert(Guid monitoringId, CancellationToken cancellationToken);

    [HttpGet("{monitoringId}")]
    Task<ProjectSettingsDto> GetSettings(Guid monitoringId, CancellationToken cancellationToken);

    [HttpPost]
    Task<ProjectSettingsDto> SaveSettings(ProjectSettingsDto settings, CancellationToken cancellationToken);
}
