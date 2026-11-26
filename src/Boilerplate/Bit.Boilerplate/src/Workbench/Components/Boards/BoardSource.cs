namespace Boilerplate.Workbench.Components.Boards;

/// <summary>Where a board's facts come from: a page with its address and the day it was read, or a file of the project.</summary>
public sealed record BoardSource(string Title, string? Url = null, string? ReadOn = null);
