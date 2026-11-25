using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Bit.Fabric.Client.Core.Components;
using Bit.Fabric.Client.Core.Components.Pages.Home;

namespace Bit.Fabric.Tests.Features.Home;

/// <summary>
/// What crawlers and AI agents get from bitfabric.co besides the pages' text (bitfabric-internal/specs/home-page.md, "What
/// crawlers and agents get", 2026-11-19): llms.txt, the sitemap, the JSON-LD, security.txt, the company's details, links
/// that need no redirect, one address per page and language-only hreflang.
/// </summary>
public partial class HomePageSeoTests
{
    /// <summary>The site's public pages, which the sitemap lists under every culture and llms.txt may name.</summary>
    private static readonly string[] PublicPages =
        [PageUrls.Home, PageUrls.Features, PageUrls.Pricing, PageUrls.Trust, PageUrls.Cost, PageUrls.Cli, PageUrls.Contents, PageUrls.NewProject, PageUrls.SetupProject, PageUrls.Terms, PageUrls.PrivacyPolicy, PageUrls.About, .. ComparePages, .. ForPages];

    /// <summary>Pages that only exist to sign in, or that need an account to show anything.</summary>
    private static readonly string[] AccountPages = [PageUrls.SignIn, PageUrls.SignUp, PageUrls.Projects, PageUrls.Settings];

    [TestMethod]
    public async Task LlmsTxt_Should_SayWhatBitFabricIs_AndListOnlyPublicPagesThatAnswer()
    {
        await using var server = await StartPrerenderingServer();
        using var client = NoRedirectClient(server);

        using var response = await client.GetAsync("/llms.txt", TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(response.Content.Headers.ContentType);
        Assert.AreEqual("text/plain", response.Content.Headers.ContentType.MediaType);
        var llms = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        Assert.StartsWith("# bit Fabric\n", llms, "It names the product the way the product names itself.");
        foreach (var word in new[] { "Bit.Fabric", "{{", "PLACEHOLDER", ".NET", "C#", "Blazor", "run a command", "laptop", "referral" })
        {
            Assert.DoesNotContain(word, llms, $"llms.txt says nothing of \"{word}\".");
        }

        var origin = server.WebAppAddress.ToString().TrimEnd('/');
        var ownLinks = Regex.Matches(llms, @"\]\((?<url>[^)]+)\)").Select(match => match.Groups["url"].Value).Where(url => url.StartsWith(origin, StringComparison.Ordinal)).ToArray();
        Assert.IsGreaterThanOrEqualTo(5, ownLinks.Length, "It lists the site's own pages, under this host's address.");
        foreach (var page in new[] { $"{origin}/en-US{PageUrls.Pricing}", $"{origin}{AppPricing.MarkdownUrl}", $"{origin}/en-US{PageUrls.Trust}", $"{origin}/en-US{PageUrls.About}", $"{origin}/en-US{PageUrls.Cost}", $"{origin}{AppCostCase.MarkdownUrl}", $"{origin}/en-US{PageUrls.Cli}", $"{origin}{AppCliGuide.MarkdownUrl}" }.Concat(ComparePages.Select(compare => $"{origin}/en-US{compare}")))
        {
            Assert.Contains(page, ownLinks, $"It lists {page}.");
        }

        foreach (var link in ownLinks)
        {
            var path = new Uri(link).AbsolutePath;

            // A page's Markdown copy, such as /pricing.md, is one address for every language
            if (path.EndsWith(".md", StringComparison.Ordinal))
            {
                using var markdown = await client.GetAsync(link, TestContext.CancellationToken);
                Assert.AreEqual(HttpStatusCode.OK, markdown.StatusCode, $"{link} answers.");
                Assert.IsNotNull(markdown.Content.Headers.ContentType, $"{link} has a type.");
                Assert.AreEqual("text/markdown", markdown.Content.Headers.ContentType.MediaType, $"{link} is Markdown.");
                continue;
            }

            Assert.StartsWith("/en-US/", path, $"{link} carries its culture, so an agent meets no redirect.");
            Assert.Contains(BarePath(path), PublicPages, $"{link} is a public page.");
            Assert.DoesNotContain(BarePath(path), AccountPages, $"{link} needs no account.");

            using var page = await client.GetAsync(link, TestContext.CancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, page.StatusCode, $"{link} answers.");
        }

        // Every page leads agents to it, as they find such files through links (llmstxt.org)
        var head = Head(await GetPage(server, Googlebot, $"/en-US{PageUrls.Features}"));
        Assert.Contains($"<link rel=\"describedby\" href=\"{origin}/llms.txt\"", head);
    }

    [TestMethod]
    public async Task TheSitemap_Should_ListThePublicPages_AndNoneThatOnlySignIn()
    {
        await using var server = await StartPrerenderingServer();
        using var client = NoRedirectClient(server);

        var sitemap = XDocument.Parse(await client.GetStringAsync("/sitemap.xml", TestContext.CancellationToken));
        var locs = sitemap.Descendants(XName.Get("loc", "http://www.sitemaps.org/schemas/sitemap/0.9")).Select(loc => new Uri(loc.Value)).ToArray();
        var pages = locs.Select(loc => BarePath(loc.AbsolutePath)).Distinct().ToArray();

        foreach (var page in PublicPages)
        {
            Assert.Contains(page, pages, $"The sitemap lists {page}.");
        }
        foreach (var page in AccountPages)
        {
            Assert.DoesNotContain(page, pages, $"The sitemap leaves {page} out.");
        }
        Assert.HasCount(PublicPages.Length * CultureInfoManager.SupportedCultures.Length, locs, "Each public page once under each culture.");

        // Every address it lists answers as it is, without a redirect
        foreach (var loc in locs.Where(loc => loc.AbsolutePath.StartsWith("/en-US/", StringComparison.Ordinal) || loc.AbsolutePath.StartsWith("/fa-IR/", StringComparison.Ordinal)))
        {
            using var response = await client.GetAsync(loc, TestContext.CancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"{loc} answers.");
        }
    }

    /// <summary>
    /// The home page's JSON-LD describes the company with its legal details, the site, the product and the page, and nothing
    /// that isn't on the site: no placeholder, rating, review or price.
    /// </summary>
    [TestMethod]
    public async Task TheHomePagesJsonLd_Should_DescribeTheCompanyAndTheProduct_WithNothingMadeUp()
    {
        await using var server = await StartPrerenderingServer();

        foreach (var culture in new[] { "en-US", "fa-IR" })
        {
            var html = await GetPage(server, Googlebot, $"/{culture}/");
            var json = JsonLdOf(html);

            foreach (var word in new[] { "PLACEHOLDER", "aggregateRating", "review", "offers", "price", "iso6523Code" })
            {
                Assert.DoesNotContain(word, json, $"The {culture} home page's JSON-LD has no \"{word}\".");
            }

            using var document = JsonDocument.Parse(json);
            var graph = document.RootElement.GetProperty("@graph").EnumerateArray().ToArray();
            Assert.AreSequenceEqual(new[] { "Organization", "WebSite", "SoftwareApplication", "WebPage" }, graph.Select(node => node.GetProperty("@type").GetString()!).ToArray());

            var organization = graph[0];
            Assert.AreEqual("bit Fabric", organization.GetProperty("name").GetString());
            Assert.AreEqual(FabricSite.LegalName, organization.GetProperty("legalName").GetString());
            Assert.AreEqual(FabricSite.VatNumber, organization.GetProperty("vatID").GetString());
            var address = organization.GetProperty("address");
            Assert.AreEqual("PostalAddress", address.GetProperty("@type").GetString());
            Assert.AreEqual("Sixmastraat 15", address.GetProperty("streetAddress").GetString());
            Assert.AreEqual("8932 PA", address.GetProperty("postalCode").GetString());
            Assert.AreEqual("Leeuwarden", address.GetProperty("addressLocality").GetString());
            Assert.AreEqual("NL", address.GetProperty("addressCountry").GetString());
            var kvk = organization.GetProperty("identifier");
            Assert.AreEqual("PropertyValue", kvk.GetProperty("@type").GetString());
            Assert.AreEqual("KVK", kvk.GetProperty("propertyID").GetString());
            Assert.AreEqual("89263723", kvk.GetProperty("value").GetString());

            var software = graph[2];
            Assert.HasCount(9, software.GetProperty("featureList").EnumerateArray().ToArray(), "The features are the page's nine day-one cards.");
            Assert.AreEqual(culture, graph[3].GetProperty("inLanguage").GetString());
        }
    }

    [TestMethod]
    public async Task TheFeaturesPagesJsonLd_Should_NameItsBands_ByTheirAddressesOnThePage()
    {
        await using var server = await StartPrerenderingServer();
        var origin = server.WebAppAddress.ToString().TrimEnd('/');

        var html = await GetFeaturesPage(server, Googlebot);
        using var document = JsonDocument.Parse(JsonLdOf(html));
        var graph = document.RootElement.GetProperty("@graph").EnumerateArray().ToArray();
        Assert.AreSequenceEqual(new[] { "WebPage", "ItemList", "BreadcrumbList" }, graph.Select(node => node.GetProperty("@type").GetString()!).ToArray());

        var bands = graph[1].GetProperty("itemListElement").EnumerateArray().ToArray();
        Assert.IsGreaterThanOrEqualTo(5, bands.Length);
        foreach (var band in bands)
        {
            var url = band.GetProperty("url").GetString()!;
            Assert.StartsWith($"{origin}/en-US{PageUrls.Features}#", url);
            Assert.Contains($"id=\"{url.Split('#')[1]}\"", html, $"{url} leads to a band of the page.");
        }

        var crumbs = graph[2].GetProperty("itemListElement").EnumerateArray().Select(crumb => crumb.GetProperty("item").GetString()).ToArray();
        Assert.AreSequenceEqual(new[] { $"{origin}/en-US/", $"{origin}/en-US{PageUrls.Features}" }, crumbs);
    }

    /// <summary>
    /// Dutch law (BW 3:15d) asks a business's site to show who it is where it is easy to find: the legal page, which every page's
    /// navigation links to (bitfabric-internal/specs/home-page.md, 2026-11-19). The nav panel itself no longer carries the
    /// company's numbers on every page (bitfabric-internal/specs/product.md, Decisions, 2026-11-20).
    /// </summary>
    [TestMethod]
    public async Task TheLegalAboutAndTrustPages_Should_ShowTheCompany_WhileTheNavPanelDoesNot()
    {
        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");

        foreach (var culture in new[] { "en-US", "fa-IR" })
        {
            // The legal page, and the About and Trust pages' company card, show who the company is
            foreach (var path in new[] { $"/{culture}{PageUrls.Terms}", $"/{culture}{PageUrls.Trust}", $"/{culture}{PageUrls.About}" })
            {
                var page = WithoutScripts(await GetPage(server, Googlebot, path));
                foreach (var text in new[] { FabricSite.LegalName, FabricSite.Address, FabricSite.KvkNumber, FabricSite.VatNumber })
                {
                    Assert.IsTrue(Contains(page, text), $"{path} shows \"{text}\".");
                }
                Assert.DoesNotContain("bit platform, Sixmastraat", page, $"{path} names the company by its legal name.");
            }

            foreach (var path in new[] { $"/{culture}/", $"/{culture}{PageUrls.Features}", $"/{culture}{PageUrls.NewProject}" })
            {
                var html = WithoutScripts(await GetPage(server, Googlebot, path));

                Assert.IsFalse(Contains(html, FabricSite.KvkNumber), $"{path} keeps the KvK number off its nav panel.");
                Assert.IsFalse(Contains(html, FabricSite.VatNumber), $"{path} keeps the VAT number off its nav panel.");
            }
        }
    }

    [TestMethod]
    public async Task SecurityTxt_Should_NameTheSitesContact_AndExpireWithinAYear()
    {
        await using var server = await StartPrerenderingServer();
        using var client = NoRedirectClient(server);

        using var response = await client.GetAsync("/.well-known/security.txt", TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(response.Content.Headers.ContentType);
        Assert.AreEqual("text/plain", response.Content.Headers.ContentType.MediaType);
        var fields = (await response.Content.ReadAsStringAsync(TestContext.CancellationToken)).Split('\n')
            .Where(line => line.Length > 0 && line.StartsWith('#') is false)
            .Select(line => line.Split(": ", 2))
            .ToDictionary(field => field[0], field => field[1]);

        Assert.AreEqual($"mailto:{FabricSite.Email}", fields["Contact"]);
        Assert.AreEqual("en, fa", fields["Preferred-Languages"]);
        Assert.AreEqual(new Uri(server.WebAppAddress, ".well-known/security.txt").ToString(), fields["Canonical"]);

        // RFC 9116 asks for less than a year ahead. When this fails, Server.Web/Seo/security.txt needs a new Expires.
        var expires = DateTimeOffset.Parse(fields["Expires"], CultureInfo.InvariantCulture);
        Assert.IsGreaterThan(DateTimeOffset.UtcNow, expires, "security.txt has expired: give Server.Web/Seo/security.txt a new Expires, less than a year ahead.");
        Assert.IsLessThanOrEqualTo(DateTimeOffset.UtcNow.AddYears(1), expires, "Expires is less than a year ahead.");
    }

    /// <summary>
    /// A link on a public page carries the page's culture (bitfabric-internal/specs/home-page.md, 2026-11-19): a culture-less
    /// one redirected a crawler by its own language, to the default culture, so the Persian page's links led it to English
    /// pages, through a redirect.
    /// </summary>
    [TestMethod]
    public async Task TheLinksOfAPublicPage_Should_CarryItsCulture_AndAnswerWithoutARedirect()
    {
        await using var server = await StartPrerenderingServer();
        using var client = NoRedirectClient(server);

        var checkedPages = new HashSet<string>();

        foreach (var culture in new[] { "en-US", "fa-IR" })
        {
            foreach (var page in new[] { PageUrls.Home, PageUrls.Features, PageUrls.Pricing, PageUrls.Trust, PageUrls.Cli, PageUrls.About, PageUrls.Contents, PageUrls.NewProject, PageUrls.SetupProject, PageUrls.Terms }.Concat(ComparePages))
            {
                var path = page is "/" ? $"/{culture}/" : $"/{culture}{page}";
                var html = await GetPage(server, Googlebot, path);

                var links = Regex.Matches(html, "<a\\b[^>]*?\\bhref=\"(?<href>/[^\"]*)\"").Select(match => WebUtility.HtmlDecode(match.Groups["href"].Value))
                    .Where(href => href.StartsWith("//", StringComparison.Ordinal) is false && Path.HasExtension(href.Split('?', '#')[0]) is false)
                    .Distinct()
                    .ToArray();
                Assert.IsGreaterThan(0, links.Length, $"{path} has links of the site's own.");

                foreach (var link in links)
                {
                    Assert.IsTrue(link == $"/{culture}/" || link.StartsWith($"/{culture}/", StringComparison.Ordinal) || link.StartsWith($"/{culture}#", StringComparison.Ordinal),
                        $"{path} links to {link}, which a crawler would be redirected from.");

                    var target = link.Split('?', '#')[0];
                    if (PublicPages.Contains(BarePath(target)) && checkedPages.Add(target))
                    {
                        using var response = await client.GetAsync(target, TestContext.CancellationToken);
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"{target}, linked from {path}, answers.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// A page answers with a trailing slash too, as routing ignores it, but names the address without it as canonical, so it
    /// has one address (bitfabric-internal/specs/home-page.md, 2026-11-19). The home page keeps its slash. Each language also
    /// has a language-only hreflang, so a Spanish reader in Mexico gets the Spanish page rather than x-default's.
    /// </summary>
    [TestMethod]
    public async Task APage_Should_HaveOneAddress_AndALanguageOnlyAlternatePerLanguage()
    {
        await using var server = await StartPrerenderingServer();
        var origin = server.WebAppAddress.ToString().TrimEnd('/');

        foreach (var (path, canonical, bare) in new[]
        {
            ($"/en-US{PageUrls.Features}/", $"{origin}/en-US{PageUrls.Features}", PageUrls.Features),
            ($"/en-US{PageUrls.Features}", $"{origin}/en-US{PageUrls.Features}", PageUrls.Features),
            ("/en-US", $"{origin}/en-US/", "/"),
            ("/en-US/", $"{origin}/en-US/", "/")
        })
        {
            var head = Head(await GetPage(server, Googlebot, path));

            Assert.AreEqual(canonical, Match(head, "<link rel=\"canonical\" href=\"([^\"]*)\""), $"{path}'s canonical");

            var alternates = Regex.Matches(head, "<link rel=\"alternate\" hreflang=\"(?<lang>[^\"]+)\" href=\"(?<href>[^\"]+)\"")
                .ToDictionary(match => match.Groups["lang"].Value, match => match.Groups["href"].Value);

            var afterCulture = bare is "/" ? "/" : bare;
            foreach (var (language, culture) in new[] { ("en", "en-US"), ("nl", "nl-NL"), ("es", "es-ES"), ("ar", "ar-SA"), ("fa", "fa-IR"), ("zh", "zh-CN") })
            {
                Assert.AreEqual($"{origin}/{culture}{afterCulture}", alternates.GetValueOrDefault(language), $"{path}'s {language} alternate");
            }
            Assert.AreEqual($"{origin}/en-GB{afterCulture}", alternates.GetValueOrDefault("en-GB"), $"{path}'s en-GB alternate");
            Assert.AreEqual($"{origin}{bare}", alternates.GetValueOrDefault("x-default"), $"{path}'s x-default");
        }
    }

    private static HttpClient NoRedirectClient(AppTestServer server) =>
        new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = server.WebAppAddress };

    /// <summary>The path without its culture segment: /en-US/features is /features, and /en-US/ is /.</summary>
    private static string BarePath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rest = segments.Length > 0 && CultureInfoManager.GetCultureInfo(segments[0]) is not null ? segments[1..] : segments;
        return $"/{string.Join('/', rest)}";
    }

    private static string Head(string html) => html[..html.IndexOf("</head>", StringComparison.Ordinal)];

    private static string JsonLdOf(string html)
    {
        // Blazor writes the + of the type as &#x2B;, which an HTML parser reads back as +, and a page with scoped styles adds
        // its scope's attribute
        var match = Regex.Match(WebUtility.HtmlDecode(html), "<script type=\"application/ld\\+json\"[^>]*>(?<json>.*?)</script>", RegexOptions.Singleline);
        Assert.IsTrue(match.Success, "The page has JSON-LD.");
        return match.Groups["json"].Value;
    }

    private static string WithoutScripts(string html) => Regex.Replace(html, "<script\\b.*?</script>", string.Empty, RegexOptions.Singleline);
}
