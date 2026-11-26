using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Projects;
using Bit.Fabric.Shared.Features.Projects.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// Switches from one project's page straight to another's (bitfabric-internal/specs/monitoring.md, A project's page
/// switches to another project directly), on the same tab. The team's projects load once, when it first opens.
/// </summary>
public partial class ProjectSwitcher
{
    [Parameter, EditorRequired] public ProjectDto Current { get; set; } = default!;

    /// <summary>The tab the page shows, which the other project opens on; null for Overview.</summary>
    [Parameter] public string? Tab { get; set; }

    [AutoInject] private IProjectsController projectsController = default!;

    private bool isOpen;
    private string? search;
    private List<ProjectDto>? projects;

    private async Task Load()
    {
        projects ??= [.. (await projectsController.GetProjects(TimeZoneService.CurrentTimeZone.Id, CurrentCancellationToken))
            .OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>By the project's name, or by one of its environments' names or addresses.</summary>
    private IEnumerable<ProjectDto> Matches() => (projects ?? []).Where(project => string.IsNullOrWhiteSpace(search)
        || project.Name?.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase) is true
        || project.Monitorings.Any(environment => environment.Environment?.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase) is true
                                                  || environment.Address?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) is true));

    private string HrefOf(ProjectDto project)
        => string.IsNullOrEmpty(Tab) ? $"{PageUrls.Projects}/{project.Id}" : $"{PageUrls.Projects}/{project.Id}?tab={Uri.EscapeDataString(Tab)}";

    /// <summary>"Production, Test", in the language's own way of listing; nothing yet for a project with none.</summary>
    private string EnvironmentsOf(ProjectDto project) => project.Monitorings.Count is 0
        ? Localizer["No environment yet"]
        : MonitoringWords.Parts(Localizer, [.. project.Monitorings.Select(environment => environment.Environment ?? "")]);
}
