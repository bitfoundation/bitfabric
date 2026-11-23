using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>One question of the new project page: its heading, a line on why it's asked, a "Help me choose" link to its help topic, and its controls.</summary>
public partial class NewProjectQuestion
{
    private readonly string headingId = $"np-q-{Guid.NewGuid():N}";

    [Parameter, EditorRequired] public string Title { get; set; } = default!;

    [Parameter] public string? Lead { get; set; }

    /// <summary>The help topic the "Help me choose" link opens, from <see cref="NewProjectHelpTopics"/>.</summary>
    [Parameter] public string? Topic { get; set; }

    [Parameter] public string HeadingElement { get; set; } = "h2";

    [Parameter] public RenderFragment? ChildContent { get; set; }
}
