namespace Bit.Fabric.Client.Core.Components.Pages.Identity.Components;

/// <summary>
/// The sign-in providers' logos by name, for the home page's film that draws them (<c>AppHomeFilm.Logo</c>). The films'
/// library can't reference Client.Core, so whoever plays a film, the app or the Workbench, hands it these.
/// </summary>
public static class SignInLogo
{
    public static RenderFragment<string> Fragment { get; } = brand => builder =>
    {
        var icon = brand switch
        {
            "google" => typeof(GoogleIcon),
            "apple" => typeof(AppleIcon),
            "github" => typeof(GitHubIcon),
            "twitter" => typeof(TwitterIcon),
            "facebook" => typeof(FacebookIcon),
            _ => null
        };

        if (icon is null)
            return;

        builder.OpenComponent(0, icon);
        builder.CloseComponent();
    };
}
