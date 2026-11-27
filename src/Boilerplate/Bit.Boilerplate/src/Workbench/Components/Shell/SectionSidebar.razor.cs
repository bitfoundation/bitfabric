namespace Boilerplate.Workbench.Components.Shell;

/// <summary>
/// The side panel of every section (pages, boards and tasks), the same in each: a search over the items' titles, the
/// line under them and what else they hold, then the items in groups with the open one marked, and the section's overview
/// first where it has one. The header's first button folds it away on a desktop and opens it over the page on a smaller
/// screen, where following a link closes it.
/// </summary>
public partial class SectionSidebar
{
    private string? searchText;
    private bool isSearchingContent;
    // The keys of the items whose content matched the search, from SearchContent; empty when it has none.
    private IReadOnlySet<string> contentMatches = new HashSet<string>();
    private CancellationTokenSource? contentSearch;

    /// <summary>The name of the panel as a landmark, like the section's name.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    [Parameter] public string? SearchPlaceholder { get; set; }

    /// <summary>The section's overview, listed first and marked while no item is open.</summary>
    [Parameter] public SectionListItem? Overview { get; set; }

    [Parameter] public IReadOnlyList<SectionListGroup> Groups { get; set; } = [];

    /// <summary>The key of the item that is open; none while the overview is.</summary>
    [Parameter] public string? CurrentKey { get; set; }

    /// <summary>
    /// Finds the items whose content holds a search beyond what <see cref="SectionListItem.SearchText"/> carries, such as a
    /// board's text, and returns their keys.
    /// </summary>
    [Parameter] public Func<string, CancellationToken, Task<IReadOnlySet<string>>>? SearchContent { get; set; }

    /// <summary>Raised when a link is followed.</summary>
    [Parameter] public EventCallback OnNavigate { get; set; }

    private bool IsSearching => string.IsNullOrWhiteSpace(searchText) is false;

    private IEnumerable<SectionListGroup> FilteredGroups => IsSearching is false
        ? Groups.Where(group => group.Items.Count > 0)
        : Groups.Select(group => group with { Items = [.. group.Items.Where(Matches)] }).Where(group => group.Items.Count > 0);

    private bool Matches(SectionListItem item)
    {
        var term = Normalize(searchText);

        return Normalize(item.Title).Contains(term, StringComparison.CurrentCultureIgnoreCase)
            || Normalize(item.Meta).Contains(term, StringComparison.CurrentCultureIgnoreCase)
            || Normalize(item.SearchText).Contains(term, StringComparison.CurrentCultureIgnoreCase)
            || contentMatches.Contains(item.Key);
    }

    /// <summary>
    /// Matches Persian typed with an Arabic keyboard and text with or without its zero-width non-joiners the same: Arabic
    /// yeh and kaf as Persian's, and the non-joiner left out.
    /// </summary>
    public static string Normalize(string? text) => (text ?? string.Empty).Trim()
        .Replace('ي', 'ی').Replace('ك', 'ک').Replace("‌", string.Empty, StringComparison.Ordinal);

    private async Task Search(string? text)
    {
        searchText = text;

        contentSearch?.Cancel();
        contentMatches = new HashSet<string>();

        if (SearchContent is null || IsSearching is false)
        {
            isSearchingContent = false;
            return;
        }

        var search = contentSearch = new CancellationTokenSource();
        isSearchingContent = true;
        StateHasChanged();

        try
        {
            var matches = await SearchContent(Normalize(text), search.Token);

            if (search.IsCancellationRequested is false)
            {
                contentMatches = matches;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (ReferenceEquals(search, contentSearch))
            {
                isSearchingContent = false;
            }
        }
    }

    private async Task Navigated()
    {
        WorkbenchStore.CloseSidePanel();

        await OnNavigate.InvokeAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        contentSearch?.Cancel();

        await base.DisposeAsync();
    }
}

/// <summary>A group of the side panel, under its title; a group without items isn't listed.</summary>
public sealed record SectionListGroup(string Title, IReadOnlyList<SectionListItem> Items);

/// <summary>
/// An item of the side panel: its title, written as it reads in its own direction, a short line under it, its icon and the
/// tone that colors the icon (primary, secondary, tertiary, info, success or warning), what else a search finds it by, and
/// a count it carries, such as what a task waits for.
/// </summary>
public sealed record SectionListItem(string Key, string Title, string? Meta, string IconName, string Href, string? Tone = null, string? SearchText = null, string? Badge = null);
