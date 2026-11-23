namespace Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

/// <summary>
/// A band's tiles, one fact each, as the home page shows where each piece lives. /pricing and
/// /trust use them for their own bands, so the pages look like the home page's siblings. Used as it is by the
/// app and by its design in the Workbench, so it needs nothing of either's base class.
/// </summary>
public partial class AppHomeTiles
{
    [Parameter] public AppHomeTile[] Tiles { get; set; } = [];

    /// <summary>The heading level of each tile's title.</summary>
    [Parameter] public string HeadingElement { get; set; } = "h3";

    /// <summary>The narrowest a tile gets before the row wraps: four tiles of 15rem sit side by side at the page's widest.</summary>
    [Parameter] public string MinWidth { get; set; } = "15rem";
}

/// <summary>
/// One tile: an icon, a short title and one line, with an amount when it has one, such as "From $10 a month", and anything
/// it needs under its line, such as a link.
/// </summary>
public sealed record AppHomeTile(string Icon, string Title, string Text, string? Value = null, RenderFragment? More = null);
