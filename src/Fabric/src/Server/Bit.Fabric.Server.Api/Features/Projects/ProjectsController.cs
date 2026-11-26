using Bit.Fabric.Server.Api.Features.Monitoring;
using Bit.Fabric.Shared.Features.Projects;
using Bit.Fabric.Shared.Features.Projects.Dtos;

namespace Bit.Fabric.Server.Api.Features.Projects;

/// <summary>
/// The current team's projects (bitfabric-internal/specs/monitoring.md, Projects, environments and their people). A
/// selected team is required: without one a write would land in the fallback tenant every tenant-less user shares.
/// </summary>
[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]"), Authorize(Policy = AuthPolicies.TENANT_SELECTED)]
public partial class ProjectsController : AppControllerBase, IProjectsController
{
    [AutoInject] private ProjectsService projectsService = default!;

    /// <summary>Read the same way by the AI tools (See ProjectsService).</summary>
    [HttpGet]
    public Task<List<ProjectDto>> GetProjects(string? timeZone, CancellationToken cancellationToken)
        => projectsService.GetProjects(MonitoringsController.ZoneOf(timeZone), cancellationToken);

    [HttpGet("{id}")]
    public Task<ProjectDto> GetProject(Guid id, string? timeZone, CancellationToken cancellationToken)
        => projectsService.GetProject(id, MonitoringsController.ZoneOf(timeZone), cancellationToken);

    [HttpPost]
    public async Task<ProjectDto> Rename(RenameProjectRequestDto request, CancellationToken cancellationToken)
    {
        await projectsService.Rename(request.Id, request.Name!, cancellationToken);

        return await projectsService.GetProject(request.Id, TimeZoneInfo.Utc, cancellationToken);
    }

    [HttpGet("{id}")]
    public Task<List<ProjectMemberDto>> GetMembers(Guid id, CancellationToken cancellationToken)
        => projectsService.GetMembers(id, cancellationToken);

    [HttpPost]
    public async Task<List<ProjectMemberDto>> AddMember(ProjectMemberRequestDto request, CancellationToken cancellationToken)
    {
        var project = await projectsService.FindProject(request.ProjectId, cancellationToken);

        await projectsService.EnsureMember(project.Id, project.TenantId, request.UserId, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);

        return await projectsService.GetMembers(project.Id, cancellationToken);
    }

    [HttpPost]
    public async Task<List<ProjectMemberDto>> RemoveMember(ProjectMemberRequestDto request, CancellationToken cancellationToken)
    {
        await projectsService.RemoveMember(request.ProjectId, request.UserId, cancellationToken);

        return await projectsService.GetMembers(request.ProjectId, cancellationToken);
    }
}
