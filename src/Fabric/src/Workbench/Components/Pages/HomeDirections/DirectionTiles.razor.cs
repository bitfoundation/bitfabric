namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

/// <summary>How a band's tiles are drawn.</summary>
public enum DirectionTileLook
{
    /// <summary>On the page's second background, with a round icon.</summary>
    Filled,

    /// <summary>Framed by a line, with a round icon.</summary>
    Outlined,

    /// <summary>Numbered in their order, with no icon.</summary>
    Steps,

    /// <summary>A number said large, with its line under it.</summary>
    Figure
}

public partial class DirectionTiles
{
    [Parameter, EditorRequired] public DirectionTile[] Items { get; set; } = [];

    [Parameter] public DirectionTileLook Look { get; set; }

    /// <summary>The narrowest a tile gets before the row wraps.</summary>
    [Parameter] public string MinWidth { get; set; } = "14rem";

    /// <summary>Each tile's icon beside its words rather than over them, for tiles stacked in a narrow column.</summary>
    [Parameter] public bool Inline { get; set; }
}
