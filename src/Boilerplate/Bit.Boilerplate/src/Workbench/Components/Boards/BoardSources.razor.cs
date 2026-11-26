namespace Boilerplate.Workbench.Components.Boards;

/// <summary>Where a board's facts come from, folded under one line at its foot, each with the day it was read.</summary>
public partial class BoardSources
{
    [Parameter, EditorRequired] public BoardSource[] Items { get; set; } = [];

    [Parameter] public string Title { get; set; } = "Sources";
}
