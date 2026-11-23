namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>One feature as a card, with an info button that opens its film, or its few lines, in the page's dialog, or its live demo in a new tab.</summary>
public partial class AppHomeFeatureCard
{
    [Parameter, EditorRequired] public AppHomeFeature Feature { get; set; } = default!;

    /// <summary>The heading level of the card's title, one below its section's.</summary>
    [Parameter] public string HeadingElement { get; set; } = "h3";

    [Parameter] public EventCallback<AppHomeFeature> OnExplain { get; set; }

    /// <summary>What the card carries besides what it shows: its film's text, while prerendering.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private string InfoLabel => Feature switch
    {
        { Url: not null } => Localizer[nameof(AppStrings.AppHomeTryLive), Feature.Title],
        { Note: null } => Localizer[nameof(AppStrings.AppHomeSeeHowWorks), Feature.Title],
        _ => Localizer[nameof(AppStrings.AppHomeMoreAbout), Feature.Title]
    };
}
