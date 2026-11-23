using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>
/// "Super technical", under the technical choices of the last step: the template's options that neither a question nor the
/// technical choices ask. Each starts at its default, so leaving the section closed changes
/// nothing. The choices take a choice group as the database does and the on/off ones a switch, in the list's order: the
/// choices before the switches above them and the ones after (the design system) below them.
/// </summary>
public partial class NewProjectSuperTechnicalQuestion
{
    [Parameter] public string HeadingElement { get; set; } = "h3";

    private static IEnumerable<NewProjectTemplateOption> Options(NewProjectOptionKind kind) =>
        NewProjectTemplateOption.SuperTechnical.Where(option => option.Kind == kind);

    private static IEnumerable<NewProjectTemplateOption> ChoicesBeforeSwitches =>
        NewProjectTemplateOption.SuperTechnical.TakeWhile(option => option.Kind == NewProjectOptionKind.Choice);

    private static IEnumerable<NewProjectTemplateOption> ChoicesAfterSwitches =>
        NewProjectTemplateOption.SuperTechnical.SkipWhile(option => option.Kind == NewProjectOptionKind.Choice)
            .Where(option => option.Kind == NewProjectOptionKind.Choice);

    private static bool IsOn(NewProjectAnswers answers, string name) => answers.Option(name) is "true";

    private void Switch(string name, bool isOn) => Store.SetTemplateOption(name, isOn ? "true" : "false");

    private string Title(string name) => NewProjectOptionTexts.Title(Localizer, name);

    private string? Lead(string name) => NewProjectOptionTexts.Lead(Localizer, name);
}
