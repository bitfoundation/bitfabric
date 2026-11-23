namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

public partial class DirectionBand
{
    /// <summary>The band's name in the address, after the #.</summary>
    [Parameter, EditorRequired] public string Anchor { get; set; } = default!;

    [Parameter, EditorRequired] public string Title { get; set; } = default!;

    /// <summary>The answer to the title's question, in one sentence, said first and larger than a lead.</summary>
    [Parameter] public string? Answer { get; set; }

    [Parameter] public string? Intro { get; set; }

    /// <summary>The band's closing line, said large.</summary>
    [Parameter] public string? Line { get; set; }

    /// <summary>A line under the band that holds for all of it, beside an icon.</summary>
    [Parameter] public string? Note { get; set; }

    [Parameter] public string NoteIcon { get; set; } = BitIconName.Info;

    [Parameter] public RenderFragment? ChildContent { get; set; }
}
