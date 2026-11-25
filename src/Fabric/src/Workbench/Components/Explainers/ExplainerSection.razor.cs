namespace Bit.Fabric.Workbench.Components.Explainers;

/// <summary>One section of an explainer: its heading, an optional line under it, and its content.</summary>
public partial class ExplainerSection
{
    /// <summary>The section's element id, so a link can point at it with #id.</summary>
    [Parameter, EditorRequired] public string Id { get; set; } = default!;

    [Parameter, EditorRequired] public string Title { get; set; } = default!;

    /// <summary>A sentence or two under the heading: what the section answers.</summary>
    [Parameter] public string? Intro { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private string TitleId => $"{Id}-title";
}
