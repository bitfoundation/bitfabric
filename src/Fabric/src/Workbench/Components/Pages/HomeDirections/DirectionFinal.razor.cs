namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

public partial class DirectionFinal
{
    [Parameter, EditorRequired] public string Title { get; set; } = default!;

    [Parameter, EditorRequired] public string Text { get; set; } = default!;
}
