using Bit.Fabric.Client.Core.Components.Pages.Home.Parts;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// bitfabric.co/trust: who makes bit Fabric, what it sees and never sees of a project, where
/// everything of the visitor's lives, what happens if it stops, how to check it all, how to report a security problem, and
/// where bitfabric.co's own privacy notice is. Built from the Workbench's "Trust first" design, with only what is settled:
/// no data processing agreement, no list of services beyond the privacy notice, no certification, and no "no tracking",
/// which isn't settled yet.
/// </summary>
public partial class TrustPage
{
    private static string GetStartedUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.NewProject);

    private static string TermsUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Terms);

    private static string CliUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Cli);

    private string SecurityTxtUrl => $"{AppHomeJsonLd.SiteUrlOf(NavigationManager)}/.well-known/security.txt";

    /// <summary>The line the wizard's green shield gives to check a build's attestation (See NewProjectLineSteps).</summary>
    private const string AttestationLine = "gh attestation verify <bit.dll> --repo bitfoundation/bitplatform";

    /// <summary>The installers the pasted line runs, as the wizard links them: one for macOS and Linux, one for Windows.</summary>
    private static (string Name, string Url)[] Installers =>
    [
        ("install.sh", $"{NewProjectAnswers.InstallerHost}/install.sh"),
        ("install.ps1", $"{NewProjectAnswers.InstallerHost}/install.ps1")
    ];

    /// <summary>
    /// The Trust and privacy of bitfabric-internal/specs/product.md, row by row: what bit Fabric never sees, what the wizard
    /// never keeps, then what it sees, and only when the visitor allows it. <c>HomePageSeoTests</c>
    /// reads these rows against the spec's lists.
    /// </summary>
    private (string Topic, string Text, bool Never)[] Sees =>
    [
        (Localizer[nameof(AppStrings.TrustNeverCode)], Localizer[nameof(AppStrings.TrustNever)], true),
        (Localizer[nameof(AppStrings.TrustNeverPrompts)], Localizer[nameof(AppStrings.TrustNever)], true),
        (Localizer[nameof(AppStrings.TrustNeverData)], Localizer[nameof(AppStrings.TrustNever)], true),
        (Localizer[nameof(AppStrings.TrustNeverKeys)], Localizer[nameof(AppStrings.TrustNever)], true),
        (Localizer[nameof(AppStrings.TrustNeverLogs)], Localizer[nameof(AppStrings.TrustNeverLogsText)], true),
        (Localizer[nameof(AppStrings.TrustWizard)], Localizer[nameof(AppStrings.TrustWizardText)], true),
        (Localizer[nameof(AppStrings.TrustSeesAccount)], Localizer[nameof(AppStrings.TrustSeesAccountText)], false),
        (Localizer[nameof(AppStrings.TrustSeesHealth)], Localizer[nameof(AppStrings.TrustSeesHealthText)], false),
        (Localizer[nameof(AppStrings.TrustSeesScans)], Localizer[nameof(AppStrings.TrustSeesScansText)], false),
        // The command stays as it is typed, isolated so a right-to-left sentence keeps it whole
        (Localizer[nameof(AppStrings.TrustSeesCli)], Localizer[nameof(AppStrings.TrustSeesCliText), "⁨bit telemetry off⁩"], false)
    ];

    /// <summary>Where each piece lives: the home page's four tiles, with their own texts, and the server they run on.</summary>
    private AppHomeTile[] Where =>
    [
        new(BitIconName.Code, Localizer[nameof(AppStrings.AppHomeCompareYourCode)], Localizer[nameof(AppStrings.AppHomeOwnCodeText)]),
        new(BitIconName.Server, Localizer[nameof(AppStrings.TrustWhereServer)], Localizer[nameof(AppStrings.TrustWhereServerText)]),
        new(BitIconName.People, Localizer[nameof(AppStrings.AppHomeCompareYourUsers)], Localizer[nameof(AppStrings.AppHomeOwnUsersText)]),
        new(BitIconName.Permissions, Localizer[nameof(AppStrings.AppHomeOwnKeys)], Localizer[nameof(AppStrings.AppHomeOwnKeysText)]),
        new(BitIconName.Robot, Localizer[nameof(AppStrings.AppHomeOwnAI)], Localizer[nameof(AppStrings.AppHomeOwnAIText)])
    ];

    /// <summary>
    /// How to check it, in the words of the wizard's "Why is this line safe?": the installer
    /// to read, how the tool is built and signed, and the line that verifies its attestation.
    /// </summary>
    private AppHomeTile[] Check(RenderFragment installers, RenderFragment verify) =>
    [
        new(BitIconName.ReportDocument, Localizer[nameof(AppStrings.TrustCheckScript)], Localizer[nameof(AppStrings.NewProjectSafeInstaller)], More: installers),
        new(BitIconName.Code, Localizer[nameof(AppStrings.TrustCheckOpen)], Localizer[nameof(AppStrings.NewProjectSafeSigned)]),
        new(BitIconName.Certificate, Localizer[nameof(AppStrings.TrustCheckSigned)], Localizer[nameof(AppStrings.NewProjectSafeVerify), "⁨bit about⁩"], More: verify)
    ];

    /// <summary>
    /// The page as JSON-LD: the company in full, as the home page describes it (the same @id), a WebPage about it, and the way
    /// back to the home page. Only what the page shows.
    /// </summary>
    private string BuildJsonLd()
    {
        var siteUrl = AppHomeJsonLd.SiteUrlOf(NavigationManager);
        var title = Localizer[nameof(AppStrings.TrustPageTitle)].Value;

        return AppHomeJsonLd.Document(
            AppHomeJsonLd.Organization(siteUrl),
            AppHomeJsonLd.WebPage(CanonicalUrl, siteUrl, title, Localizer[nameof(AppStrings.TrustPageDescription)], AppHomeJsonLd.OrganizationId(siteUrl)),
            AppHomeJsonLd.Breadcrumb(CanonicalUrl, siteUrl, Localizer[nameof(AppStrings.Home)], Localizer[nameof(AppStrings.Trust)]));
    }
}
