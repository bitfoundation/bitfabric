using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>What the visitor will get, in plain words, and what the setup takes: the download, the time and the disk space.</summary>
public partial class NewProjectSummary
{
    /// <summary>False shows only the estimate, for a bar too small for the list.</summary>
    [Parameter] public bool ShowsList { get; set; } = true;
}
