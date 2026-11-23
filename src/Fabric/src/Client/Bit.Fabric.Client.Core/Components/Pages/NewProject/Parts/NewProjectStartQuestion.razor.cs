using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>What the project starts with: the essentials, the admin panel or the store, with the live demos of both.</summary>
public partial class NewProjectStartQuestion
{
    [Parameter] public string HeadingElement { get; set; } = "h2";

    private static string Icon(NewProjectStart start) => start switch
    {
        NewProjectStart.Admin => BitIconName.BarChartVerticalFill,
        NewProjectStart.Store => BitIconName.ShoppingCart,
        _ => BitIconName.Contact
    };

    private string Description(NewProjectStart start) => start switch
    {
        NewProjectStart.Admin => Localizer[nameof(AppStrings.NewProjectStartAdminText)],
        NewProjectStart.Store => Localizer[nameof(AppStrings.NewProjectStartStoreText)],
        _ => Localizer[nameof(AppStrings.NewProjectStartBlankText)]
    };
}
