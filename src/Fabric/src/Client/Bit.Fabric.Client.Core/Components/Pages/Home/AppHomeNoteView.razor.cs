namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// What a feature's info button shows when a film would add nothing, as for the database and the computers to build on: a
/// few lines, with the facts as a short list.
/// </summary>
public partial class AppHomeNoteView
{
    [Parameter, EditorRequired] public string Title { get; set; } = default!;

    [Parameter, EditorRequired] public AppHomeNote Note { get; set; } = default!;

    [Parameter] public EventCallback OnClose { get; set; }
}
