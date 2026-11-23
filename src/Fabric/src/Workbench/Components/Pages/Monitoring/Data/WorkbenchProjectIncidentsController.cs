using System.Globalization;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// The stand-in for bit Fabric's <see cref="IProjectIncidentsController"/>: a sample project's history, incidents with
/// "I'm on it" and the team's updates, maintenance windows, alert routes with a test alert, and its settings, all changed
/// in the view's own copy of the sample world.
/// </summary>
public sealed class WorkbenchProjectIncidentsController(WorkbenchScenarioState scenarioState, MonitoringSampleWorld world, AuthenticationStateProvider authenticationStateProvider)
    : MonitoringStandIn(scenarioState, world, authenticationStateProvider), IProjectIncidentsController
{
    public Task<ProjectHistoryDto> GetHistory(ProjectHistoryRequestDto request, CancellationToken cancellationToken)
    {
        return Load("history", () => MonitoringReads.History(World.Get(request.ProjectId), request.From, request.To, World.Now), () =>
        {
            // Nothing happened yet: a project watched for the last hour, with the parts its report has.
            var history = MonitoringReads.History(World.Get(request.ProjectId), request.From, request.To, World.Now);
            return new ProjectHistoryDto { WatchedSince = World.Now.AddHours(-1), Parts = history.Parts };
        }, cancellationToken);
    }

    public Task<ProjectIncidentDto[]> GetIncidents(Guid projectId, CancellationToken cancellationToken)
        => Load<ProjectIncidentDto[]>("incidents", () => MonitoringReads.Incidents(World.Get(projectId), World.Now), () => [], cancellationToken);

    public async Task<ProjectIncidentDto> Acknowledge(Guid id, CancellationToken cancellationToken)
    {
        var name = await CurrentUserName();

        return await Save(() =>
        {
            var incident = World.GetIncident(id, out var project);
            incident.AcknowledgedBy = name;
            incident.AcknowledgedAt = World.Now;
            incident.Version++;

            return MonitoringReads.Incident(project, incident);
        }, cancellationToken);
    }

    public async Task<ProjectIncidentDto> PostUpdate(ProjectIncidentUpdateRequestDto request, CancellationToken cancellationToken)
    {
        var name = await CurrentUserName();

        return await Save(() =>
        {
            var incident = World.GetIncident(request.IncidentId, out var project);
            incident.Updates.Insert(0, new() { At = World.Now, Phase = request.Phase, Author = name, Text = new() { [CultureInfo.CurrentUICulture.Name] = request.Text ?? "" } });
            incident.Version++;

            return MonitoringReads.Incident(project, incident);
        }, cancellationToken);
    }

    public Task<ProjectMaintenanceWindowDto[]> GetMaintenanceWindows(Guid projectId, CancellationToken cancellationToken)
        => Load<ProjectMaintenanceWindowDto[]>("maintenance", () => [.. World.Get(projectId).Maintenance.OrderBy(window => window.StartsAt)], () => [], cancellationToken);

    public Task<ProjectMaintenanceWindowDto> AddMaintenanceWindow(ProjectMaintenanceWindowDto window, CancellationToken cancellationToken) => Save(() =>
    {
        var project = World.Get(window.ProjectId);

        if (window.EndsAt <= window.StartsAt || (window.RepeatsWeekly && window.EndsAt - window.StartsAt >= TimeSpan.FromDays(7)))
            throw new BadRequestException();

        var added = new ProjectMaintenanceWindowDto
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = window.Title,
            StartsAt = window.StartsAt,
            EndsAt = window.EndsAt,
            RepeatsWeekly = window.RepeatsWeekly,
            Version = 1
        };

        project.Maintenance.Add(added);
        return added;
    }, cancellationToken);

    public Task DeleteMaintenanceWindow(Guid id, long version, CancellationToken cancellationToken)
        => Save(() => World.Projects.ForEach(project => project.Maintenance.RemoveAll(window => window.Id == id)), cancellationToken);

    public Task<ProjectAlertSettingsDto> GetAlertSettings(Guid projectId, CancellationToken cancellationToken)
        => Load("alerts", () => MonitoringReads.AlertSettings(World.Get(projectId)),
            () => new ProjectAlertSettingsDto { ProjectId = projectId, Members = MonitoringReads.AlertSettings(World.Get(projectId)).Members }, cancellationToken);

    public Task<ProjectAlertSettingsDto> SaveAlertSettings(ProjectAlertSettingsDto settings, CancellationToken cancellationToken) => Save(() =>
    {
        var project = World.Get(settings.ProjectId);

        project.Routes.Clear();
        project.Routes.AddRange(settings.Routes.Select(route =>
        {
            route.Id = route.Id == Guid.Empty ? Guid.NewGuid() : route.Id;
            route.UserName = MonitoringSamples.Team.FirstOrDefault(member => member.UserId == route.UserId)?.Name;
            return route;
        }));

        return MonitoringReads.AlertSettings(project);
    }, cancellationToken);

    public Task<ProjectAlertDto[]> SendTestAlert(Guid projectId, CancellationToken cancellationToken) => Save<ProjectAlertDto[]>(() =>
    {
        var project = World.Get(projectId);
        var now = World.Now;

        return [.. project.Routes.OrderBy(route => route.Step).Select(route => MonitoringSamples.Alert(route, ProjectAlertKind.Test, now))];
    }, cancellationToken);

    public Task<ProjectSettingsDto> GetSettings(Guid projectId, CancellationToken cancellationToken)
        => Load("settings", () => MonitoringReads.Settings(World.Get(projectId)), () => MonitoringReads.Settings(World.Get(projectId)), cancellationToken);

    public Task<ProjectSettingsDto> SaveSettings(ProjectSettingsDto settings, CancellationToken cancellationToken) => Save(() =>
    {
        var project = World.Get(settings.Id);
        project.Name = settings.Name?.Trim() ?? project.Name;
        project.TimeZoneId = settings.TimeZoneId ?? project.TimeZoneId;
        project.Version++;

        return MonitoringReads.Settings(project);
    }, cancellationToken);
}
