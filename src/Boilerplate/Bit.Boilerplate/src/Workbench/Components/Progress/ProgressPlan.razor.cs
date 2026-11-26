namespace Boilerplate.Workbench.Components.Progress;

/// <summary>
/// The plan a task follows, like a change plan with its checklist of tasks: a preview on the task, how many of its tasks are
/// ticked, and the whole of it read full screen with its outline beside it.
/// </summary>
public partial class ProgressPlan
{
    // A plan longer than this many lines shows its start, with a button to show the rest.
    private const int PreviewLines = 24;

    private static readonly BitMarkdownPipeline pipeline = new BitMarkdownPipelineBuilder().UseAutoIdentifiers(anchorLinks: false)
                                                                                          .UseAdvanced()
                                                                                          .Build();

    private bool isOpen;
    private bool isExpanded;
    private int taskCount;
    private int doneCount;
    private string? title;
    private List<OutlineEntry> outline = [];

    [Parameter, EditorRequired] public string Markdown { get; set; } = default!;

    /// <summary>The way the plan's task reads (see WorkbenchTextDirection).</summary>
    [Parameter] public string TaskDirection { get; set; } = WorkbenchTextDirection.LeftToRight;

    /// <summary>The language the task is written in, like <c>fa</c>.</summary>
    [Parameter] public string? Language { get; set; }

    private bool IsLong => Markdown.AsSpan().Count('\n') > PreviewLines;

    private string Direction => WorkbenchTextDirection.Of(Markdown, TaskDirection);

    private BitDir BitDirection => WorkbenchTextDirection.ToBitDir(Direction);

    private double Percent => taskCount is 0 ? 0 : 100.0 * doneCount / taskCount;

    private string TasksDoneText => Localizer[nameof(WorkbenchStrings.ProgressPlanTasksDone), doneCount, taskCount];

    private void Open() => isOpen = true;

    /// <summary>The plan's title, outline and task list, as the viewer parsed it, so a box in a code example isn't counted.</summary>
    private void ReadDocument(BitMarkdownDocumentNode document)
    {
        var nodes = BitMarkdownAstHelper.Descendants(document).ToArray();
        var tasks = nodes.OfType<BitMarkdownTaskCheckboxNode>().ToArray();
        var headings = nodes.OfType<BitMarkdownHeadingNode>().ToArray();

        taskCount = tasks.Length;
        doneCount = tasks.Count(task => task.Checked);

        // The plan's own title is its first level-one heading, as it renders: an emoji's shortcode as the emoji.
        title = headings.FirstOrDefault(heading => heading.Level is 1) is { } first ? BitMarkdownInlineHelpers.PlainText(first.Inlines) : null;
        outline = [.. headings.Where(heading => heading.Level is 2 or 3 && string.IsNullOrEmpty(heading.Id) is false)
                              .Select(heading => new OutlineEntry(heading.Level, heading.Id!, BitMarkdownInlineHelpers.PlainText(heading.Inlines)))];
    }

    private sealed record OutlineEntry(int Level, string Id, string Text);

}
