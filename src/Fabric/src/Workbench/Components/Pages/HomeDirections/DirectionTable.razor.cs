namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

public partial class DirectionTable
{
    /// <summary>The id of the band title that names the table.</summary>
    [Parameter, EditorRequired] public string LabelledBy { get; set; } = default!;

    /// <summary>The header of the rows' own column; none leaves that corner empty.</summary>
    [Parameter] public string? TopicHeader { get; set; }

    [Parameter, EditorRequired] public string[] Columns { get; set; } = [];

    /// <summary>A smaller line under each column's header, such as the names a column stands for.</summary>
    [Parameter] public string?[]? ColumnNotes { get; set; }

    [Parameter, EditorRequired] public DirectionRow[] Rows { get; set; } = [];

    /// <summary>The column drawn on the second background, bit Fabric's; -1 for none.</summary>
    [Parameter] public int Highlight { get; set; } = -1;

    /// <summary>How wide the rows' own column is; a table with one column of answers has room for its topics on one line.</summary>
    [Parameter] public string TopicWidth { get; set; } = "12rem";

    /// <summary>Text a size larger and more room around it, for a table with few columns that carries a page.</summary>
    [Parameter] public bool IsLarge { get; set; }

    /// <summary>
    /// An entry of the page, as the path of the page with the entry after the #: a bare #entry would lead to the site's root,
    /// as the app's base address is / (See DirectionComponentBase.InPage).
    /// </summary>
    private string InPage(string anchor) => $"{new Uri(NavigationManager.Uri).PathAndQuery}#{anchor}";

    private static string IconOf(DirectionMark mark) => mark switch
    {
        DirectionMark.Good => BitIconName.CheckMark,
        DirectionMark.Partly => BitIconName.Warning,
        DirectionMark.Info => BitIconName.Info,
        _ => BitIconName.Cancel
    };

    private static BitColor ColorOf(DirectionMark mark) => mark switch
    {
        DirectionMark.Good => BitColor.Success,
        DirectionMark.Partly => BitColor.SevereWarning,
        DirectionMark.Info => BitColor.SecondaryForeground,
        _ => BitColor.Error
    };
}
