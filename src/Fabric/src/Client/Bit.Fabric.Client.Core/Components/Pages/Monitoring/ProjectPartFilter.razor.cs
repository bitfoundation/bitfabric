namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// The chip of a tab shown on one part of the app (See ProjectTabLink): its name, and a close that shows every part again.
/// </summary>
public partial class ProjectPartFilter
{
    /// <summary>What the app calls the part.</summary>
    [Parameter, EditorRequired] public string Title { get; set; } = "";

    [Parameter] public EventCallback OnClear { get; set; }
}
