using Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

/// <summary>
/// A page a fact rests on: its id in the go-to-market research (bitfabric-internal/.agents/gtm/research/competitors.md, its
/// Sources table, such as LOV-3), the name a reader knows it by and its addresses. Every source was read on 19 November 2026
/// (<see cref="CompareShared.CheckedOn"/>).
/// </summary>
public sealed record CompareSource(string Id, string Name, string[] Urls);

/// <summary>One side's answer in one row, with the ids of the sources it rests on; bit Fabric's own facts need none.</summary>
public sealed record CompareFact(string Text, string[] Sources);

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
    GoingLive,
    Security,
    Leaving,
    Terms,
    Updates
}

/// <summary>A row: its topic, then a fact per column, bit Fabric's last.</summary>
public sealed record CompareRow(CompareTopic Topic, CompareFact[] Cells);

/// <summary>A question the page answers, with the sources its answer rests on, if any.</summary>
public sealed record CompareQuestion(string Question, string Answer, string[] Sources);

/// <summary>A column of the table: its name, with a smaller line under it where one helps, such as the products it stands for.</summary>
public sealed record CompareColumn(string Name, string? Note = null);

/// <summary>
/// Everything one comparison page says, built from the facts of its own class (See LovableComparison and the others next to
/// it): correct a fact there, and the table, the hub's card, the JSON-LD and the page's list of sources follow.
/// </summary>
public sealed record CompareSubject(string Url, string Name)
{
    /// <summary>The page's title in the browser and in search results.</summary>
    public string PageTitle { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>The page's one h1.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Who each is the better choice for, in two sentences, said before the table.</summary>
    public string Answer { get; init; } = string.Empty;

    /// <summary>The line on the hub's card that leads here.</summary>
    public string CardLine { get; init; } = string.Empty;

    public string CardIcon { get; init; } = BitIconName.Table;

    /// <summary>Whether the page leads to /for, where each agent is set up: the pages about the visitor's own agent.</summary>
    public bool LeadsToAgents { get; init; }

    /// <summary>The other product's own address, for the JSON-LD; null for what isn't one product, such as an empty project.</summary>
    public string? ProductUrl { get; init; }

    /// <summary>The title of the band that holds the table.</summary>
    public string TableTitle { get; init; } = string.Empty;

    /// <summary>The columns, bit Fabric's last.</summary>
    public CompareColumn[] Columns { get; init; } = [];

    public CompareRow[] Rows { get; init; } = [];

    /// <summary>The line under the table that says where its facts come from.</summary>
    public string SourceLine { get; init; } = string.Empty;

    /// <summary>How bit Fabric is built on purpose: each a choice, and what it gives the visitor.</summary>
    public AppHomeTile[] Gaps { get; init; } = [];

    /// <summary>A band of its own after the table, such as the way to a live app a starter kit doesn't sell; null for none.</summary>
    public (string Title, string Intro, AppHomeTile[] Tiles)? Extra { get; init; }

    public CompareQuestion[] Questions { get; init; } = [];

    /// <summary>The sources of the cost band, which every page shows.</summary>
    public string[] CostSources { get; init; } = [];

    /// <summary>
    /// Every source the page cites, in the order the page first cites them: the table's, then the cost band's, then the
    /// questions'. The page numbers them so.
    /// </summary>
    public string[] SourceIds =>
    [
        .. Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Sources)
            .Concat(CostSources)
            .Concat(Questions.SelectMany(question => question.Sources))
            .Distinct()
    ];
}
