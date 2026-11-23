namespace Bit.Fabric.Client.Diagrams.Components;

/// <summary>Green for what is all good, red for what is stopped or failed, and nothing else gets a color.</summary>
public enum AppHomePictureTone { Neutral, Good, Bad }

public enum AppHomePictureKind { Card, Bar, Room, Wall, Chip }

/// <summary>Where a box sits, in the picture's own units. A box inside a room is placed relative to the room.</summary>
public readonly record struct AppHomePictureArea(double X, double Y, double Width, double Height);

/// <summary>One box of a picture, with its place on a wide screen and on a phone.</summary>
public sealed record AppHomePictureBox(string Id, string? Icon, string Title, string? Text, AppHomePictureArea Wide, AppHomePictureArea Narrow)
{
    public AppHomePictureTone Tone { get; init; }

    public AppHomePictureKind Kind { get; init; }

    /// <summary>The room this box is drawn in, which has to come earlier in the picture's boxes.</summary>
    public string? ParentId { get; init; }

    /// <summary>The box's turn in the light that walks through the picture, if it has one.</summary>
    public int? Step { get; init; }
}

/// <summary>A line between two boxes. Each side is one of l, r, t and b, and the picture mirrors them in a right-to-left language.</summary>
public sealed record AppHomePictureLine(string From, string To)
{
    public AppHomePictureTone Tone { get; init; }

    /// <summary>The line marches, showing that something travels along it.</summary>
    public bool Flows { get; init; }

    /// <summary>The line is a way that doesn't exist.</summary>
    public bool Dashed { get; init; }

    public string? Label { get; init; }

    public string WideFrom { get; init; } = "r";

    public string WideTo { get; init; } = "l";

    public string NarrowFrom { get; init; } = "b";

    public string NarrowTo { get; init; } = "t";
}

public sealed record AppHomePicture(string Id, string AriaLabel, double WideHeight, double NarrowHeight, AppHomePictureBox[] Boxes, AppHomePictureLine[] Lines);
