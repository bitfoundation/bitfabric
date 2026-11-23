namespace Bit.Fabric.Client.Core.Components.Pages;

public partial class OpenSourceLicensesPage
{
    /// <summary>
    /// The acknowledgements are some 85 cards, and rendered with the rest of the page they took a large part of its start on a
    /// phone (bitfabric-internal/specs/app-shell.md). On WebAssembly they follow once the page's frame has shown; a
    /// prerendered page has them at once, as they are the license notice.
    /// </summary>
    private bool showAcknowledgements;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        if (InPrerenderSession)
        {
            showAcknowledgements = true;
            return;
        }

        // The page as it is, then the acknowledgements in a render of their own, after the browser had its turn.
        StateHasChanged();
        await Task.Yield();
        showAcknowledgements = true;
    }
}
