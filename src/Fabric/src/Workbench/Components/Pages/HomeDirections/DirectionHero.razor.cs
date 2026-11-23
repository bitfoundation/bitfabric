namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

public partial class DirectionHero
{
    [Parameter] public string? Eyebrow { get; set; }

    /// <summary>The h1, a sentence a line, each broken inside only where the screen is too narrow for it.</summary>
    [Parameter, EditorRequired] public string[] TitleLines { get; set; } = [];

    /// <summary>How large the h1 is drawn: a definition reads better smaller than a slogan.</summary>
    [Parameter] public BitTypography TitleTypography { get; set; } = BitTypography.H2;

    [Parameter, EditorRequired] public string Lead { get; set; } = default!;

    [Parameter] public string? SecondaryText { get; set; }

    [Parameter] public string? SecondaryHref { get; set; }

    [Parameter] public string? SecondaryIcon { get; set; }

    /// <summary>The line under the buttons; the app's own ("Starting is free · No account needed · ...") when not given.</summary>
    [Parameter] public string? Fine { get; set; }

    [Parameter] public DirectionHeroLayout Layout { get; set; }

    /// <summary>What sits under the line, such as a row of short promises.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The direction's picture, beside the words in a split hero and under them otherwise.</summary>
    [Parameter] public RenderFragment? Aside { get; set; }
}
