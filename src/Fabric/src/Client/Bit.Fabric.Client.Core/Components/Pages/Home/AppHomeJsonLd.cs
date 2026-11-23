using System.Text.Json.Nodes;

namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// The JSON-LD bitfabric.co's public pages share: the company behind the site as one Organization
/// node, which the home page and <c>/trust</c> describe in full and the others name by its <c>@id</c>, and a page's WebPage
/// with the way back to the home page. Only what the site shows: no rating, review or offer, as there are none, and an offer
/// with a price of 0 would read as the whole thing being free, which the claims table rules out
/// (bitfabric-internal/.agents/product-marketing.md).
/// </summary>
public static class AppHomeJsonLd
{
    /// <summary>The site's origin, not a page's url: the same nodes are the site's identity under every culture prefix.</summary>
    public static string SiteUrlOf(NavigationManager navigationManager) => new Uri(navigationManager.BaseUri).GetLeftPart(UriPartial.Authority);

    public static string OrganizationId(string siteUrl) => $"{siteUrl}/#organization";

    public static string WebSiteId(string siteUrl) => $"{siteUrl}/#website";

    /// <summary>bit Fabric itself, which the home page describes as a SoftwareApplication.</summary>
    public static string SoftwareId(string siteUrl) => $"{siteUrl}/#software";

    public static JsonObject Ref(string id) => new() { ["@id"] = id };

    /// <summary>The company, with its details (See <see cref="FabricSite"/>).</summary>
    public static JsonObject Organization(string siteUrl) => new()
    {
        ["@type"] = "Organization",
        ["@id"] = OrganizationId(siteUrl),
        ["name"] = "bit Fabric",
        ["legalName"] = FabricSite.LegalName,
        ["url"] = siteUrl,
        ["logo"] = $"{siteUrl}/images/icons/bit-icon-512.png",
        ["email"] = FabricSite.Email,
        ["address"] = new JsonObject
        {
            ["@type"] = "PostalAddress",
            ["streetAddress"] = FabricSite.StreetAddress,
            ["postalCode"] = FabricSite.PostalCode,
            ["addressLocality"] = FabricSite.Locality,
            ["addressCountry"] = FabricSite.CountryCode
        },
        ["vatID"] = FabricSite.VatNumber,
        // The KvK number as a plain identifier: the ISO 6523 code for the KvK register couldn't be verified
        ["identifier"] = new JsonObject
        {
            ["@type"] = "PropertyValue",
            ["propertyID"] = "KVK",
            ["value"] = FabricSite.KvkNumber
        },
        ["sameAs"] = new JsonArray(FabricSite.GitHubOrganizationUrl)
    };

    /// <summary>A page below the home page: what it is about, by its <c>@id</c>, and its breadcrumb (See <see cref="Breadcrumb"/>).</summary>
    public static JsonObject WebPage(string canonicalUrl, string siteUrl, string name, string description, string aboutId)
    {
        var webPage = new JsonObject
        {
            ["@type"] = "WebPage",
            ["@id"] = $"{canonicalUrl}#webpage",
            ["url"] = canonicalUrl,
            ["name"] = name,
            ["description"] = description,
            ["isPartOf"] = Ref(WebSiteId(siteUrl)),
            ["about"] = Ref(aboutId),
            ["breadcrumb"] = Ref($"{canonicalUrl}#breadcrumb")
        };

        if (string.IsNullOrWhiteSpace(CultureInfo.CurrentUICulture.Name) is false)
        {
            webPage["inLanguage"] = CultureInfo.CurrentUICulture.Name;
        }

        return webPage;
    }

    /// <summary>
    /// The way back to the home page, in the page's language, through the page above this one when it has one, such as
    /// /compare above /compare/lovable (<paramref name="parentName"/> and <paramref name="parentPage"/>, a PageUrls address).
    /// </summary>
    public static JsonObject Breadcrumb(string canonicalUrl, string siteUrl, string homeName, string name, string? parentName = null, string? parentPage = null)
    {
        string UrlOf(string page) => new Uri(new Uri(siteUrl), CultureInfoManager.GetCurrentCultureUrl(page)).ToString();

        var crumbs = new List<(string Name, string Url)> { (homeName, UrlOf(PageUrls.Home)) };
        if (parentName is not null && parentPage is not null)
        {
            crumbs.Add((parentName, UrlOf(parentPage)));
        }
        crumbs.Add((name, canonicalUrl));

        return new JsonObject
        {
            ["@type"] = "BreadcrumbList",
            ["@id"] = $"{canonicalUrl}#breadcrumb",
            ["itemListElement"] = new JsonArray([.. crumbs.Select((crumb, i) => (JsonNode)new JsonObject
            {
                ["@type"] = "ListItem",
                ["position"] = i + 1,
                ["name"] = crumb.Name,
                ["item"] = crumb.Url
            })])
        };
    }

    public static string Document(params JsonNode[] graph) => new JsonObject
    {
        ["@context"] = "https://schema.org",
        ["@graph"] = new JsonArray(graph)
    }.ToJsonString();
}
