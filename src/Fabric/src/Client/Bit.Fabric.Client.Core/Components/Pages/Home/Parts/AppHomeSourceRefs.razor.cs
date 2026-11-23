using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

/// <summary>
/// The numbers of a fact's sources, in the order the page lists them (<see cref="PageIds"/>), each a link to its entry in
/// the page's <see cref="AppHomeSourceList"/>. Used as it is by the app and by its design in the Workbench, so it needs
/// nothing of either's base class.
/// </summary>
public partial class AppHomeSourceRefs
{
    [Inject] private NavigationManager navigationManager { get; set; } = default!;
    [Inject] private IStringLocalizer<AppStrings> Localizer { get; set; } = default!;

    /// <summary>Every source the page cites, in the order its list numbers them.</summary>
    [Parameter, EditorRequired] public string[] PageIds { get; set; } = [];

    /// <summary>The sources of this fact.</summary>
    [Parameter, EditorRequired] public string[] Ids { get; set; } = [];

    /// <summary>
    /// A place on this page, with the page's own path: the site's base address is its root, so a bare #anchor would lead to
    /// the home page, as the band titles' addresses know (See AppHomeBandTitle).
    /// </summary>
    private string InPage(string anchor) => $"{new Uri(navigationManager.Uri).AbsolutePath}#{anchor}";
}
