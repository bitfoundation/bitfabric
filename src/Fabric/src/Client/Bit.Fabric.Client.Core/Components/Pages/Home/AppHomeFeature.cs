namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// One thing every project comes with, as a card: its name, a line on it, and what its info button shows: the film found by
/// its id in AppHomePage.razor.Films.cs, or its <paramref name="Note"/> where a film would add nothing. One whose card says it
/// all is not <paramref name="Explained"/>: it has no info button, the apps on every platform among them.
/// One with a <paramref name="Url"/> opens it in a new tab instead, such as a component's live demo (2026-11-11). One with
/// a <paramref name="More"/> keeps its film, and leads to that page, where the feature can be seen live, from the card and
/// from the film's end, both in the prerendered page too (2026-11-26).
/// </summary>
public sealed record AppHomeFeature(string Id, string Icon, string Title, string Text, string? Badge = null, AppHomeNote? Note = null, bool Explained = true, string? Url = null, string? More = null);

/// <summary>A feature's few lines: what it gives first, the facts as a short list, and a line after them.</summary>
public sealed record AppHomeNote(string Lead, (string Term, string Detail)[] Facts, string? After = null);

/// <summary>A real app to open, built the way the visitor's will be: its name, a line on it, and where each platform's version is.</summary>
public sealed record AppHomeTry(string Name, string Text, (string Platform, string? Url)[] Links);

/// <summary>A band of the page: what it is about, a line under its heading, its cards, a link to read more, and a showcase among its cards.</summary>
public sealed record AppHomeSection(string Id, string Title, string Intro, AppHomeFeature[] Features, string? LinkText = null, string? LinkUrl = null, AppHomeShowcase? Showcase = null, string? Note = null);

/// <summary>
/// More cards behind an accordion, right after the band's card <paramref name="After"/>: the components the visitor's AI puts in
/// place instead of writing them (2026-11-11). A line comes before the cards, whose info buttons open their live demos.
/// </summary>
public sealed record AppHomeShowcase(string After, string Title, string Intro, AppHomeFeature[] Features);

/// <summary>How one of the compared fares on a row: well, partly or badly.</summary>
public enum AppHomeMark { Good, Partly, Bad }

/// <summary>
/// A row of the comparison: what it is about, then a hosted AI builder, the visitor's AI on an empty
/// project and bit Fabric, in that order.
/// </summary>
public sealed record AppHomeComparisonRow(string Topic, (AppHomeMark Mark, string Text)[] Cells);
