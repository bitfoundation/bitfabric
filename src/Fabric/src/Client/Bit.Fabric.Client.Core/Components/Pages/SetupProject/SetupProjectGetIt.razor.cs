using Bit.Fabric.Client.Core.Components.Pages.NewProject;

namespace Bit.Fabric.Client.Core.Components.Pages.SetupProject;

/// <summary>
/// The last part of the page that sets up a project that already exists: the line for this computer with the same steps
/// the new project page shows (<see cref="NewProject.Parts.NewProjectLineSteps"/>), or what to do to get one, and what
/// bitfabric.co keeps.
/// </summary>
public partial class SetupProjectGetIt
{
    [Parameter, EditorRequired] public SetupProjectAnswers Setup { get; set; } = default!;

    /// <summary>What the line does, step by step and in plain words, in the order bit setup does it.</summary>
    private IEnumerable<string> PlanSteps
    {
        get
        {
            yield return Localizer[nameof(AppStrings.NewProjectPlanSdk)];

            if (Setup.IsOnThisComputer is false)
                yield return Localizer[nameof(AppStrings.SetupProjectPlanClone), Setup.RepositoryName!];

            yield return Localizer[nameof(AppStrings.NewProjectPlanTools)];
            yield return Localizer[nameof(AppStrings.SetupProjectPlanContainers)];
            yield return Localizer[nameof(AppStrings.SetupProjectPlanBuild)];
            yield return Localizer[nameof(AppStrings.NewProjectPlanStart)];

            if (Setup.Devices.Count > 0)
                yield return Localizer[nameof(AppStrings.NewProjectPlanDevices), ListOf(Setup.Devices.Select(DeviceName))];

            yield return Localizer[nameof(AppStrings.SetupProjectPlanVsCode)];
            yield return Localizer[nameof(AppStrings.NewProjectPlanFailure)];
        }
    }
}
