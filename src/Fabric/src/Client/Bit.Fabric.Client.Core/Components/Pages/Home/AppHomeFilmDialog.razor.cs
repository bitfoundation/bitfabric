using Bit.Fabric.Client.Core.Components.Pages.Identity.Components;

namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>The dialog a feature's film, or its few lines, plays in, on the home page, <c>/features</c> and <c>/contents</c>.</summary>
public partial class AppHomeFilmDialog
{
    /// <summary>The feature whose film or few lines are open, if any.</summary>
    [Parameter] public AppHomeFeature? Playing { get; set; }

    /// <summary>The film player's component, once the films' library is loaded (See AppHomePageBase.Explain).</summary>
    [Parameter] public Type? FilmType { get; set; }

    /// <summary>Whether the home page's onboarding opened it, rather than the visitor.</summary>
    [Parameter] public bool IsOnboarding { get; set; }

    [Parameter] public EventCallback OnClose { get; set; }

    private async Task Changed(bool isOpen)
    {
        if (isOpen is false)
        {
            await Close();
        }
    }

    private Task Close() => OnClose.InvokeAsync();

    /// <summary>The film player's parameters. Called only once the films' library is loaded, as it names its types.</summary>
    private Dictionary<string, object?> FilmParameters(AppHomeFeature feature) => new()
    {
        ["Title"] = feature.Title,
        ["Scenes"] = new Bit.Fabric.Client.Films.Components.AppHomeFilms(Localizer).Of(feature.Id),
        ["Logo"] = SignInLogo.Fragment,
        ["OnClose"] = EventCallback.Factory.Create(this, Close)
    };
}
