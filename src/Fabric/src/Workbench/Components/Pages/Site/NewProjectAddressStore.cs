namespace Bit.Fabric.Workbench.Components.Pages.Site;

/// <summary>
/// The new project wizard's part of its address, as the app's page writes it: its answers and its step, saved per view and
/// handed to every screenshot, as the page keeps nothing of its own anywhere else (See NewProjectHost).
/// </summary>
[BlexStore(Name = StoreName, Persist = true)]
public partial class NewProjectAddressStore
{
    public const string StoreName = "new-project-address";

    /// <summary>The page's query, without its question mark; null while it holds nothing.</summary>
    [BlexState] private string? _query;

    [BlexAction] private void OnKeep(string? query) => Query = query;
}
