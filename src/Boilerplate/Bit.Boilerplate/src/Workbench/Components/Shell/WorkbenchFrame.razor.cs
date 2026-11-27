namespace Boilerplate.Workbench.Components.Shell;

/// <summary>
/// What the sections with a side panel are laid out in, so they read as one: the one header (<see cref="ShellHeader"/>), the
/// section's side panel (<see cref="SectionSidebar"/>) at the start, which folds away, and the section's own content, which
/// scrolls on its own under the header. A page's own view lays out the same header and side panel around its views itself.
/// </summary>
public partial class WorkbenchFrame
{
    [Parameter] public WorkbenchSection Section { get; set; }

    /// <summary>The open item's title in the header's breadcrumb.</summary>
    [Parameter] public string? Title { get; set; }

    [Parameter] public RenderFragment? TitleExtra { get; set; }

    /// <summary>The section's own buttons in the header.</summary>
    [Parameter] public RenderFragment? Actions { get; set; }

    /// <summary>The section's side panel, a <see cref="SectionSidebar"/>.</summary>
    [Parameter] public RenderFragment? SidePanel { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public string? Class { get; set; }

    /// <summary>A class for the area the content scrolls in.</summary>
    [Parameter] public string? MainClass { get; set; }

    protected override void OnInitialized()
    {
        Subscribe(WorkbenchStore);
    }
}
