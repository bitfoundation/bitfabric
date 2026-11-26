namespace Bit.Fabric.Client.Diagrams.Components;

/// <summary>How a box of the application map is doing; its colour and its icon follow it.</summary>
public enum ProjectMapTone { Healthy, Degraded, Failing, NotConfigured, Neutral }

/// <summary>
/// One thing the app talks to, ready to draw: what the app calls it, the report's icon, one short measure and how it's doing.
/// <paramref name="Flag"/> is a word on its corner, such as an ongoing incident; <paramref name="Behind"/> is what the app's
/// report says lies behind it, such as "Twilio", drawn under its name.
/// </summary>
public sealed record ProjectMapPart(string Key, string Title, string? Icon, string? Measure, ProjectMapTone Tone, string? Flag, string? Behind = null);

/// <summary>A category of the report, such as Messaging, with its parts, worst first; its line to the app takes its worst tone.</summary>
public sealed record ProjectMapGroup(string Name, string Title, string Icon, ProjectMapTone Tone, ProjectMapPart[] Parts);

/// <summary>
/// The application map of a project, worded by the page that shows it (Client.Core's ProjectMap), so this library only lays
/// it out and draws it: the app in the middle, the categories of what it talks to around it.
/// </summary>
public sealed record ProjectMapPicture(string AppTitle, string? AppSubtitle, string? AppMeasure, ProjectMapTone AppTone, ProjectMapGroup[] Groups, string AriaLabel);
