namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

/// <summary>
/// One fact as a tile: an icon, a short title and one line. A <paramref name="Placeholder"/> marks a value nobody knows yet,
/// shown as such on the page, and a <paramref name="FilmId"/> plays that film of the app's (See AppHomeFilms).
/// </summary>
public sealed record DirectionTile(string Icon, string Title, string Text, string? Placeholder = null, string? FilmId = null);

/// <summary>How a cell of a table fares: well, partly, badly, a condition to know about, or a plain fact with no mark.</summary>
public enum DirectionMark { None, Good, Partly, Bad, Info }

/// <summary>A cell's mark and text, with the sources it rests on when the page lists them (See DirectionRef).</summary>
public sealed record DirectionCell(DirectionMark Mark, string Text, DirectionRef[]? Refs = null);

/// <summary>A source a cell rests on: its number in the page's list of sources, and the id of that entry in the page.</summary>
public sealed record DirectionRef(int Number, string Anchor);

/// <summary>A row of a table: what it is about, then a cell per column.</summary>
public sealed record DirectionRow(string Topic, DirectionCell[] Cells);

/// <summary>A term and what it means, short enough to be quoted alone: a key fact or a glossary entry.</summary>
public sealed record DirectionFact(string Term, string Detail, string? Placeholder = null);

public sealed record DirectionQuestion(string Question, string Answer);

/// <summary>A card with a list under its title, each line with the same mark, such as what keeps running or who it isn't for.</summary>
public sealed record DirectionList(string Title, DirectionMark Mark, string[] Items, string? After = null);

/// <summary>Where a direction's hero puts its words: centred over the page, or at the start with a picture beside them.</summary>
public enum DirectionHeroLayout { Centered, Split }
