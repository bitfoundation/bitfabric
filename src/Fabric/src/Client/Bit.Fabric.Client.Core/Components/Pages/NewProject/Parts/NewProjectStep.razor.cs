using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>
/// One step of the new project page. The page shows the current one; while it is pre-rendering it also renders every other
/// step, hidden, so a crawler and the page's Markdown copy read every question, not only the first.
/// </summary>
public partial class NewProjectStep
{
    /// <summary>The step, from 0 (the product) to 4 (getting it).</summary>
    [Parameter] public int Step { get; set; }

    /// <summary>The button that folds the super technical options away, with how many of them the visitor changed.</summary>
    private string SuperTechnicalSummary
    {
        get
        {
            var changed = Answers.TemplateOptions.Keys.Count(name => NewProjectTemplateOption.Find(name) is { IsTechnical: false });

            return changed is 0
                ? Localizer[nameof(AppStrings.NewProjectSuperTechnicalDefaults)]
                : Localizer[nameof(AppStrings.NewProjectSuperTechnicalChanged), changed];
        }
    }
}
