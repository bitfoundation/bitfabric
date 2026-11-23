namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

public partial class DirectionPlaceholder
{
    /// <summary>What stands in for the value, such as "KvK 00000000".</summary>
    [Parameter, EditorRequired] public string Text { get; set; } = default!;
}
