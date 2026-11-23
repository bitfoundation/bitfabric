using System.Text.Json.Nodes;
using Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;
using Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Compare;

/// <summary>
/// bitfabric.co/compare and its pages (bitfabric-internal/specs/home-page.md, "The comparison pages"). One page with a
/// literal route per address, so each prerenders at its own address with its own title, description, canonical and
/// hreflang, the sitemap lists each, and an unknown subject finds no route and answers 404. Which subject a route shows is read from its address (See <see cref="Subject"/>).
/// </summary>
public partial class ComparePage
{
    private string? builtFor;
    private CompareSubject current = default!;
    private CompareSubject[] pages = [];

    private static string HubUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Compare);

    private static string PricingUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Pricing);

    private static string CostUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Cost);

    private static string GetStartedUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.NewProject);

    /// <summary>The subject of this address, in the page's language, built once per address and language.</summary>
    private CompareSubject Subject
    {
        get
        {
            Build();
            return current;
        }
    }

    /// <summary>The pages the hub's cards lead to.</summary>
    private CompareSubject[] Subjects
    {
        get
        {
            Build();
            return pages;
        }
    }

    private static bool IsHub(CompareSubject subject) => subject.Url == PageUrls.Compare;

    /// <summary>The id of a source's entry in the page's list, which the table's numbers lead to.</summary>
    public static string AnchorOf(string sourceId) => AppHomeSourceList.AnchorOf(sourceId);

    /// <summary>
    /// A place on this page, with the page's own path: the site's base address is its root, so a bare #anchor would lead to
    /// the home page, as the band titles' addresses know (See AppHomeBandTitle).
    /// </summary>
    private string InPage(string anchor) => $"{new Uri(NavigationManager.Uri).AbsolutePath}#{anchor}";

    /// <summary>
    /// A title split around bit Fabric's name, so the page draws the name unbroken: a Persian title on a phone broke it
    /// between its two words. <c>After</c> is null when the title doesn't name it.
    /// </summary>
    private static (string Before, string? After) AroundBrand(string title)
    {
        const string brand = "bit Fabric";
        var at = title.IndexOf(brand, StringComparison.Ordinal);
        return at < 0 ? (title, null) : (title[..at], title[(at + brand.Length)..]);
    }

    private void Build()
    {
        var path = PathOf(new Uri(NavigationManager.Uri));
        var key = $"{CultureInfo.CurrentUICulture.Name}{path}";
        if (key == builtFor)
            return;

        pages = HubComparison.Subjects(Localizer);
        current = pages.FirstOrDefault(page => page.Url == path) ?? HubComparison.Build(Localizer);
        builtFor = key;
    }

    /// <summary>The address's path without its culture segment or a trailing slash: /en-US/compare/bolt/ is /compare/bolt.</summary>
    private static string PathOf(Uri uri)
    {
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rest = segments.Length > 0 && CultureInfoManager.GetCultureInfo(segments[0]) is not null ? segments[1..] : segments;
        return $"/{string.Join('/', rest)}";
    }

    /// <summary>
    /// The page as JSON-LD, with real values only (bitfabric-internal/.agents/gtm/research/seo-geo.md, section 4.3): a WebPage about bit Fabric (the home page's
    /// SoftwareApplication, by its @id) and about the other product by its name and address, citing every source the page
    /// lists, modified the day they were read, and the way back through /compare to the home page. No rating, review or offer.
    /// </summary>
    private string BuildJsonLd(CompareSubject subject)
    {
        var siteUrl = AppHomeJsonLd.SiteUrlOf(NavigationManager);
        var softwareId = AppHomeJsonLd.SoftwareId(siteUrl);

        var webPage = AppHomeJsonLd.WebPage(CanonicalUrl, siteUrl, subject.PageTitle, subject.Description, softwareId);
        if (subject.ProductUrl is { } productUrl)
        {
            webPage["about"] = new JsonArray(
                AppHomeJsonLd.Ref(softwareId),
                new JsonObject { ["@type"] = "SoftwareApplication", ["name"] = subject.Name, ["url"] = productUrl });
        }
        webPage["dateModified"] = CompareShared.CheckedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        webPage["citation"] = new JsonArray([.. subject.SourceIds.Select(id => CompareSources.All[id]).Select(source => (JsonNode)new JsonObject
        {
            ["@type"] = "CreativeWork",
            ["name"] = source.Name,
            ["url"] = source.Urls[0]
        })]);

        var home = Localizer[nameof(AppStrings.Home)].Value;
        var breadcrumb = IsHub(subject)
            ? AppHomeJsonLd.Breadcrumb(CanonicalUrl, siteUrl, home, subject.Name)
            : AppHomeJsonLd.Breadcrumb(CanonicalUrl, siteUrl, home, subject.Name, Localizer[nameof(AppStrings.Compare)], PageUrls.Compare);

        return AppHomeJsonLd.Document(webPage, breadcrumb);
    }
}
