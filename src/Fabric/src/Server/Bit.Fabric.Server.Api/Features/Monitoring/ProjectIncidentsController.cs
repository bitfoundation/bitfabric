using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// A project's History, Incidents, Alerts and Settings tabs (bitfabric-internal/specs/monitoring.md, the plan's PR 2).
/// Every query goes through the tenant filter, so another team's project is simply not found. What the AI tools read and
/// do as well is in <see cref="ProjectIncidentsService"/>.
/// </summary>
[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]"), Authorize(Policy = AuthPolicies.TENANT_SELECTED)]
public partial class ProjectIncidentsController : AppControllerBase, IProjectIncidentsController
{
    [AutoInject] private ProjectAlerter alerter = default!;
    [AutoInject] private Projects.ProjectsService projectsService = default!;
    [AutoInject] private ProjectIncidentsService incidentsService = default!;

    [HttpPost]
    public Task<ProjectHistoryDto> GetHistory(ProjectHistoryRequestDto request, CancellationToken cancellationToken)
        => incidentsService.GetHistory(request, cancellationToken);

    [HttpGet("{monitoringId}")]
    public Task<ProjectIncidentDto[]> GetIncidents(Guid monitoringId, CancellationToken cancellationToken)
        => incidentsService.GetIncidents(monitoringId, cancellationToken);

    [HttpPost("{id}")]
    public Task<ProjectIncidentDto> Acknowledge(Guid id, CancellationToken cancellationToken)
        => incidentsService.Acknowledge(id, User.GetUserId(), User.GetDisplayName(), cancellationToken);

    [HttpPost]
    public Task<ProjectIncidentDto> PostUpdate(ProjectIncidentUpdateRequestDto request, CancellationToken cancellationToken)
        => incidentsService.PostUpdate(request, User.GetDisplayName(), cancellationToken);

    [HttpGet("{monitoringId}")]
    public Task<ProjectMaintenanceWindowDto[]> GetMaintenanceWindows(Guid monitoringId, CancellationToken cancellationToken)
        => incidentsService.GetMaintenanceWindows(monitoringId, cancellationToken);

    [HttpPost]
    public Task<ProjectMaintenanceWindowDto> AddMaintenanceWindow(ProjectMaintenanceWindowDto window, CancellationToken cancellationToken)
        => incidentsService.AddMaintenanceWindow(window, cancellationToken);

    [HttpDelete("{id}/{version}")]
    public async Task DeleteMaintenanceWindow(Guid id, long version, CancellationToken cancellationToken)
    {
        var window = await DbContext.ProjectMaintenanceWindows.FirstOrDefaultAsync(window => window.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ResourceNotFoundException)]);

        window.Version = version;
        DbContext.ProjectMaintenanceWindows.Remove(window);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    [HttpGet("{monitoringId}")]
    public async Task<ProjectAlertSettingsDto> GetAlertSettings(Guid monitoringId, CancellationToken cancellationToken)
    {
        var project = await FindProject(monitoringId, cancellationToken);

        var routes = await DbContext.ProjectAlertRoutes.AsNoTracking()
            .Include(route => route.User)
            .Where(route => route.MonitoringId == project.Id)
            .OrderBy(route => route.Step).ThenBy(route => route.Channel)
            .ToListAsync(cancellationToken);

        return new()
        {
            MonitoringId = project.Id,
            Routes = [.. routes.Select(route => new ProjectAlertRouteDto
            {
                Id = route.Id,
                Step = route.Step,
                DelayMinutes = route.DelayMinutes,
                Channel = route.Channel,
                UserId = route.UserId,
                UserName = route.User?.DisplayName,
                Address = route.Address,
                WebhookFormat = route.WebhookFormat
            })],
            Members = await MembersOf(project.TenantId, cancellationToken)
        };
    }

    [HttpPost]
    public async Task<ProjectAlertSettingsDto> SaveAlertSettings(ProjectAlertSettingsDto alertSettings, CancellationToken cancellationToken)
    {
        var project = await FindProject(alertSettings.MonitoringId, cancellationToken);
        var members = (await MembersOf(project.TenantId, cancellationToken)).Select(member => member.UserId).ToHashSet();

        foreach (var route in alertSettings.Routes)
        {
            var valid = route.Channel switch
            {
                ProjectAlertChannel.Webhook => Uri.TryCreate(route.Address, UriKind.Absolute, out var address) && address.Scheme == Uri.UriSchemeHttps && route.WebhookFormat is not null,
                _ => route.UserId is { } userId && members.Contains(userId)
            };

            if (valid is false || route.Step is < 0 or > 5 || route.DelayMinutes is < 0 or > 24 * 60)
                throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringEachAlertNeedsATarget)]);
        }

        var existing = await DbContext.ProjectAlertRoutes.Where(route => route.MonitoringId == project.Id).ToListAsync(cancellationToken);
        var culture = CultureInfo.CurrentUICulture.Name;

        DbContext.ProjectAlertRoutes.RemoveRange(existing);
        await DbContext.ProjectAlertRoutes.AddRangeAsync(alertSettings.Routes.Select(route => new ProjectAlertRoute
        {
            Id = Guid.CreateSequentialGuid(),
            MonitoringId = project.Id,
            TenantId = project.TenantId,
            Step = route.Step,
            // Step 0 goes out at once.
            DelayMinutes = route.Step is 0 ? 0 : route.DelayMinutes,
            Channel = route.Channel,
            UserId = route.Channel is ProjectAlertChannel.Webhook ? null : route.UserId,
            Address = route.Channel is ProjectAlertChannel.Webhook ? route.Address!.Trim() : null,
            WebhookFormat = route.Channel is ProjectAlertChannel.Webhook ? route.WebhookFormat : null,
            // A route keeps the language it was made in; a new one takes its maker's.
            Culture = existing.FirstOrDefault(old => old.Id == route.Id)?.Culture ?? culture
        }), cancellationToken);

        // Someone an alert reaches works on the project (bitfabric-internal/specs/monitoring.md, Members are who works on it).
        foreach (var userId in alertSettings.Routes.Where(route => route.Channel is not ProjectAlertChannel.Webhook).Select(route => route.UserId!.Value).Distinct())
        {
            await projectsService.EnsureMember(project.ProjectId, project.TenantId, userId, cancellationToken);
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        return await GetAlertSettings(project.Id, cancellationToken);
    }

    [HttpPost("{monitoringId}")]
    public async Task<ProjectAlertDto[]> SendTestAlert(Guid monitoringId, CancellationToken cancellationToken)
    {
        var project = await FindProject(monitoringId, cancellationToken);

        return [.. (await alerter.SendTest(project, cancellationToken)).Select(ProjectIncidentsService.ToDto)];
    }

    [HttpGet("{monitoringId}")]
    public async Task<ProjectSettingsDto> GetSettings(Guid monitoringId, CancellationToken cancellationToken)
    {
        var project = await FindProject(monitoringId, cancellationToken);

        return await SettingsOf(project, cancellationToken);
    }

    private async Task<ProjectSettingsDto> SettingsOf(ProjectMonitoring project, CancellationToken cancellationToken)
    {
        var connection = await DbContext.ProjectConnections.AsNoTracking()
            .Where(connection => connection.MonitoringId == project.Id)
            .Select(connection => new { connection.ConnectedByUserId, connection.ConnectedOn, connection.Resource })
            .FirstOrDefaultAsync(cancellationToken);

        var approver = connection is null ? null : await DbContext.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == connection.ConnectedByUserId, cancellationToken);

        var projectName = await DbContext.Projects.Where(parent => parent.Id == project.ProjectId).Select(parent => parent.Name).FirstOrDefaultAsync(cancellationToken);

        return new()
        {
            Id = project.Id,
            Name = projectName,
            ProjectId = project.ProjectId,
            Environment = project.Environment,
            TimeZoneId = project.TimeZoneId,
            ApprovedBy = approver?.DisplayName,
            ApprovedOn = connection?.ConnectedOn,
            Reads = connection?.Resource,
            Version = project.Version
        };
    }

    [HttpPost]
    public async Task<ProjectSettingsDto> SaveSettings(ProjectSettingsDto projectSettings, CancellationToken cancellationToken)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(projectSettings.TimeZoneId!, out _) is false)
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringUnknownTimeZone)]);

        // Not by the row's Version: every round rewrites the environment's state on the same row, so a version read when the
        // tab opened is stale within a minute. The names and the time zone are fields a person edits now and then, and the
        // last edit stands.
        var monitoring = await FindProject(projectSettings.Id, cancellationToken);
        var environment = projectSettings.Environment!.Trim();

        if (await DbContext.Monitorings.AnyAsync(other => other.ProjectId == monitoring.ProjectId && other.Id != monitoring.Id && other.Environment!.ToLower() == environment.ToLower(), cancellationToken))
            throw new BadRequestException(Localizer["This project already has an environment named {0}.", environment]);

        await DbContext.Monitorings
            .Where(project => project.Id == projectSettings.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(project => project.Environment, environment)
                                                  .SetProperty(project => project.TimeZoneId, projectSettings.TimeZoneId), cancellationToken);

        // The project's name, which every environment of it is called by (See ProjectMonitoring.Name).
        await projectsService.Rename(monitoring.ProjectId, projectSettings.Name!, cancellationToken);

        return await SettingsOf(await FindProject(projectSettings.Id, cancellationToken), cancellationToken);
    }

    private Task<ProjectMonitoring> FindProject(Guid monitoringId, CancellationToken cancellationToken)
        => incidentsService.FindProject(monitoringId, cancellationToken);

    private async Task<ProjectTeamMemberDto[]> MembersOf(Guid tenantId, CancellationToken cancellationToken)
    {
        var members = await DbContext.TenantMemberships.AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.AcceptedOn != null)
            .Select(member => member.User!)
            .ToListAsync(cancellationToken);

        return [.. members.Select(user => new ProjectTeamMemberDto
        {
            UserId = user.Id,
            Name = user.DisplayName,
            HasEmail = string.IsNullOrEmpty(user.Email) is false && user.EmailConfirmed,
            HasPhoneNumber = string.IsNullOrEmpty(user.PhoneNumber) is false && user.PhoneNumberConfirmed
        }).OrderBy(member => member.Name)];
    }
}
