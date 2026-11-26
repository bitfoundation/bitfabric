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
        (ProjectLicenseKind.Licensed, LicenseState.EndingSoon) => (BitColor.Warning, BitIconName.Certificate, Localizer["License ends {0}", Day(License.Until)], Localizer["Renew it, free, from the link bit prints in the project's folder."]),
        (ProjectLicenseKind.Licensed, LicenseState.InGrace) => (BitColor.SevereWarning, BitIconName.Certificate, Localizer["License ended {0}", Day(License.Until)], Localizer["bit keeps working for two weeks after. Renew it, free, from the link bit prints."]),
        (ProjectLicenseKind.Licensed, _) => (BitColor.Success, BitIconName.Certificate, Localizer["Licensed until {0}", Day(License.Until)], null),
        (ProjectLicenseKind.LicenseEnded, _) => (BitColor.Error, BitIconName.Certificate, Localizer["License ended {0}", Day(License.Until)], Localizer["Renew it, free, from the link bit prints in the project's folder."]),
        (ProjectLicenseKind.Trial, _) => (BitColor.Info, BitIconName.Clock, Localizer["Trial until {0}", Day(License.Until)], null),
        (ProjectLicenseKind.TrialEnded, _) => (BitColor.Warning, BitIconName.Clock, Localizer["Trial ended {0}", Day(License.Until)], Localizer["Get its free license from the link bit prints in the project's folder."]),
        (ProjectLicenseKind.None, _) => (BitColor.SecondaryForeground, BitIconName.Certificate, Localizer["No license"], Localizer["Run bit license in the project's folder for the link that gets it one, free."]),
        _ => (BitColor.SecondaryForeground, BitIconName.Unknown, Localizer["License not known yet"], Localizer["bit Fabric learns it once the app is on the newest bit Boilerplate, or once your team gets its license."])
    };

    private string Day(DateTimeOffset? at) => at is { } value ? TimeZoneService.ToLocalTime(value).ToString("d", CultureInfo.CurrentUICulture) : "";
}
