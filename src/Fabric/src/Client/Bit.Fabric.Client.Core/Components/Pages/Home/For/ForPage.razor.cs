using System.Text.Json.Nodes;
using Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;
using Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For;

/// <summary>
/// bitfabric.co/for and its pages (bitfabric-internal/specs/home-page.md, "The AI agent pages"): one component for the hub and
/// every agent and model, each page's words in its own class under Data. A literal route per address, so each
/// prerenders at its own address with its own title, description, canonical and hreflang, the sitemap lists each, and an
/// unknown agent finds no route and answers 404. Which page a route shows is read from its address (See <see cref="Page"/>).
/// </summary>
public partial class ForPage
{
    private string? builtFor;
    private AgentPage current = default!;
    private AgentPage[] agents = [];
    private AgentPage[] models = [];

    /// <summary>The page of this address, in the page's language, built once per address and language.</summary>
    private AgentPage Page
    {
        get
        {
            Build();
            return current;
        }
    }

    /// <summary>The agents' pages, which the hub's table and cards show.</summary>
    private AgentPage[] Agents
    {
        get
        {
            Build();
            return agents;
        }
    }

    /// <summary>The models' pages.</summary>
    private AgentPage[] ModelPages
    {
        get
        {
            Build();
            return models;
        }
    }

    /// <summary>Every page under the hub, which each page's band of the others leads to.</summary>
    private AgentPage[] Pages => [.. Agents, .. ModelPages];

    private static bool IsHub(AgentPage page) => page.Url == PageUrls.For;

    private static string UrlOf(string page) => CultureInfoManager.GetCurrentCultureUrl(page);

    /// <summary>
    /// A place on this page, with the page's own path: the site's base address is its root, so a bare #anchor would lead to
    /// the home page, as the band titles' addresses know (See AppHomeBandTitle).
    /// </summary>
    private string InPage(string anchor) => $"{new Uri(NavigationManager.Uri).AbsolutePath}#{anchor}";

    private static string IconOf(AgentLineKind kind) => kind switch
    {
        AgentLineKind.Instructions => BitIconName.ReportDocument,
        AgentLineKind.Skills => BitIconName.TaskList,
        AgentLineKind.Tools => BitIconName.PlugConnected,
        _ => BitIconName.Robot
    };

    /// <summary>The numbers of a model's sources and who makes it, under its line.</summary>
    private static RenderFragment ModelMore(AgentModel model, RenderFragment<string[]> refs) => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "for-maker");
        builder.AddContent(2, model.Maker);
        builder.AddContent(3, refs(model.Sources));
        builder.CloseElement();
    };

    /// <summary>
    /// The cost band's tiles: the agent's or the model's own plan, with the page its price is read from; bit Fabric's, which
    /// starts free during the beta and sells no AI credits; and the server the app runs on.
    /// </summary>
    private AppHomeTile[] CostTiles(AgentPage page, RenderFragment<string[]> refs) =>
    [
        new(page.CardIcon, page.Name, page.Cost.Text, page.Cost.Value, page.Cost.Sources.Length > 0 ? refs(page.Cost.Sources) : null),
        new(BitIconName.Savings, "bit Fabric", Localizer[nameof(AppStrings.ForCostFabricText)]),
        new(BitIconName.Server, Localizer[nameof(AppStrings.ForCostServerTitle)], Localizer[nameof(AppStrings.ForCostServerText)], Localizer[nameof(AppStrings.ForCostServerValue)])
    ];

    private void Build()
    {
        var path = PathOf(new Uri(NavigationManager.Uri));
        var key = $"{CultureInfo.CurrentUICulture.Name}{path}";
        if (key == builtFor)
            return;

        agents = ForHub.Agents(Localizer);
        models = ForHub.Models(Localizer);
        current = agents.Concat(models).FirstOrDefault(page => page.Url == path) ?? ForHub.Build(Localizer);
        builtFor = key;
    }

    /// <summary>The address's path without its culture segment or a trailing slash: /en-US/for/codex/ is /for/codex.</summary>
    private static string PathOf(Uri uri)
    {
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rest = segments.Length > 0 && CultureInfoManager.GetCultureInfo(segments[0]) is not null ? segments[1..] : segments;
        return $"/{string.Join('/', rest)}";
    }

    /// <summary>
    /// The page as JSON-LD, with real values only (bitfabric-internal/.agents/gtm/research/seo-geo.md, section 4.3): a WebPage about bit Fabric (the home page's
    /// SoftwareApplication, by its @id) and about the agent by its name and address, citing every source the page lists,
    /// modified the day they were read, and the way back through /for to the home page. No rating, review or offer.
    /// </summary>
    private string BuildJsonLd(AgentPage page, string[] ids)
    {
        var siteUrl = AppHomeJsonLd.SiteUrlOf(NavigationManager);
        var softwareId = AppHomeJsonLd.SoftwareId(siteUrl);

        var webPage = AppHomeJsonLd.WebPage(CanonicalUrl, siteUrl, page.PageTitle, page.Description, softwareId);
        if (page.ProductUrl is { } productUrl)
        {
            webPage["about"] = new JsonArray(
                AppHomeJsonLd.Ref(softwareId),
                new JsonObject { ["@type"] = "SoftwareApplication", ["name"] = page.Name, ["url"] = productUrl });
        }
        webPage["dateModified"] = ForShared.CheckedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        webPage["citation"] = new JsonArray([.. ids.Select(id => ForSources.All[id]).Select(source => (JsonNode)new JsonObject
        {
            ["@type"] = "CreativeWork",
            ["name"] = source.Name,
            ["url"] = source.Urls[0]
        })]);

        var home = Localizer[nameof(AppStrings.Home)].Value;
        var breadcrumb = IsHub(page)
            ? AppHomeJsonLd.Breadcrumb(CanonicalUrl, siteUrl, home, page.Name)
            : AppHomeJsonLd.Breadcrumb(CanonicalUrl, siteUrl, home, page.Name, Localizer[nameof(AppStrings.ForHubName)], PageUrls.For);

        return AppHomeJsonLd.Document(webPage, breadcrumb);
    }
}
