using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Server.Api.Features.Projects.Models;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Features.Projects.Dtos;

namespace Bit.Fabric.Server.Api.Features.Projects;

/// <summary>
/// The current team's projects (bitfabric-internal/specs/monitoring.md, Projects, environments and their people), for
/// <see cref="ProjectsController"/> and the AI tools alike. Every query goes through the tenant filter: another team's
/// project is simply not found.
/// </summary>
public partial class ProjectsService(AppDbContext dbContext,
    MonitoringsService monitoringsService,
    ProjectLicenses projectLicenses,
    TimeProvider timeProvider,
    IStringLocalizer<AppStrings> localizer)
{
    /// <summary>Every project of the team, those needing someone first, each with its environments' cards and its license.</summary>
    public async Task<List<ProjectDto>> GetProjects(TimeZoneInfo zone, CancellationToken cancellationToken)
    {
        var projects = await dbContext.Projects.AsNoTracking().ToListAsync(cancellationToken);
        var cards = (await monitoringsService.GetMonitorings(zone, cancellationToken)).ToLookup(card => card.ProjectId);

        return [.. (await ToDtos(projects, cards, cancellationToken))
            .OrderBy(project => project.Monitorings.Count is 0 ? int.MaxValue : project.Monitorings.Min(MonitoringsService.AttentionRank))
            .ThenBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task<ProjectDto> GetProject(Guid id, TimeZoneInfo zone, CancellationToken cancellationToken)
    {
        var project = await FindProject(id, cancellationToken);
        var cards = (await monitoringsService.GetMonitorings(zone, cancellationToken)).Where(card => card.ProjectId == id).ToLookup(card => card.ProjectId);

        return (await ToDtos([project], cards, cancellationToken)).Single();
    }

    public async Task<Project> FindProject(Guid id, CancellationToken cancellationToken)
        => await dbContext.Projects.AsNoTracking().FirstOrDefaultAsync(project => project.Id == id, cancellationToken)
           ?? throw new ResourceNotFoundException(localizer[nameof(AppStrings.ResourceNotFoundException)]);

    /// <summary>Renames the project, and with it what its environments are called (See ProjectMonitoring.Name).</summary>
    public async Task Rename(Guid id, string name, CancellationToken cancellationToken)
    {
        name = name.Trim();

        // Not by the row's Version: a name is a field a person edits now and then, and the last edit stands.
        var updated = await dbContext.Projects.Where(project => project.Id == id)
                                              .ExecuteUpdateAsync(setters => setters.SetProperty(project => project.Name, name), cancellationToken);

        if (updated is 0)
            throw new ResourceNotFoundException(localizer[nameof(AppStrings.ResourceNotFoundException)]);

        await RenameEnvironments(id, name, cancellationToken);
    }

    /// <summary>Writes what each environment of the project is called, after the project or one of them was renamed.</summary>
    public async Task RenameEnvironments(Guid projectId, string projectName, CancellationToken cancellationToken)
    {
        var environments = await dbContext.Monitorings.Where(monitoring => monitoring.ProjectId == projectId)
                                                      .Select(monitoring => new { monitoring.Id, monitoring.Environment })
                                                      .ToListAsync(cancellationToken);

        foreach (var environment in environments)
        {
            var name = ProjectMonitoring.NameOf(projectName, environment.Environment);

            // Without the Version check, as SaveSettings: every round rewrites the environment's own row.
            await dbContext.Monitorings.Where(monitoring => monitoring.Id == environment.Id)
                                       .ExecuteUpdateAsync(setters => setters.SetProperty(monitoring => monitoring.Name, name), cancellationToken);
        }
    }

    /// <summary>The team's people who accepted it, the project's members first, each with their alert routes on it.</summary>
    public async Task<List<ProjectMemberDto>> GetMembers(Guid id, CancellationToken cancellationToken)
    {
        var project = await FindProject(id, cancellationToken);

        var people = await dbContext.TenantMemberships.AsNoTracking()
            .Where(membership => membership.TenantId == project.TenantId && membership.AcceptedOn != null)
            .Select(membership => new { membership.UserId, membership.User!.FullName, membership.User.UserName })
            .ToListAsync(cancellationToken);

        var members = await dbContext.ProjectMembers.AsNoTracking()
            .Where(member => member.ProjectId == id)
            .ToDictionaryAsync(member => member.UserId, member => member.AddedOn, cancellationToken);

        var routes = (await dbContext.ProjectAlertRoutes.AsNoTracking()
            .Where(route => route.UserId != null && route.Monitoring!.ProjectId == id)
            .Select(route => route.UserId!.Value)
            .ToListAsync(cancellationToken))
            .CountBy(userId => userId)
            .ToDictionary();

        return [.. people.Select(person => new ProjectMemberDto
        {
            UserId = person.UserId,
            Name = person.FullName ?? person.UserName,
            IsMember = members.ContainsKey(person.UserId),
            AddedOn = members.TryGetValue(person.UserId, out var addedOn) ? addedOn : null,
            AlertRouteCount = routes.GetValueOrDefault(person.UserId)
        })
        .OrderByDescending(person => person.IsMember)
        .ThenBy(person => person.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>
    /// Makes <paramref name="userId"/> a member of the project, unless they are one already: whoever adds an environment, gets
    /// it a license or is given an alert route on it. They must have accepted the team, which the key checks too.
    /// </summary>
    public async Task EnsureMember(Guid projectId, Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var isMember = await dbContext.ProjectMembers.IgnoreQueryFilters().AnyAsync(member => member.TenantId == tenantId && member.ProjectId == projectId && member.UserId == userId, cancellationToken);

        if (isMember)
            return;

        var hasAccepted = await dbContext.TenantMemberships.AnyAsync(membership => membership.TenantId == tenantId && membership.UserId == userId && membership.AcceptedOn != null, cancellationToken);

        if (hasAccepted is false)
            throw new BadRequestException(localizer[nameof(AppStrings.ProjectsOnlyTeamMembers)]);

        await dbContext.ProjectMembers.AddAsync(new() { ProjectId = projectId, TenantId = tenantId, UserId = userId, AddedOn = timeProvider.GetUtcNow() }, cancellationToken);
    }

    /// <summary>Takes someone off the project, and their alert routes on its environments with them.</summary>
    public async Task RemoveMember(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        await FindProject(projectId, cancellationToken);

        await dbContext.ProjectAlertRoutes.Where(route => route.UserId == userId && route.Monitoring!.ProjectId == projectId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.ProjectMembers.Where(member => member.ProjectId == projectId && member.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<List<ProjectDto>> ToDtos(IReadOnlyCollection<Project> projects, ILookup<Guid, MonitoringDto> cards, CancellationToken cancellationToken)
    {
        Guid[] ids = [.. projects.Select(project => project.Id)];

        var memberCounts = await dbContext.ProjectMembers.AsNoTracking()
            .Where(member => ids.Contains(member.ProjectId))
            .GroupBy(member => member.ProjectId)
            .Select(group => new { ProjectId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.ProjectId, group => group.Count, cancellationToken);

        var licenses = await projectLicenses.Of(projects, cancellationToken);

        return [.. projects.Select(project => new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            BitProjectId = project.BitProjectId,
            Monitorings = [.. cards[project.Id].OrderBy(card => string.Equals(card.Environment, ProjectMonitoring.Production, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                                               .ThenBy(card => card.Environment, StringComparer.CurrentCultureIgnoreCase)],
            License = licenses[project.Id],
            MemberCount = memberCounts.GetValueOrDefault(project.Id),
            CreatedOn = project.CreatedOn,
            Version = project.Version
        })];
    }
}
