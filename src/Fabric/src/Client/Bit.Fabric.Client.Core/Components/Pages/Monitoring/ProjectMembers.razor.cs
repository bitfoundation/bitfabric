using Bit.Fabric.Shared.Features.Projects;
using Bit.Fabric.Shared.Features.Projects.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// A project's people on its Settings tab (bitfabric-internal/specs/monitoring.md, A project's name and an environment's
/// name): the team's members, those on the project first, each added or taken off with one button.
/// </summary>
public partial class ProjectMembers
{
    [Parameter, EditorRequired] public Guid ProjectId { get; set; }

    [AutoInject] private IProjectsController projectsController = default!;

    private List<ProjectMemberDto>? people;
    private Guid? busy;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        people = await projectsController.GetMembers(ProjectId, CurrentCancellationToken);
    }

    private Task Add(ProjectMemberDto person) => Change(person, () => projectsController.AddMember(new() { ProjectId = ProjectId, UserId = person.UserId }, CurrentCancellationToken));

    private Task Remove(ProjectMemberDto person) => Change(person, () => projectsController.RemoveMember(new() { ProjectId = ProjectId, UserId = person.UserId }, CurrentCancellationToken));

    private async Task Change(ProjectMemberDto person, Func<Task<List<ProjectMemberDto>>> change)
    {
        if (busy is not null) return;
        busy = person.UserId;

        try
        {
            people = await change();
        }
        finally
        {
            busy = null;
        }
    }
}
