using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>A real product or a quick try (Q31): PostgreSQL, or SQLite with the sample pages.</summary>
public partial class NewProjectGoalQuestion
{
    [Parameter] public string HeadingElement { get; set; } = "h2";
}
