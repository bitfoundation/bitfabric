using System.Text.Json.Nodes;

namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// bitfabric.co/cost: why a real app costs less with bit Fabric. Count what a
/// finished feature costs, not a prompt; what every project starts with is already built; the AI settles the plan and shows
/// the design before it builds; quality saves; and a stronger model costs twice as much per token but not per task, in
/// Anthropic's own figures, each leading to its source. Its words are <see cref="AppCostCase"/>'s, which /cost.md writes out too.
/// </summary>
public partial class CostPage
{
    private static string GetStartedUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.NewProject);

    private static string PricingUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Pricing);

    private static string CompareUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Compare);

    private string MarkdownUrl => $"{AppHomeJsonLd.SiteUrlOf(NavigationManager)}{AppCostCase.MarkdownUrl}";

    /// <summary>The day every source was read, in the page's language.</summary>
    private string ReadOn => Localizer[nameof(AppStrings.CostReadDate)];

    /// <summary>
    /// A place on this page, with the page's own path: the site's base address is its root, so a bare #anchor would lead to
    /// the home page, as the band titles' addresses know (See AppHomeBandTitle).
    /// </summary>
    private string InPage(string anchor) => $"{new Uri(NavigationManager.Uri).AbsolutePath}#{anchor}";

    /// <summary>An address as a reader would type it, without its scheme or a trailing slash.</summary>
    private static string Shown(string url) => url.Replace("https://", string.Empty, StringComparison.Ordinal).TrimEnd('/');

    /// <summary>A bar's length as CSS reads it, whatever the culture writes a decimal point as.</summary>
    private static string Percent(double share) => (share * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    /// <summary>
    /// The page as JSON-LD: a WebPage about bit Fabric (the home page's SoftwareApplication, by its @id) that cites Anthropic's
    /// pages it quotes, and the way back to the home page. No offer, rating or review: there are none.
    /// </summary>
    private string BuildJsonLd()
    {
        var siteUrl = AppHomeJsonLd.SiteUrlOf(NavigationManager);
        var title = Localizer[nameof(AppStrings.CostPageTitle)].Value;

        var page = AppHomeJsonLd.WebPage(CanonicalUrl, siteUrl, title, Localizer[nameof(AppStrings.CostPageDescription)], AppHomeJsonLd.SoftwareId(siteUrl));
        page["dateModified"] = AppCostCase.ReadOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        page["citation"] = new JsonArray([.. AppCostCase.Sources(Localizer).Select(source => (JsonNode)new JsonObject
        {
            ["@type"] = "CreativeWork",
            ["name"] = source.Name,
            ["url"] = source.Url
        })]);

        return AppHomeJsonLd.Document(
            page,
            AppHomeJsonLd.Breadcrumb(CanonicalUrl, siteUrl, Localizer[nameof(AppStrings.Home)], Localizer[nameof(AppStrings.Cost)]));
    }
}
