using System.Globalization;

namespace Boilerplate.Workbench.Components.Boards;

/// <summary>
/// The frame of a board (WorkbenchBoardCatalog): its hero, with an icon, a title and one line under it, a date and a note as
/// small chips, then its blocks (BoardBlock), a Submit bar on a board that asks its reader to pick, and its sources, folded away. A board an agent writes is in one language, the one
/// it was asked in, and reads in it whatever language the Workbench is in; one whose words are in the Workbench's
/// translations reads in the Workbench's. See the boards skill (.github/agents/boards.agent.md).
/// </summary>
public partial class BoardFrame
{
    private BitDir? dir;

    [Parameter, EditorRequired] public string Title { get; set; } = default!;

    /// <summary>One line under the title. A board shows rather than tells, so it stays one line.</summary>
    [Parameter] public string? Lead { get; set; }

    [Parameter] public string IconName { get; set; } = BitIconName.Lightbulb;

    /// <summary>The theme color its hero is drawn in: primary, secondary, tertiary, info, success or warning.</summary>
    [Parameter] public string Tone { get; set; } = "primary";

    /// <summary>When its facts were read, such as "Read on 25 November 2026".</summary>
    [Parameter] public string? Meta { get; set; }

    /// <summary>A short caveat, shown as a chip under the title.</summary>
    [Parameter] public string? Note { get; set; }

    /// <summary>
    /// The language the board is written in, a culture name such as en or fa; none for a board whose words come from the
    /// Workbench's translations, which reads in the Workbench's language.
    /// </summary>
    [Parameter] public string? Language { get; set; }

    private string BoardLanguage => Language ?? CultureInfo.CurrentUICulture.Name;

    /// <summary>What sits beside the title on a wide screen, such as a few big numbers.</summary>
    [Parameter] public RenderFragment? Aside { get; set; }

    [Parameter] public BoardSource[]? Sources { get; set; }

    /// <summary>
    /// Whether the board asks its reader to pick, like options to tick: a Submit bar stays at the foot of the screen, and what
    /// they pick, kept in the board's store (named board-{id}), is in the board's file for the agent as they go and is kept
    /// there as final when they submit (BoardSubmit, WorkbenchBoardFiles).
    /// </summary>
    [Parameter] public bool Picks { get; set; }

    /// <summary>
    /// The id of the Progress task whose agent waits for the picks: Submit tells it at once, which wakes its wait on the task.
    /// Implies <see cref="Picks"/>.
    /// </summary>
    [Parameter] public string? ProgressTask { get; set; }

    [Parameter] public string SourcesTitle { get; set; } = "Sources";

    /// <summary>The board's blocks (BoardBlock).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void OnParametersSet()
    {
        dir = IsRightToLeft(BoardLanguage) ? BitDir.Rtl : BitDir.Ltr;

        base.OnParametersSet();
    }

    private bool IsPersian => BoardLanguage.StartsWith("fa", StringComparison.OrdinalIgnoreCase);

    private static bool IsRightToLeft(string language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(language).TextInfo.IsRightToLeft;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
