namespace Boilerplate.Workbench.Components.Pages;

/// <summary>
/// The boards: what an agent shows the user as a page instead of telling it in the chat (the boards skill,
/// .github/agents/boards.agent.md). /boards opens the section's own board, which says what boards are and lists them by
/// kind, and /boards/{id} opens one, each with every board in the side panel, whose search reads what the boards say too.
/// </summary>
public partial class BoardsPage
{
    [AutoInject] private IServiceProvider services = default!;

    [Parameter] public string? BoardId { get; set; }

    private WorkbenchBoard? Board => BoardId is null ? null : WorkbenchBoardCatalog.Find(BoardId);

    private SectionListItem Overview => new(string.Empty, Localizer[nameof(WorkbenchStrings.AllBoards)], null, WorkbenchSections.IconOf(WorkbenchSection.Boards), WorkbenchUrls.Boards);

    private List<SectionListGroup> SideGroups => [.. WorkbenchBoardCatalog.Groups.Select(group => new SectionListGroup(
        Localizer[WorkbenchBoardCatalog.TitleKeyOf(group.Kind)],
        [.. group.Boards.Select(board => new SectionListItem(board.Id,
                                                            Localizer[board.TitleKey],
                                                            Localizer[board.SummaryKey],
                                                            board.IconName,
                                                            WorkbenchUrls.Board(board.Id),
                                                            board.Tone))]))];

    private Task<IReadOnlySet<string>> SearchContent(string term, CancellationToken cancellationToken) => WorkbenchBoardText.FindAsync(services, term, cancellationToken);
}
