using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

/// <summary>
/// Who makes bit Fabric: the company as <see cref="FabricSite"/> holds it, the facts Dutch law asks a business's site to show,
/// beside the team behind bit platform with the home page's numbers. /trust and /about
/// show it the same way. Used as it is by the app and by its design in the Workbench, so it needs nothing of either's base class.
/// </summary>
public partial class AppHomeCompany
{
    [Inject] private IStringLocalizer<AppStrings> Localizer { get; set; } = default!;

    /// <summary>The heading level of each card's title.</summary>
    [Parameter] public string HeadingElement { get; set; } = "h3";

    private (string Term, string Detail, string? Url)[] Company =>
    [
        (Localizer[nameof(AppStrings.TrustCompanyName)], FabricSite.LegalName, null),
        (Localizer[nameof(AppStrings.TrustCompanyAddress)], FabricSite.Address, null),
        (Localizer[nameof(AppStrings.TrustCompanyKvk)], FabricSite.KvkNumber, null),
        (Localizer[nameof(AppStrings.TrustCompanyVat)], FabricSite.VatNumber, null),
        (Localizer[nameof(AppStrings.TrustCompanyContact)], FabricSite.Email, $"mailto:{FabricSite.Email}")
    ];
}
