namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// bitfabric.co/pricing: the draft plans, examples of who
/// pays what, an example month on a hosted builder and with bit Fabric, what a license gives, what stays true whatever the
/// visitor pays, what they pay others and the questions they ask, then the way to start. Built from the Workbench's pricing
/// design, option 1 with its "row by row" saving. Its words are <see cref="AppPricing"/>'s, which /pricing.md writes out too.
/// </summary>
public partial class PricingPage
{
    /// <summary>The example month's rows, for both cards.</summary>
    private AppPricingCostLine[] Saving => AppPricing.Saving(Localizer);

    private static string GetStartedUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.NewProject);

    /// <summary>Why a real app costs less, in the page's language.</summary>
    private static string CostUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Cost);

    private string MarkdownUrl => $"{AppHomeJsonLd.SiteUrlOf(NavigationManager)}{AppPricing.MarkdownUrl}";

    /// <summary>
    /// The page as JSON-LD: a WebPage about bit Fabric (the home page's SoftwareApplication, by its @id) and the way back to
    /// the home page. No offer: a price of 0 would read as the whole thing being free, and the plans are drafts that start after the beta.
    /// </summary>
    private string BuildJsonLd()
    {
        var siteUrl = AppHomeJsonLd.SiteUrlOf(NavigationManager);
        var title = Localizer[nameof(AppStrings.PricingPageTitle)].Value;

        return AppHomeJsonLd.Document(
            AppHomeJsonLd.WebPage(CanonicalUrl, siteUrl, title, Localizer[nameof(AppStrings.PricingPageDescription)], AppHomeJsonLd.SoftwareId(siteUrl)),
            AppHomeJsonLd.Breadcrumb(CanonicalUrl, siteUrl, Localizer[nameof(AppStrings.Home)], Localizer[nameof(AppStrings.Pricing)]));
    }
}
