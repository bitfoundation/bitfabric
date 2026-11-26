namespace Bit.Fabric.Workbench.Components.Boards;

/// <summary>
/// One idea of a board: a short heading, at most one line under it, and the visual that carries it, which slides in as it
/// scrolls into view. A board is a handful of these, never a document.
/// </summary>
public partial class BoardBlock
{
    /// <summary>The section's id, which its address can point at, such as #fees.</summary>
    [Parameter] public string? Id { get; set; }

    [Parameter] public string? Title { get; set; }

    /// <summary>One line under the heading, when the visual needs it.</summary>
    [Parameter] public string? Kicker { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }
}
