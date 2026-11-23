using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

/// <summary>
/// What keeps running and what stops if bit Fabric does, as two cards: nothing of the visitor's depends on it. The home
/// page's band and /trust's show it the same way, each under its own title. Used as it is by the app and by its design in the Workbench, so it needs nothing of either's base class.
/// </summary>
public partial class AppHomeIfWeStop
{
    [Inject] private IStringLocalizer<AppStrings> Localizer { get; set; } = default!;

    /// <summary>The heading level of each card's title.</summary>
    [Parameter] public string HeadingElement { get; set; } = "h3";

    private string[] KeepsRunning =>
    [
        Localizer[nameof(AppStrings.AppHomeKeepsRunningApp)],
        Localizer[nameof(AppStrings.AppHomeKeepsRunningCode)],
        Localizer[nameof(AppStrings.AppHomeKeepsRunningPipeline)],
        Localizer[nameof(AppStrings.AppHomeKeepsRunningAI)]
    ];
}
