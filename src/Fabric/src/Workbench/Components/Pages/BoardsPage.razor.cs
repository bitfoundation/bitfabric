namespace Bit.Fabric.Workbench.Components.Pages;

/// <summary>
/// The boards: what an agent shows the user as a page instead of telling it in the chat (the boards skill,
/// .github/agents/boards.agent.md). /boards lists them, newest first, and /boards/{id} opens one full width, in the
/// Workbench's own theme.
/// </summary>
public partial class BoardsPage
{
    [AutoInject] private BitThemeManager bitThemeManager = default!;

    [Parameter] public string? BoardId { get; set; }

    private WorkbenchBoard? Board => BoardId is null ? null : WorkbenchBoardCatalog.Find(BoardId);

    private async Task ToggleTheme()
    {
        await bitThemeManager.ToggleDarkLightAsync();
    }
}
