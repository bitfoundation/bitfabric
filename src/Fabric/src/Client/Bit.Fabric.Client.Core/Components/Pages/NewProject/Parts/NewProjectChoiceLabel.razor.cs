using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>
/// The label of a choice card: an icon, the choice with its badge, and a line on what it means under it. The choice groups
/// pass their option's own Text, Suffix, Description and IconName in, so the cards stay a native radio group.
/// </summary>
public partial class NewProjectChoiceLabel
{
    [Parameter] public string? IconName { get; set; }

    [Parameter] public string? Text { get; set; }

    [Parameter] public string? Badge { get; set; }

    [Parameter] public string? Description { get; set; }
}
