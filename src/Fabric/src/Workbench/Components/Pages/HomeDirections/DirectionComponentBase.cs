using Bit.Fabric.Client.Core.Components.Pages.Home;

namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

/// <summary>
/// Base of the home page directions and their building blocks: where "Get started" leads, an address inside the page, and
/// the film a feature plays in the page's dialog (See HomeDirectionsPage).
/// </summary>
public abstract class DirectionComponentBase : WorkbenchComponentBase
{
    /// <summary>The app's wizard, which the Workbench opens as its new-project page in the view's settings (See SitePageHost).</summary>
    protected static string GetStartedUrl => PageUrls.NewProject;

    /// <summary>Plays a feature's film in the page's dialog.</summary>
    [Parameter] public EventCallback<AppHomeFeature> OnExplain { get; set; }

    /// <summary>
    /// A band of this page, as the path of the page with the band after the #: a bare #band would lead to the site's root,
    /// as the app's base address is /, rather than down the page (See AppHomeBandTitle).
    /// </summary>
    protected string InPage(string anchor) => $"{new Uri(NavigationManager.Uri).PathAndQuery}#{anchor}";

    protected Task Play(string filmId, string icon, string title, string text) => OnExplain.InvokeAsync(new AppHomeFeature(filmId, icon, title, text));
}
