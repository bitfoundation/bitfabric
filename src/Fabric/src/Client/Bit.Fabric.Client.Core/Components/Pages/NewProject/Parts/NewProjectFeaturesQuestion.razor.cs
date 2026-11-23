using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>The AI assistant with live updates, and push notifications: both on by default, as on the website today (Q31).</summary>
public partial class NewProjectFeaturesQuestion
{
    [Parameter] public string HeadingElement { get; set; } = "h2";
}
