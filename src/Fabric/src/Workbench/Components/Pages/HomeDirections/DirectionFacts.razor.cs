namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

public partial class DirectionFacts
{
    /// <summary>The box's own heading, such as "Key facts"; none for a list under a band's title.</summary>
    [Parameter] public string? Title { get; set; }

    [Parameter] public string TitleId { get; set; } = "dfx-title";

    /// <summary>The heading level of the box's title: h2 on its own, as under a hero, and h3 inside a band.</summary>
    [Parameter] public string TitleElement { get; set; } = "h2";

    [Parameter, EditorRequired] public DirectionFact[] Items { get; set; } = [];

    /// <summary>A framed box of facts, one under the other, rather than a grid of terms.</summary>
    [Parameter] public bool IsBox { get; set; }
}
