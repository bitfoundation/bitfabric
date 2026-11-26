namespace Boilerplate.Workbench.Components.Pages;

/// <summary>
/// The boards: what an agent shows the user as a page instead of telling it in the chat (the boards skill,
/// .github/agents/boards.agent.md). /boards lists them by kind, newest first, and /boards/{id} opens one, in the Workbench's
/// own theme, with the others listed at its side to switch to; a phone folds that list away behind a link back to /boards.
/// </summary>
public partial class BoardsPage
{
    [Parameter] public string? BoardId { get; set; }

    private WorkbenchBoard? Board => BoardId is null ? null : WorkbenchBoardCatalog.Find(BoardId);

    private string BackIcon => CurrentDir is BitDir.Rtl ? BitIconName.Forward : BitIconName.Back;

    private List<SectionListGroup> SideGroups => [.. WorkbenchBoardCatalog.Groups.Select(group => new SectionListGroup(
        Localizer[WorkbenchBoardCatalog.TitleKeyOf(group.Kind)],
        [.. group.Boards.Select(board => new SectionListItem(board.Id,
                                                            Localizer[board.TitleKey],
                                                            board.Updated.ToString("d MMM yyyy", CultureInfo.CurrentUICulture),
                                                            board.IconName,
                                                            WorkbenchUrls.Board(board.Id),
                                                            board.Tone))]))];
}
