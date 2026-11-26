using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Projects;
using Bit.Fabric.Shared.Features.Projects.Dtos;
using Bit.Fabric.Shared.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// The stand-in for bit Fabric's <see cref="IProjectsController"/>: the sample world's environments, gathered into their
/// projects the way the server does, each with a license in one of the states the Licenses tab shows, by its place in the
/// list so every state is seen.
/// </summary>
public sealed class WorkbenchProjectsController(WorkbenchScenarioState scenarioState, MonitoringSampleWorld world, AuthenticationStateProvider authenticationStateProvider,
    IMonitoringsController monitorings)
    : MonitoringStandIn(scenarioState, world, authenticationStateProvider), IProjectsController
{
    public async Task<List<ProjectDto>> GetProjects(string? timeZone, CancellationToken cancellationToken)
    {
        var environments = await monitorings.GetMonitorings(timeZone, cancellationToken);

        return [.. environments.GroupBy(environment => environment.ProjectId).Select((group, index) => new ProjectDto
        {
            Id = group.Key,
            Name = group.First().ProjectName,
            BitProjectId = index % 5 is 4 ? null : group.Key,
            Monitorings = [.. group],
            License = LicenseOf(index, World.Now),
            MemberCount = 1 + index % 3,
            CreatedOn = group.Min(environment => environment.CreatedOn)
        })];
    }

    public async Task<ProjectDto> GetProject(Guid id, string? timeZone, CancellationToken cancellationToken)
        => (await GetProjects(timeZone, cancellationToken)).FirstOrDefault(project => project.Id == id)
           ?? throw new ResourceNotFoundException(AppStrings.ResourceNotFoundException);

    public async Task<ProjectDto> Rename(RenameProjectRequestDto request, CancellationToken cancellationToken)
    {
        await Save(() =>
        {
            foreach (var environment in World.Projects.Where(project => (project.ProjectId ?? project.Id) == request.Id))
            {
                environment.Name = request.Name!;
            }
        }, cancellationToken);

        return await GetProject(request.Id, null, cancellationToken);
    }

    public async Task<List<ProjectMemberDto>> GetMembers(Guid id, CancellationToken cancellationToken)
    {
        var me = await CurrentUserName();

        return await Load<List<ProjectMemberDto>>("members", () =>
        [
            new() { UserId = Guid.Parse("8ff71671-a1d6-4f97-abb9-d87d7b47d6e7"), Name = me, IsMember = true, AddedOn = World.Now.AddDays(-40), AlertRouteCount = 2 },
            new() { UserId = Guid.Parse("2b9a6c1e-5d3f-4e8a-9b7c-1f2e3d4c5b6a"), Name = "Sara", IsMember = true, AddedOn = World.Now.AddDays(-12), AlertRouteCount = 1 },
            new() { UserId = Guid.Parse("7c1d2e3f-4a5b-4c6d-8e9f-0a1b2c3d4e5f"), Name = "Navid", IsMember = false }
        ], () => [], cancellationToken);
    }

    public async Task<List<ProjectMemberDto>> AddMember(ProjectMemberRequestDto request, CancellationToken cancellationToken)
    {
        await Save(() => { }, cancellationToken);
        return await GetMembers(request.ProjectId, cancellationToken);
    }

    public async Task<List<ProjectMemberDto>> RemoveMember(ProjectMemberRequestDto request, CancellationToken cancellationToken)
    {
        await Save(() => { }, cancellationToken);
        return await GetMembers(request.ProjectId, cancellationToken);
    }

    /// <summary>Every state the Licenses tab draws, one after the other down the list.</summary>
    private static ProjectLicenseDto LicenseOf(int index, DateTimeOffset now) => (index % 5) switch
    {
        0 => new() { Kind = ProjectLicenseKind.Licensed, State = LicenseState.InForce, Until = now.AddDays(300), LicenseId = Guid.NewGuid() },
        1 => new() { Kind = ProjectLicenseKind.Trial, Until = now.AddDays(18) },
        2 => new() { Kind = ProjectLicenseKind.Licensed, State = LicenseState.EndingSoon, Until = now.AddDays(12), LicenseId = Guid.NewGuid() },
        3 => new() { Kind = ProjectLicenseKind.TrialEnded, Until = now.AddDays(-3) },
        _ => new() { Kind = ProjectLicenseKind.Unknown }
    };
}
