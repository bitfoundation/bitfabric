namespace Bit.Fabric.Workbench.Components.Pages.Boards.GetStarted;

/// <summary>
/// The example board bit Fabric's Workbench comes with: how a visitor gets from bitfabric.co's home page to the wizard that
/// creates their project, told on the two pages themselves (BoardFlow).
/// </summary>
public partial class GetStartedBoard
{
    private BoardFlowScreen[] Screens =>
    [
        new("home", "app-home", Localizer[nameof(WorkbenchStrings.GetStartedBoardHome)]),
        new("wizard", "new-project", Localizer[nameof(WorkbenchStrings.GetStartedBoardWizard)])
    ];

    private BoardFlowLink[] Links =>
    [
        new("home", "wizard", Localizer[nameof(WorkbenchStrings.GetStartedBoardButton)])
    ];
}
