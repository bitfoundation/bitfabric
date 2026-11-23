using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>
/// One of the template's choice options as a question, with a choice group as the database takes, its default marked: file
/// storage among the technical choices, and the choices of "Super technical".
/// </summary>
public partial class NewProjectOptionQuestion
{
    [Parameter, EditorRequired] public NewProjectTemplateOption Option { get; set; } = default!;

    [Parameter] public string HeadingElement { get; set; } = "h3";

    /// <summary>What the default choice is marked with: "Default", or "Recommended" among the technical choices, as the database is.</summary>
    [Parameter] public string? DefaultBadge { get; set; }

    private string Title(string name) => NewProjectOptionTexts.Title(Localizer, name);
}
