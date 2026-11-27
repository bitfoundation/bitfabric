namespace Boilerplate.Workbench.Components.Shell;

/// <summary>
/// The Pages section's side panel (<see cref="SectionSidebar"/>): the pages by category, found by their titles, descriptions
/// and the components they show off. Its links carry the current views' query, so the next page opens on the same devices
/// and settings.
/// </summary>
public partial class PageNavigator
{
    [Parameter] public string? CurrentKey { get; set; }

    /// <summary>The current views' query, which the link to the current page carries as it is.</summary>
    [Parameter] public string? Query { get; set; }

    /// <summary>Raised when a link is followed.</summary>
    [Parameter] public EventCallback OnNavigate { get; set; }

    private List<SectionListGroup> Groups => [.. WorkbenchPageCatalog.Pages.GroupBy(page => page.CategoryKey).Select(group => new SectionListGroup(
        Localizer[group.Key],
        [.. group.Select(page => new SectionListItem(page.Key,
                                                     Localizer[page.TitleKey],
                                                     Localizer[page.DescriptionKey],
                                                     page.IconName,
                                                     $"{WorkbenchUrls.Page(page.Key)}{GetQuery(page)}",
                                                     SearchText: string.Join(' ', page.Showcases)))]))];

    /// <summary>
    /// The query a page link carries: the current one for the current page, and for another page the same views
    /// without the current page's design option and switches.
    /// </summary>
    private string? GetQuery(WorkbenchPageInfo page) => page.Key == CurrentKey ? Query : WorkbenchStore.OtherPageQueryString;
}
