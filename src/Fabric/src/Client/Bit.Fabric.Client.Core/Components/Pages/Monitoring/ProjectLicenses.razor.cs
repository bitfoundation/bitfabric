using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Bit.Fabric.Shared.Features.Projects.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>Each of the team's projects and its license, as the Projects page's Licenses tab lists them.</summary>
public partial class ProjectLicenses
{
    [Parameter, EditorRequired] public List<ProjectDto> Projects { get; set; } = [];

    /// <summary>The separator between the parts of a line, which in Persian isn't a middle dot (See MonitoringWords.Dot).</summary>
    private string Dot => MonitoringWords.Dot(Localizer);

    /// <summary>What needs something first: ended, then about to end, then the rest, each by name.</summary>
    private IEnumerable<ProjectDto> Ordered() => Projects
        .OrderBy(project => (project.License.Kind, project.License.State) switch
        {
            (ProjectLicenseKind.LicenseEnded or ProjectLicenseKind.TrialEnded, _) => 0,
            (ProjectLicenseKind.Licensed, LicenseState.InGrace or LicenseState.EndingSoon) => 1,
            (ProjectLicenseKind.None or ProjectLicenseKind.Trial, _) => 2,
            (ProjectLicenseKind.Licensed, _) => 3,
            _ => 4
        })
        .ThenBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase);

    private string EnvironmentsOf(ProjectDto project) => project.Monitorings.Count is 0
        ? Localizer[nameof(AppStrings.MonitoringNoEnvironmentYet)]
        : MonitoringWords.Parts(Localizer, [.. project.Monitorings.Select(environment => environment.Environment ?? "")]);
}
