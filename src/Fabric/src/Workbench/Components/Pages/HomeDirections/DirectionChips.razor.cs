namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

public partial class DirectionChips
{
    [Parameter, EditorRequired] public (string Icon, string Text)[] Items { get; set; } = [];

    /// <summary>What the row is, for a screen reader.</summary>
    [Parameter] public string? Label { get; set; }

    [Parameter] public bool IsCentered { get; set; }
}
