namespace Boilerplate.Workbench.Components.Boards;

/// <summary>A big number and what it counts, in a few words.</summary>
public sealed record BoardNumber(string Value, string Label);

/// <summary>A board's headline numbers, big and in its tone, two to a row; usually beside its title (BoardFrame.Aside).</summary>
public partial class BoardNumbers
{
    [Parameter, EditorRequired] public IReadOnlyList<BoardNumber> Items { get; set; } = [];
}
