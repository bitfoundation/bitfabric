namespace Bit.Fabric.Client.Films.Components;

/// <summary>A film's person, drawn as a face in a circle: over a device to say whose screen it is, or in a row.</summary>
public partial class AppHomeFace
{
    [Parameter, EditorRequired] public AppHomePerson Person { get; set; }
}
