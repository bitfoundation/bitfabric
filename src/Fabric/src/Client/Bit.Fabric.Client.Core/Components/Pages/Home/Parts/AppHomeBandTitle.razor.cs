using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

/// <summary>
/// The title of one of the home page's bands, which holds the band's address: bitfabric.co/#windows, say. It leads to the
/// band, and a click or a tap copies it. Used as it is by the app and by its design in the Workbench, so it needs nothing
/// of either's base class.
/// </summary>
public partial class AppHomeBandTitle
{
    private const int CopiedMilliseconds = 2000;

    [Inject] private Clipboard clipboard { get; set; } = default!;
    [Inject] private NavigationManager navigationManager { get; set; } = default!;
    [Inject] private IStringLocalizer<AppStrings> Localizer { get; set; } = default!;

    private ElementReference target;
    private bool isCopied;

    /// <summary>The band's name in the address, after the #.</summary>
    [Parameter, EditorRequired] public string Anchor { get; set; } = default!;

    [Parameter, EditorRequired] public string Text { get; set; } = default!;

    /// <summary>The heading level of the title.</summary>
    [Parameter] public string Element { get; set; } = "h3";

    /// <summary>A path rather than the whole address, which behind a proxy the server doesn't know while it prerenders.</summary>
    private string Address => $"{new Uri(navigationManager.Uri).PathAndQuery}#{Anchor}";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        // A browser that got the page prerendered has gone there by itself, but one that didn't had nothing to go to yet
        if (firstRender && new Uri(navigationManager.Uri).Fragment == $"#{Anchor}")
        {
            await target.ScrollIntoView();
        }
    }

    private async Task Copy()
    {
        try
        {
            await clipboard.WriteText($"{navigationManager.Uri.Split('#')[0]}#{Anchor}");
        }
        catch (JSException)
        {
            // The browser refused, and the title is still a link its menu can copy
            return;
        }

        isCopied = true;
        StateHasChanged();

        await Task.Delay(CopiedMilliseconds);
        isCopied = false;
    }
}
