namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

public partial class DirectionLists
{
    [Parameter, EditorRequired] public DirectionList[] Lists { get; set; } = [];

    /// <summary>The narrowest a card gets before the row wraps: wider keeps four lists two by two.</summary>
    [Parameter] public string MinWidth { get; set; } = "18rem";

    /// <summary>A larger title and lines, for lists that carry a page's main point, such as who should choose what.</summary>
    [Parameter] public bool IsLarge { get; set; }

    private static string IconOf(DirectionMark mark) => mark switch
    {
        DirectionMark.Good => BitIconName.CheckMark,
        DirectionMark.Partly or DirectionMark.Info => BitIconName.Info,
        DirectionMark.Bad => BitIconName.Cancel,
        _ => BitIconName.CheckMark
    };

    private static BitColor? ColorOf(DirectionMark mark) => mark switch
    {
        DirectionMark.Good => BitColor.Success,
        DirectionMark.Partly or DirectionMark.Info => BitColor.SecondaryForeground,
        DirectionMark.Bad => BitColor.SevereWarning,
        _ => null
    };
}
