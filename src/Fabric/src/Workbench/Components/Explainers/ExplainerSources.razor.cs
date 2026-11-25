namespace Bit.Fabric.Workbench.Components.Explainers;

/// <summary>
/// Where an explainer's facts come from, at its end: each a link, or a file of the repository, with a note such as the day
/// it was read. A fact the reader may act on, like a price or a rule, names its source here.
/// </summary>
public partial class ExplainerSources
{
    [Parameter, EditorRequired] public IReadOnlyList<ExplainerSource> Items { get; set; } = [];

    /// <summary>The heading, in the explainer's language.</summary>
    [Parameter] public string Title { get; set; } = "Sources";

    /// <summary>A line under the heading, such as when the sources were read.</summary>
    [Parameter] public string? Intro { get; set; }
}
