using Bit.Fabric.Shared.Features.Licensing.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Licensing;

/// <summary>
/// A project's license in a few words (bitfabric-internal/specs/product.md, Seeing licenses): licensed until a date, about
/// to end, in its weeks of grace, ended, a trial, or none, for the project cards, a project's page and the Licenses tab.
/// </summary>
public partial class ProjectLicenseTag
{
    [Parameter, EditorRequired] public ProjectLicenseDto License { get; set; } = default!;

    [Parameter] public BitSize Size { get; set; } = BitSize.Small;

    private (BitColor Color, string Icon, string Text, string? Help) Look => (License.Kind, License.State) switch
    {
        (ProjectLicenseKind.Licensed, LicenseState.EndingSoon) => (BitColor.Warning, BitIconName.Certificate, Localizer[nameof(AppStrings.LicenseEnds), Day(License.Until)], Localizer[nameof(AppStrings.LicenseRenewFromTheLink)]),
        (ProjectLicenseKind.Licensed, LicenseState.InGrace) => (BitColor.SevereWarning, BitIconName.Certificate, Localizer[nameof(AppStrings.LicenseEnded), Day(License.Until)], Localizer[nameof(AppStrings.LicenseInGraceHelp)]),
        (ProjectLicenseKind.Licensed, _) => (BitColor.Success, BitIconName.Certificate, Localizer[nameof(AppStrings.LicensedUntil), Day(License.Until)], null),
        (ProjectLicenseKind.LicenseEnded, _) => (BitColor.Error, BitIconName.Certificate, Localizer[nameof(AppStrings.LicenseEnded), Day(License.Until)], Localizer[nameof(AppStrings.LicenseRenewFromTheLink)]),
        (ProjectLicenseKind.Trial, _) => (BitColor.Info, BitIconName.Clock, Localizer[nameof(AppStrings.LicenseTrialUntil), Day(License.Until)], null),
        (ProjectLicenseKind.TrialEnded, _) => (BitColor.Warning, BitIconName.Clock, Localizer[nameof(AppStrings.LicenseTrialEnded), Day(License.Until)], Localizer[nameof(AppStrings.LicenseGetFromTheLink)]),
        (ProjectLicenseKind.None, _) => (BitColor.SecondaryForeground, BitIconName.Certificate, Localizer[nameof(AppStrings.LicenseNone)], Localizer[nameof(AppStrings.LicenseRunBitLicense)]),
        _ => (BitColor.SecondaryForeground, BitIconName.Unknown, Localizer[nameof(AppStrings.LicenseNotKnownYet)], Localizer[nameof(AppStrings.LicenseNotKnownYetHelp)])
    };

    private string Day(DateTimeOffset? at) => at is { } value ? TimeZoneService.ToLocalTime(value).ToString("d", CultureInfo.CurrentUICulture) : "";
}
