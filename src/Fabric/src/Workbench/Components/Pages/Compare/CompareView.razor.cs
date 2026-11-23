using Bit.Fabric.Workbench.Components.Pages.Compare.Data;
using Bit.Fabric.Workbench.Components.Pages.HomeDirections;

namespace Bit.Fabric.Workbench.Components.Pages.Compare;

public partial class CompareView
{
    [Parameter, EditorRequired] public CompareSubject Subject { get; set; } = default!;

    /// <summary>The fit option: who should choose which leads, and the facts follow as a compact table.</summary>
    [Parameter] public bool IsFit { get; set; }

    /// <summary>The comparisons the hub's cards lead to.</summary>
    [Parameter] public CompareSubject[] Subjects { get; set; } = [];

    /// <summary>Opens another comparison, or the hub, by its key.</summary>
    [Parameter] public EventCallback<string> OnOpen { get; set; }

    private bool IsHub => Subject.Key == HubComparison.Key;

    /// <summary>The fit option says what each does well in its own columns, so its table leaves that row out.</summary>
    private bool WithStrengths => IsFit is false;

    private string[] SourceIds => Subject.SourceIds(WithStrengths);

    private string TableTitle => (IsFit, IsHub) switch
    {
        (true, _) => Localizer["The facts, side by side"],
        (false, true) => Localizer["The three ways, and bit Fabric"],
        _ => Localizer["Side by side"]
    };

    // The hub's four columns leave its topics a narrower column than a page with one other column next to bit Fabric's
    private string TopicWidth => IsHub ? "10rem" : "14rem";

    /// <summary>Who should choose which, each list of equal weight and the same mark.</summary>
    private DirectionList[] ChoiceLists => [.. Subject.Choices.Select(choice => new DirectionList(choice.Title, DirectionMark.Good, choice.Items))];

    /// <summary>
    /// The rows as the table draws them, each fact with the numbers of its sources in the page's list. What each does well is
    /// marked on every side alike, and nothing else is marked, so no column is judged by an icon.
    /// </summary>
    private DirectionRow[] TableRows =>
    [
        .. Subject.RowsShown(WithStrengths).Select(row => new DirectionRow(CompareShared.TopicLabel(Localizer, row.Topic),
                                                                           [.. row.Cells.Select(fact => CellOf(row.Topic, fact))]))
    ];

    private DirectionCell CellOf(CompareTopic topic, CompareFact fact)
    {
        var ids = SourceIds;

        return new(topic is CompareTopic.DoesWell ? DirectionMark.Good : fact.Mark,
                   fact.Text,
                   [.. fact.Sources.Select(id => new DirectionRef(Array.IndexOf(ids, id) + 1, ComparePage.AnchorOf(id))).OrderBy(source => source.Number)]);
    }
}
