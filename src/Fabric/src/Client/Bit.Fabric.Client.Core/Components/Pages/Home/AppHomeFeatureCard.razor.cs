namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// One feature as a card, with an info button that opens its film, or its few lines, in the page's dialog, or its live demo in
/// a new tab, and, for a feature with details, a "More info" toggle that opens them inside the card.
/// </summary>
public partial class AppHomeFeatureCard
{
    [Parameter, EditorRequired] public AppHomeFeature Feature { get; set; } = default!;

    /// <summary>The heading level of the card's title, one below its section's.</summary>
    [Parameter] public string HeadingElement { get; set; } = "h3";

    [Parameter] public EventCallback<AppHomeFeature> OnExplain { get; set; }

    /// <summary>What the card carries besides what it shows: its film's text, while prerendering.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The cards of its grid, which keep their height while this card's "More info" is open.</summary>
    [Parameter] public AppHomeCardGroup? Group { get; set; }

    /// <summary>The card itself, whose height its group holds.</summary>
    internal ElementReference Element;

    /// <summary>Whether the card's "More info" part is open.</summary>
    private bool isDetailsShown;

    /// <summary>The "More info" part's id: a feature's id is unique on its page.</summary>
    private string DetailsId => $"ah-more-{Feature.Id}";

    private string DetailsToggleId => $"{DetailsId}-toggle";

    protected override async Task OnInitAsync()
    {
        Group?.Add(this);

        await base.OnInitAsync();
    }

    /// <summary>Opens or closes the card's "More info", the cards of its grid holding the height they had while it is open.</summary>
    private async Task ToggleDetails(bool open)
    {
        if (open && Group is not null)
        {
            await Group.Hold();
        }

        isDetailsShown = open;

        if (open is false && Group is not null)
        {
            await Group.Release();
        }
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        Group?.Remove(this);

        await base.DisposeAsync(disposing);
    }

    private string InfoLabel => Feature switch
    {
        { Url: not null } => Localizer[nameof(AppStrings.AppHomeTryLive), Feature.Title],
        { Note: null } => Localizer[nameof(AppStrings.AppHomeSeeHowWorks), Feature.Title],
        _ => Localizer[nameof(AppStrings.AppHomeMoreAbout), Feature.Title]
    };
}
