using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>The product's name, which names the project, so it follows a C# project's name rules, with a valid one offered when it doesn't.</summary>
public partial class NewProjectNameQuestion
{
    [Parameter] public string HeadingElement { get; set; } = "h2";

    private void UseSuggestedName() => Store.Rename(Answers.SuggestedName);
}
