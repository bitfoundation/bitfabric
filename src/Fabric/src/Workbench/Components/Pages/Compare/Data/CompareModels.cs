using Bit.Fabric.Workbench.Components.Pages.HomeDirections;

namespace Bit.Fabric.Workbench.Components.Pages.Compare.Data;

/// <summary>
/// A page a fact rests on: its id in bitfabric-internal/.agents/gtm/research/competitors.md (such as LOV-3), the name a reader
/// knows it by, its addresses, and the date the page itself gives, when it gives one. Every source was read on
/// <see cref="CompareShared.ReadOn"/>.
/// </summary>
public sealed record CompareSource(string Id, string Name, string[] Urls, string? PageDate = null);

/// <summary>One side's answer in one row, with the ids of the sources it rests on; bit Fabric's own facts need none.</summary>
public sealed record CompareFact(string Text, string[] Sources, DirectionMark Mark = DirectionMark.None);

/// <summary>What a row of a comparison is about. Each subject answers the ones the research can back, and leaves out the rest.</summary>
public enum CompareTopic
{
    DoesWell,
    DayOne,
    Where,
    StopPaying,
    Platforms,
    Cost,
    Ai,
    Production,
    VendorStops,
    License,
    Updates
}

/// <summary>A row: its topic, then a fact per column, bit Fabric's last.</summary>
public sealed record CompareRow(CompareTopic Topic, CompareFact[] Cells);

/// <summary>"Choose … if": a heading and the situations it fits, each a short line.</summary>
public sealed record CompareChoice(string Title, string[] Items);

/// <summary>
/// Everything one comparison page says, built from the facts of its own class (See LovableComparison and the rest next to it):
/// correct a fact there and both design options, the hub's card and the page's list of sources follow.
/// </summary>
public sealed record CompareSubject(string Key, string Name)
{
    /// <summary>The h1 when the table leads.</summary>
    public string[] TableTitle { get; init; } = [];

    /// <summary>Who each is the better choice for, in two sentences, said before the table.</summary>
    public string Answer { get; init; } = string.Empty;

    /// <summary>The h1 when the choice leads.</summary>
    public string[] FitTitle { get; init; } = [];

    public string FitLead { get; init; } = string.Empty;

    /// <summary>The line on the hub's card that leads here: what this page leads with.</summary>
    public string CardLine { get; init; } = string.Empty;

    public string CardIcon { get; init; } = BitIconName.Table;

    /// <summary>The columns of the table, bit Fabric's last, with a smaller line under a header where one helps.</summary>
    public string[] Columns { get; init; } = [];

    public string?[]? ColumnNotes { get; init; }

    public CompareRow[] Rows { get; init; } = [];

    /// <summary>The line under the table that says where its facts come from.</summary>
    public string SourceLine { get; init; } = string.Empty;

    /// <summary>"Choose … if", one per column, of equal weight.</summary>
    public CompareChoice[] Choices { get; init; } = [];

    /// <summary>What bit Fabric doesn't have, said plainly.</summary>
    public DirectionTile[] Gaps { get; init; } = [];

    /// <summary>A band of its own after the facts, such as the lifecycle a starter kit doesn't sell; null for none.</summary>
    public (string Title, string Intro, DirectionTile[] Tiles)? Extra { get; init; }

    public DirectionQuestion[] Questions { get; init; } = [];

    /// <summary>Sources the page uses outside the table, such as the AI plans' prices in the cost band.</summary>
    public string[] MoreSources { get; init; } = [];

    /// <summary>
    /// The rows a table shows: every one, or all but what each does well where the page says that in its own columns.
    /// </summary>
    public CompareRow[] RowsShown(bool withStrengths) => [.. Rows.Where(row => withStrengths || row.Topic is not CompareTopic.DoesWell)];

    /// <summary>
    /// Every source the page cites, in the order its table first uses them, then the rest: the page numbers them so, and a
    /// source only a row the page leaves out cites isn't listed.
    /// </summary>
    public string[] SourceIds(bool withStrengths)
        => [.. RowsShown(withStrengths).SelectMany(row => row.Cells).SelectMany(cell => cell.Sources).Concat(MoreSources).Distinct()];
}
