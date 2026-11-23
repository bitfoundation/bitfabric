using Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// bitfabric.co/about: what the name means (the brand story: the warp is ready, you weave the weft), the principles
/// bit Fabric is built on (bitfabric-internal/specs/product.md, Design principles), who makes it, and the ways on to /trust,
/// /pricing and the terms. Its last section is what a user of an installed app needs from the page: what the app knows of
/// itself, which each app's own About page passes in (Client.Web, Client.Maui and Client.Windows), since only that app's
/// platform can tell it, and the open-source licenses, on a page of their own. Nothing on it names the technology underneath.
/// </summary>
public partial class AboutBitFabric
{
    /// <summary>The product's name, as the apps' About pages show it: never Bit.Fabric.</summary>
    public const string AppName = "bit Fabric";

    /// <summary>What the app tells of itself, its version above all: each app's AppInfoCard.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private static string GetStartedUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.NewProject);

    private static string TrustUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Trust);

    private static string PricingUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Pricing);

    private static string TermsUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Terms);

    private static string LicensesUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.OpenSourceLicenses);

    /// <summary>The warp, the weft and the fabric.</summary>
    private AppHomeTile[] Story =>
    [
        new(BitIconName.ShowGrid, Localizer[nameof(AppStrings.AboutWarpTitle)], Localizer[nameof(AppStrings.AboutWarpText)]),
        new(BitIconName.Lightbulb, Localizer[nameof(AppStrings.AboutWeftTitle)], Localizer[nameof(AppStrings.AboutWeftText)]),
        new(BitIconName.VerifiedBrand, Localizer[nameof(AppStrings.AboutFabricTitle)], Localizer[nameof(AppStrings.AboutFabricText)])
    ];

    /// <summary>The Design principles of bitfabric-internal/specs/product.md, in its order, each said for someone who doesn't build software.</summary>
    private AppHomeTile[] Principles =>
    [
        new(BitIconName.Home, Localizer[nameof(AppStrings.AboutPrincipleYoursTitle)], Localizer[nameof(AppStrings.AboutPrincipleYoursText)]),
        new(BitIconName.Preview, Localizer[nameof(AppStrings.AboutPrincipleProductFirstTitle)], Localizer[nameof(AppStrings.AboutPrincipleProductFirstText)]),
        new(BitIconName.TimelineProgress, Localizer[nameof(AppStrings.AboutPrincipleQuietTitle)], Localizer[nameof(AppStrings.AboutPrincipleQuietText)]),
        new(BitIconName.Info, Localizer[nameof(AppStrings.AboutPrincipleExplainTitle)], Localizer[nameof(AppStrings.AboutPrincipleExplainText)]),
        new(BitIconName.Chat, Localizer[nameof(AppStrings.AboutPrincipleWordsTitle)], Localizer[nameof(AppStrings.AboutPrincipleWordsText)]),
        new(BitIconName.FieldReadOnly, Localizer[nameof(AppStrings.AboutPrincipleReadOnlyTitle)], Localizer[nameof(AppStrings.AboutPrincipleReadOnlyText)]),
        new(BitIconName.Savings, Localizer[nameof(AppStrings.AboutPrincipleCostTitle)], Localizer[nameof(AppStrings.AboutPrincipleCostText)]),
        new(BitIconName.Shield, Localizer[nameof(AppStrings.AboutPrincipleSafeTitle)], Localizer[nameof(AppStrings.AboutPrincipleSafeText)]),
        new(BitIconName.Switch, Localizer[nameof(AppStrings.AboutPrincipleReplaceableTitle)], Localizer[nameof(AppStrings.AboutPrincipleReplaceableText)])
    ];

    // The weave in the story band, in the picture's own units: warp threads stand across the whole width, and the weft is
    // woven through them, over one and under the next, row by row, the visitor's row in blue and still on its way.
    private const int ThreadWidth = 10;
    private const int ThreadGap = 8;
    private const int WarpCount = 24;
    private const int WeftCount = 6;
    private const int YourRow = 3;
    private const int YourRowWarps = 15;

    private const int WeaveWidth = WarpCount * (ThreadWidth + ThreadGap) - ThreadGap;
    private const int WeaveHeight = WeftCount * (ThreadWidth + ThreadGap) - ThreadGap;

    private static int At(int index) => index * (ThreadWidth + ThreadGap);

    private static readonly int[] WarpXs = [.. Enumerable.Range(0, WarpCount).Select(At)];

    /// <summary>The rows woven so far: each above the visitor's runs the whole width, the visitor's stops part way.</summary>
    private static readonly (int Y, int Length, bool IsYours)[] WeftRows =
        [.. Enumerable.Range(0, YourRow + 1).Select(row => (At(row), row == YourRow ? At(YourRowWarps) - ThreadGap / 2 : WeaveWidth, row == YourRow))];

    /// <summary>Where a warp thread lies over the weft: every other crossing, shifted by one on each row.</summary>
    private static readonly (int X, int Y)[] WarpOverWeft =
        [.. from row in Enumerable.Range(0, YourRow + 1)
            from warp in Enumerable.Range(0, row == YourRow ? YourRowWarps : WarpCount)
            where (warp + row) % 2 == 0
            select (At(warp), At(row))];

    /// <summary>
    /// The page as JSON-LD: the company in full, as the home page and /trust describe it (the same @id), the page as an
    /// AboutPage about it, and the way back to the home page. Only what the page shows.
    /// </summary>
    private string BuildJsonLd()
    {
        var siteUrl = AppHomeJsonLd.SiteUrlOf(NavigationManager);
        var canonicalUrl = new Uri(NavigationManager.Uri).GetCanonicalUrl();

        var page = AppHomeJsonLd.WebPage(canonicalUrl, siteUrl, Localizer[nameof(AppStrings.AboutBitFabricPageTitle)],
            Localizer[nameof(AppStrings.AboutBitFabricPageDescription)], AppHomeJsonLd.OrganizationId(siteUrl));
        page["@type"] = "AboutPage";

        return AppHomeJsonLd.Document(
            AppHomeJsonLd.Organization(siteUrl),
            page,
            AppHomeJsonLd.Breadcrumb(canonicalUrl, siteUrl, Localizer[nameof(AppStrings.Home)], Localizer[nameof(AppStrings.About)]));
    }
}
