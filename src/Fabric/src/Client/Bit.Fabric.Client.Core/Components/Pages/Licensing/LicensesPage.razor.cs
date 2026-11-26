using Bit.Fabric.Shared.Features.Licensing;
using Bit.Fabric.Shared.Features.Licensing.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Licensing;

/// <summary>
/// Every license of each team the signed-in account is in (bitfabric-internal/specs/product.md, Seeing licenses): its
/// project, until when, who got it, and the prompt that puts it into the project again, for a teammate's clone or a fresh
/// repository of the same first commit.
/// </summary>
public partial class LicensesPage
{
    [AutoInject] private ILicenseController licenseController = default!;

    [AutoInject] private Clipboard clipboard = default!;

    private bool isLoading = true;
    private List<LicenseDto> licenses = [];
    private Guid? copied;

    private static string BackIcon => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? BitIconName.Forward : BitIconName.Back;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        try
        {
            licenses = await licenseController.GetLicenses(CurrentCancellationToken);
        }
        finally
        {
            isLoading = false;
        }
    }

    private static ProjectLicenseDto AsProjectLicense(LicenseDto license) => new()
    {
        Kind = license.State is LicenseState.Ended ? ProjectLicenseKind.LicenseEnded : ProjectLicenseKind.Licensed,
        State = license.State,
        Until = license.ExpiresOn,
        LicenseId = license.Id
    };

    private string Day(DateTimeOffset at) => TimeZoneService.ToLocalTime(at).ToString("d", CultureInfo.CurrentUICulture);

    private async Task CopyPrompt(LicenseDto license)
    {
        await clipboard.WriteText(LicensePrompt.For(license.ProjectName, license.License!));
        copied = license.Id;
    }
}
