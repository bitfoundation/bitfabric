namespace Boilerplate.Workbench.Components.Shell;

/// <summary>
/// The side list of a section's items next to the open one, grouped, with the open one marked, so another opens without
/// going back to the section's list: the boards and the flows. Pages have their navigator and Progress its task cards.
/// </summary>
public partial class SectionList
{
    /// <summary>The name of the list as a landmark, like the section's name.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    [Parameter] public IReadOnlyList<SectionListGroup> Groups { get; set; } = [];

    /// <summary>The key of the item that is open.</summary>
    [Parameter] public string? CurrentKey { get; set; }
}

/// <summary>A group of the side list, under its title; a group without items isn't listed.</summary>
public sealed record SectionListGroup(string Title, IReadOnlyList<SectionListItem> Items);

/// <summary>
/// An item of the side list: its title, written as it reads in its own direction, a short line under it, its icon and the
/// tone that colors the icon (primary, secondary, tertiary, info, success or warning).
/// </summary>
public sealed record SectionListItem(string Key, string Title, string? Meta, string IconName, string Href, string? Tone = null);
