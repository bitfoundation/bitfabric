namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

public partial class DirectionQuestions
{
    [Parameter, EditorRequired] public DirectionQuestion[] Items { get; set; } = [];
}
