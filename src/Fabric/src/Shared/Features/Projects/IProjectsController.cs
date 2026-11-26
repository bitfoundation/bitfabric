using Bit.Fabric.Shared.Features.Projects.Dtos;

namespace Bit.Fabric.Shared.Features.Projects;

/// <summary>
/// The current team's projects (bitfabric-internal/specs/monitoring.md, Projects, environments and their people): each
/// with its environments, its license and its people. A project's environments are read and changed through
/// IMonitoringsController. Every action needs a selected team, never the fallback tenant.
/// </summary>
[Route("api/v1/[controller]/[action]/"), AuthorizedApi]
public interface IProjectsController : IAppController
{
    /// <summary>
    /// Every project of the team, failing ones first, each with its environments' cards, their last 30 days drawn in
    /// <paramref name="timeZone"/> (the viewer's IANA or Windows zone id; UTC when it is missing or unknown), and its license.
    /// </summary>
    [HttpGet("{?timeZone}")]
    Task<List<ProjectDto>> GetProjects(string? timeZone, CancellationToken cancellationToken);

    /// <summary>The project, its environments as their cards show them, and its license.</summary>
    [HttpGet("{id}/{?timeZone}")]
    Task<ProjectDto> GetProject(Guid id, string? timeZone, CancellationToken cancellationToken);

    /// <summary>Renames the project, and with it what its environments are called in alerts, status pages and emails.</summary>
    [HttpPost]
    Task<ProjectDto> Rename(RenameProjectRequestDto request, CancellationToken cancellationToken);

    /// <summary>The team's people, members of the project first.</summary>
    [HttpGet("{id}")]
    Task<List<ProjectMemberDto>> GetMembers(Guid id, CancellationToken cancellationToken);

    [HttpPost]
    Task<List<ProjectMemberDto>> AddMember(ProjectMemberRequestDto request, CancellationToken cancellationToken);

    /// <summary>Takes someone off the project, and with them their alert routes on its environments.</summary>
    [HttpPost]
    Task<List<ProjectMemberDto>> RemoveMember(ProjectMemberRequestDto request, CancellationToken cancellationToken);
}
