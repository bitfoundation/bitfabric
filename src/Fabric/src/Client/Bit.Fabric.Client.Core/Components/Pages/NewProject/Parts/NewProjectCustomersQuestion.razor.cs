using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>Several customer companies or one organization: the template's multi-tenancy, named as it is with what it means (Q29).</summary>
public partial class NewProjectCustomersQuestion
{
    [Parameter] public string HeadingElement { get; set; } = "h2";
}
