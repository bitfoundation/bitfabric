namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

public partial class DirectionTileItem
{
    [Parameter, EditorRequired] public DirectionTile Tile { get; set; } = default!;

    [Parameter] public DirectionTileLook Look { get; set; }

    [Parameter] public bool Inline { get; set; }
}
