using System.Text.RegularExpressions;

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
    private string? readMarkdown;
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

    protected override void OnParametersSet()
    {
        if (Markdown == readMarkdown)
            return;

        readMarkdown = Markdown;
        var tasks = TaskItem().Matches(Markdown);
        taskCount = tasks.Count;
        doneCount = tasks.Count(task => task.Groups[1].Value is not " ");
    }

    private void Open() => isOpen = true;

    private void ReadOutline(BitMarkdownDocumentNode document)
    {
        var headings = BitMarkdownAstHelper.Descendants(document).OfType<BitMarkdownHeadingNode>().ToArray();

        // The plan's own title is its first level-one heading, as it renders: an emoji's shortcode as the emoji.
        title = headings.FirstOrDefault(heading => heading.Level is 1) is { } first ? BitMarkdownInlineHelpers.PlainText(first.Inlines) : null;
        outline = [.. headings.Where(heading => heading.Level is 2 or 3 && string.IsNullOrEmpty(heading.Id) is false)
                              .Select(heading => new OutlineEntry(heading.Level, heading.Id!, BitMarkdownInlineHelpers.PlainText(heading.Inlines)))];
    }

    private sealed record OutlineEntry(int Level, string Id, string Text);

    // A task list's item: "- [ ]" or "- [x]", as GitHub ticks them.
    [GeneratedRegex(@"^\s*[-*+]\s+\[([ xX])\]\s", RegexOptions.Multiline)]
    private static partial Regex TaskItem();
}
