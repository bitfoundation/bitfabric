using Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;
using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

/// <summary>
/// A page's list of sources, each with the anchor its numbers lead to (See <see cref="AppHomeSourceRefs"/>). Used as it is by
/// the app and by its design in the Workbench, so it needs nothing of either's base class.
/// </summary>
public partial class AppHomeSourceList
{
    [Inject] private IStringLocalizer<AppStrings> Localizer { get; set; } = default!;

    /// <summary>The sources, in the order the page first cites them.</summary>
    [Parameter, EditorRequired] public CompareSource[] Sources { get; set; } = [];

    /// <summary>The day they were read, as the page writes a date, such as "21 November 2026".</summary>
    [Parameter, EditorRequired] public string ReadOn { get; set; } = default!;

    /// <summary>The id of a source's entry in the list, which the numbers beside the facts lead to.</summary>
    public static string AnchorOf(string sourceId) => $"src-{sourceId.ToLowerInvariant()}";

    /// <summary>An address as a reader would type it, without its scheme or a trailing slash.</summary>
    private static string Shown(string url) => url.Replace("https://", string.Empty, StringComparison.Ordinal).TrimEnd('/');
}
