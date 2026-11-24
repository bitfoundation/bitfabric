using System.Net;
using System.Text.Json;
using Bit.Fabric.Client.Core.Components;
using Bit.Fabric.Client.Core.Components.Pages.Home;
using Bit.Fabric.Client.Core.Components.Pages.Home.Parts;
using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Tests.Features.Home;

/// <summary>
/// /pricing, /pricing.md, /trust and /about as a crawler and an AI agent read them (bitfabric-internal/specs/home-page.md,
/// "The pricing and trust pages" and "The About page", 2026-11-19): each page answers with its own title and description,
/// one h1, its bands in order, its facts in the HTML and JSON-LD with nothing made up; the Markdown says what the page says;
/// and the trust page's table is bitfabric-internal/specs/product.md's lists.
/// </summary>
public partial class HomePageSeoTests
{
    /// <summary>What no JSON-LD of the site may carry: a placeholder, a rating or review there are none of, or an offer, whose
    /// price of 0 would read as the whole thing being free (bitfabric-internal/.agents/product-marketing.md, Claims).</summary>
    private static readonly string[] MadeUp = ["PLACEHOLDER", "aggregateRating", "review", "offers", "price", "iso6523Code"];

    [TestMethod]
    public async Task ThePricingPage_Should_ShowThePlans_AnExampleMonth_AndWhatYouPayOthers()
    {
        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");
        var origin = server.WebAppAddress.ToString().TrimEnd('/');

        var html = await GetPage(server, Googlebot, $"/en-US{PageUrls.Pricing}");
        var head = Head(html);

        Assert.AreEqual(AppStrings.PricingPageTitle, Match(head, "<title>([^<]*)</title>"));
        Assert.AreEqual(AppStrings.PricingPageDescription, Meta(head, "name", "description"));
        Assert.Contains($"<link rel=\"alternate\" type=\"text/markdown\" href=\"{origin}{AppPricing.MarkdownUrl}\"", head, "The page leads agents to its Markdown copy.");
        AssertOneH1(html, AppStrings.PricingTitle);
        AssertBandsInOrder(html, "plans", "examples", "saving", "license", "always", "others", "questions");

        var localizer = server.WebApp.Services.GetRequiredService<IStringLocalizer<AppStrings>>();
        foreach (var text in PricingFacts(localizer))
        {
            Assert.IsTrue(Contains(html, text), $"\"{text}\" is in the page.");
        }
        // What you pay others is the home page's own calculator, not a copy of its numbers
        Assert.IsTrue(Contains(html, AppStrings.AppHomeCostFreeTitle), "The calculator is on the page.");
        Assert.Contains("Copilot Pro $10", WebUtility.HtmlDecode(html), "The visitor's AI plan is priced, with its date.");

        // Never as if building took months, a computer had to be bought, or a referral paid anyone
        // (bitfabric-internal/specs/product.md, 2026-11-20)
        var words = WebUtility.HtmlDecode(WithoutScripts(html));
        foreach (var claim in new[] { "for months", "over months", "laptop", "$700", "referral", "affiliate" })
        {
            Assert.DoesNotContain(claim, words, StringComparison.OrdinalIgnoreCase, $"The page says nothing of \"{claim}\".");
        }

        using var document = JsonDocument.Parse(JsonLdOf(html));
        var graph = AssertJsonLdOfAPage(document, origin, PageUrls.Pricing, "WebPage", "BreadcrumbList");
        Assert.AreEqual($"{origin}/#software", graph[0].GetProperty("about").GetProperty("@id").GetString(), "The page is about bit Fabric.");

        // The Persian page is a page of its own, in Persian
        var persian = await GetPage(server, Googlebot, $"/fa-IR{PageUrls.Pricing}");
        Assert.Contains("<html lang=\"fa-IR\"", persian);
        Assert.Contains(AppStrings.ResourceManager.GetString(nameof(AppStrings.PricingLead), new CultureInfo("fa-IR"))!, WebUtility.HtmlDecode(persian), "The Persian page has its Persian lead.");
    }

    /// <summary>
    /// /pricing.md is the page as Markdown for AI agents (llmstxt.org), written by the code that renders the page from the same
    /// texts and the same bill, so the two agree: every fact of the page is in it, and every line of the calculator with its price.
    /// </summary>
    [TestMethod]
    public async Task PricingMd_Should_SayWhatThePricingPageSays()
    {
        await using var server = await StartPrerenderingServer();
        using var client = NoRedirectClient(server);
        var origin = server.WebAppAddress.ToString().TrimEnd('/');

        using var response = await client.GetAsync(AppPricing.MarkdownUrl, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(response.Content.Headers.ContentType);
        Assert.AreEqual("text/markdown", response.Content.Headers.ContentType.MediaType);
        var markdown = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");
        var html = WebUtility.HtmlDecode(await GetPage(server, Googlebot, $"/en-US{PageUrls.Pricing}"));
        var localizer = server.WebApp.Services.GetRequiredService<IStringLocalizer<AppStrings>>();

        Assert.StartsWith($"# {AppStrings.PricingTitle}\n", markdown, "It opens with the page's h1.");
        foreach (var text in PricingFacts(localizer).Append(AppStrings.AppHomeCostMinimum))
        {
            Assert.Contains(text, markdown, $"The Markdown says \"{text}\".");
            Assert.Contains(text, html, $"The page says \"{text}\" too.");
        }

        var bill = AppHomeCostCalculator.PaidOf(localizer);
        foreach (var item in bill)
        {
            Assert.Contains($"**{item.Title}**", markdown, $"The bill's {item.Title} is in the Markdown.");
            Assert.Contains($"€{item.Euros} {AppStrings.AppHomeCostPerMonth}", markdown, $"The bill's {item.Title} has its price.");
        }
        Assert.Contains($"**€{bill.Where(item => item.StartsOn).Sum(item => item.Euros)} {AppStrings.AppHomeCostPerMonth}**", markdown, "It totals the bill as the calculator starts.");

        Assert.Contains($"({origin}/en-US{PageUrls.Pricing})", markdown, "It links the page, on this host.");
        Assert.Contains($"({origin}/en-US{PageUrls.NewProject})", markdown, "It links Get started, on this host.");
        foreach (var word in new[] { "{{", "PLACEHOLDER", ".NET", "laptop", "referral" })
        {
            Assert.DoesNotContain(word, markdown, $"The Markdown says nothing of \"{word}\".");
        }
    }

    [TestMethod]
    public async Task TheTrustPage_Should_SayWhoMakesBitFabric_AndHowToCheckIt()
    {
        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");
        var origin = server.WebAppAddress.ToString().TrimEnd('/');

        var html = await GetPage(server, Googlebot, $"/en-US{PageUrls.Trust}");
        var head = Head(html);

        Assert.AreEqual(AppStrings.TrustPageTitle, Match(head, "<title>([^<]*)</title>"));
        Assert.AreEqual(AppStrings.TrustPageDescription, Meta(head, "name", "description"));
        AssertOneH1(html, AppStrings.TrustPageTitle);
        AssertBandsInOrder(html, "who", "what-we-see", "where", "if-we-stop", "check", "security", "privacy");

        var text = WebUtility.HtmlDecode(WithoutScripts(html));
        foreach (var fact in new[]
        {
            FabricSite.LegalName, FabricSite.Address, FabricSite.KvkNumber, FabricSite.VatNumber, FabricSite.Email,
            AppHomeNumbers.GitHubStars, AppHomeNumbers.Contributors, AppHomeNumbers.Downloads, AppHomeNumbers.ImprovementsAMonth,
            AppStrings.TrustWhereCloudflare, AppStrings.AppHomeIfWeStopTitle, AppStrings.AppHomeStopsHealth,
            AppStrings.NewProjectSafeSigned, "gh attestation verify <bit.dll> --repo bitfoundation/bitfabric"
        })
        {
            Assert.Contains(fact, text, $"The page says \"{fact}\".");
        }
        Assert.Contains($"href=\"mailto:{FabricSite.Email}\"", html, "A security problem is reported to the site's contact.");
        Assert.Contains($"href=\"{origin}/.well-known/security.txt\"", html, "The page links security.txt, which names the same contact.");
        Assert.Contains($"href=\"/en-US{PageUrls.Terms}\"", html, "The page leads to the privacy notice and terms.");

        // What isn't in place yet isn't on the page (bitfabric-internal/specs/home-page.md, 2026-11-19)
        foreach (var claim in new[] { "data processing agreement", "no tracking", "certified", "ISO 27001", "SOC 2", "sub-processor" })
        {
            Assert.DoesNotContain(claim, text, StringComparison.OrdinalIgnoreCase, $"The page claims nothing of \"{claim}\".");
        }

        using var document = JsonDocument.Parse(JsonLdOf(html));
        var graph = AssertJsonLdOfAPage(document, origin, PageUrls.Trust, "Organization", "WebPage", "BreadcrumbList");
        var organization = graph[0];
        Assert.AreEqual($"{origin}/#organization", organization.GetProperty("@id").GetString(), "The same company the home page describes.");
        Assert.AreEqual(FabricSite.LegalName, organization.GetProperty("legalName").GetString());
        Assert.AreEqual(FabricSite.VatNumber, organization.GetProperty("vatID").GetString());
        Assert.AreEqual(FabricSite.KvkNumber, organization.GetProperty("identifier").GetProperty("value").GetString());
        Assert.AreEqual($"{origin}/#organization", graph[1].GetProperty("about").GetProperty("@id").GetString(), "The page is about the company.");
    }

    /// <summary>
    /// The trust page's table, row by row: what bit Fabric never sees, what the wizard keeps (nothing), and what it sees
    /// only with permission. The lists below are bitfabric-internal/specs/product.md's Trust and privacy as written there;
    /// change them together. Each row has the words of its line.
    /// </summary>
    [TestMethod]
    public async Task TheTrustPagesTable_Should_ListWhatBitFabricSeesAndNeverSees()
    {
        (string Spec, string[] Words)[] sees =
        [
            ("The email and account, if they create one.", ["email", "account", "Only if you create one"]),
            ("If they connect monitoring, the health endpoint's answer: the status and response time of each dependency, not the data behind it. Never its error texts: they stay in the app.",
                ["health", "Only if you connect monitoring", "status and response time", "never the data behind it or its error texts"]),
            ("If they ask for scans, what any public visitor sees: headers, the HTML of public pages, load times.", ["visitor", "Only if you ask for scans", "headers", "HTML of public pages", "load times"]),
            ("bit CLI's own errors and step timings, without project content, on by default and switched off with `bit telemetry off`.",
                ["bit CLI", "Errors and step timings", "Without any project content", "On by default", "bit telemetry off"])
        ];
        (string Spec, string[] Words)[] never =
        [
            ("the source code and its Git history", ["code", "Git history"]),
            ("prompts and conversations with the AI", ["prompts", "conversations"]),
            ("database rows, files and end users' data", ["database", "files", "users' data"]),
            ("the project's keys and secrets", ["keys and secrets"]),
            ("the app's logs and errors", ["logs and errors", "Sentry or Application Insights"]),
            ("**What the wizard keeps: nothing**", ["name and the options", "Never kept"])
        ];

        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");
        var html = WebUtility.HtmlDecode(await GetPage(server, Googlebot, $"/en-US{PageUrls.Trust}"));

        string[] RowsOf(string kind) => [.. Regex.Matches(html, $"<tr[^>]*class=\"{kind}\"[^>]*>(?<row>.*?)</tr>", RegexOptions.Singleline)
            .Select(match => Regex.Replace(match.Groups["row"].Value, "<[^>]+>", " "))];

        var neverRows = RowsOf("tr-never");
        var onlyRows = RowsOf("tr-only");
        Assert.HasCount(never.Length, neverRows, "A row for each thing bit Fabric never sees or keeps.");
        Assert.HasCount(sees.Length, onlyRows, "A row for each thing bit Fabric sees, and only with permission.");

        foreach (var ((line, words), row) in never.Zip(neverRows).Concat(sees.Zip(onlyRows)))
        {
            foreach (var word in words)
            {
                Assert.Contains(word, row, StringComparison.OrdinalIgnoreCase, $"The row for \"{line}\" says \"{word}\".");
            }
        }
        foreach (var row in neverRows)
        {
            Assert.Contains(AppStrings.TrustNever, row, $"\"{row.Trim()}\" says never.");
        }
    }

    /// <summary>
    /// /about as a crawler reads it (bitfabric-internal/specs/home-page.md, "The About page", 2026-11-19): its own title and
    /// description, one h1, the warp-and-weft story and the principles (bitfabric-internal/specs/product.md) in the HTML, who
    /// makes it with the company's details, the ways on to /trust, /pricing, the terms and the licenses, JSON-LD with nothing
    /// made up, and the version of the app at the end, as the installed apps show it. It names nothing of the technology
    /// underneath, which the template's developer page it replaces did (bitfabric-internal/specs/product.md, Q09).
    /// </summary>
    [TestMethod]
    public async Task TheAboutPage_Should_TellTheStoryAndThePrinciples_AndNameNothingOfTheTechnologyUnderneath()
    {
        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");
        var origin = server.WebAppAddress.ToString().TrimEnd('/');

        using var client = NoRedirectClient(server);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(Googlebot);
        using var response = await client.GetAsync($"/en-US{PageUrls.About}", TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        var head = Head(html);

        Assert.AreEqual(AppStrings.AboutBitFabricPageTitle, Match(head, "<title>([^<]*)</title>"));
        Assert.AreEqual(AppStrings.AboutBitFabricPageDescription, Meta(head, "name", "description"));
        Assert.AreEqual($"{origin}/en-US{PageUrls.About}", Match(head, "<link rel=\"canonical\" href=\"([^\"]*)\""));
        AssertOneH1(html, AppStrings.AboutTitle);
        AssertBandsInOrder(html, "story", "principles", "who", "this-app");

        var text = WebUtility.HtmlDecode(WithoutScripts(html));
        foreach (var fact in new[]
        {
            AppStrings.AboutLead, AppStrings.AboutStoryIntro,
            AppStrings.AboutWarpTitle, AppStrings.AboutWarpText, AppStrings.AboutWeftTitle, AppStrings.AboutWeftText, AppStrings.AboutFabricTitle, AppStrings.AboutFabricText,
            AppStrings.AboutPrinciplesIntro,
            AppStrings.AboutPrincipleYoursTitle, AppStrings.AboutPrincipleYoursText,
            AppStrings.AboutPrincipleProductFirstTitle, AppStrings.AboutPrincipleProductFirstText,
            AppStrings.AboutPrincipleQuietTitle, AppStrings.AboutPrincipleQuietText,
            AppStrings.AboutPrincipleExplainTitle, AppStrings.AboutPrincipleExplainText,
            AppStrings.AboutPrincipleWordsTitle, AppStrings.AboutPrincipleWordsText,
            AppStrings.AboutPrincipleReadOnlyTitle, AppStrings.AboutPrincipleReadOnlyText,
            AppStrings.AboutPrincipleCostTitle, AppStrings.AboutPrincipleCostText,
            AppStrings.AboutPrincipleSafeTitle, AppStrings.AboutPrincipleSafeText,
            AppStrings.AboutPrincipleReplaceableTitle, AppStrings.AboutPrincipleReplaceableText,
            string.Format(AppStrings.AboutWhoIntro, FabricSite.LegalName),
            FabricSite.Address, FabricSite.KvkNumber, FabricSite.VatNumber, FabricSite.Email,
            AppHomeNumbers.GitHubStars, AppHomeNumbers.Contributors, AppHomeNumbers.Downloads, AppHomeNumbers.ImprovementsAMonth,
            // What a user of an installed app needs from the page: the app's version, under a plain title
            AppStrings.AboutThisAppTitle, $"Version {typeof(AboutBitFabric).Assembly.GetName().Version!.ToString(3)}"
        })
        {
            Assert.Contains(fact, text, $"The page says \"{fact}\".");
        }

        foreach (var page in new[] { PageUrls.Trust, PageUrls.Pricing, PageUrls.Terms, PageUrls.OpenSourceLicenses })
        {
            Assert.Contains($"href=\"/en-US{page}\"", html, $"The page leads to {page}.");
        }

        // Nothing of the template's developer page, nor of the technology underneath, in anything the page says: its text, what
        // its attributes read out (a picture's alt, a label, a tooltip, the head's meta tags) and its JSON-LD. The framework's
        // own markup around it, its comments and the addresses of its scripts and styles, are no copy, so they don't count.
        var says = string.Join('\n',
            Regex.Replace(Regex.Replace(WithoutScripts(html), "<!--.*?-->|<style\\b.*?</style>", " ", RegexOptions.Singleline), "<[^>]+>", " "),
            string.Join('\n', Regex.Matches(html, "\\s(?:alt|aria-label|aria-description|title|content|placeholder)=\"(?<value>[^\"]*)\"").Select(match => match.Groups["value"].Value)),
            JsonLdOf(html));
        foreach (var word in new[] { ".NET", "C#", "Blazor", "NuGet", ".razor", "Bit.Fabric" })
        {
            Assert.DoesNotContain(word, WebUtility.HtmlDecode(says), $"The page says nothing of \"{word}\".");
        }

        using var document = JsonDocument.Parse(JsonLdOf(html));
        var graph = AssertJsonLdOfAPage(document, origin, PageUrls.About, "Organization", "AboutPage", "BreadcrumbList");
        Assert.AreEqual($"{origin}/#organization", graph[0].GetProperty("@id").GetString(), "The same company the home page describes.");
        Assert.AreEqual(FabricSite.LegalName, graph[0].GetProperty("legalName").GetString());
        Assert.AreEqual($"{origin}/#organization", graph[1].GetProperty("about").GetProperty("@id").GetString(), "The page is about the company.");

        // The Persian page is a page of its own, in Persian
        var persian = await GetPage(server, Googlebot, $"/fa-IR{PageUrls.About}");
        Assert.Contains("<html lang=\"fa-IR\"", persian);
        Assert.Contains(AppStrings.ResourceManager.GetString(nameof(AppStrings.AboutTitle), new CultureInfo("fa-IR"))!, WebUtility.HtmlDecode(persian), "The Persian page has its Persian title.");
    }

    /// <summary>
    /// The open-source licenses the About page used to carry are a page of their own (bitfabric-internal/specs/home-page.md,
    /// 2026-11-19), which only the app sends people to, so search engines leave it out.
    /// </summary>
    [TestMethod]
    public async Task TheOpenSourceLicenses_Should_BeOnAPageOfTheirOwn_ThatSearchEnginesLeaveOut()
    {
        await using var server = await StartPrerenderingServer();
        using var client = NoRedirectClient(server);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(Googlebot);

        using var response = await client.GetAsync($"/en-US{PageUrls.OpenSourceLicenses}", TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(response.Headers.TryGetValues("X-Robots-Tag", out var robots) && robots.Contains("noindex"), "Search engines leave it out.");
        var html = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        Assert.IsTrue(Contains(html, "Standing on the shoulders of giants"), "The acknowledgements are on it.");
        Assert.IsTrue(Contains(html, "Hangfire"), "Each with its name.");
    }

    /// <summary>What /pricing says, which /pricing.md has to say too: today, what stays true, the AI plan and the questions with their answers.</summary>
    private static IEnumerable<string> PricingFacts(IStringLocalizer<AppStrings> localizer) =>
        [
            AppStrings.PricingLead,
            AppStrings.PricingPlansNote,
            .. AppPricing.Plans(localizer).SelectMany(plan => new[] { plan.Name, plan.Audience, plan.Price }.Concat(plan.Includes)),
            .. AppPricing.Examples(localizer).SelectMany(tile => new[] { tile.Title, tile.Text, tile.Value! }),
            .. AppPricing.Saving(localizer).SelectMany(line => new[] { line.Label, line.FabricAmount, line.FabricPaidTo }),
            AppPricing.Euros(AppPricing.ExampleBuilderTotal),
            AppPricing.Euros(AppPricing.ExampleFabricTotal),
            .. AppPricing.SavingWhy(localizer),
            AppStrings.PricingSavingHonest,
            .. AppPricing.License(localizer).SelectMany(tile => new[] { tile.Title, tile.Text }),
            .. AppPricing.Always(localizer).SelectMany(tile => new[] { tile.Title, tile.Text }),
            AppStrings.PricingAiPlanText,
            .. AppPricing.Questions(localizer).SelectMany(question => new[] { question.Question, question.Answer })
        ];

    private static void AssertOneH1(string html, string title)
    {
        var h1s = Regex.Matches(html, "<h1[ >].*?</h1>", RegexOptions.Singleline);
        Assert.HasCount(1, h1s, "The page has one h1.");
        // Its words, whatever the elements inside it, such as a name kept on one line
        Assert.Contains(title, WebUtility.HtmlDecode(Regex.Replace(h1s[0].Value, "<[^>]+>", string.Empty)), "The h1 is the page's title.");
    }

    private static void AssertBandsInOrder(string html, params string[] bands)
    {
        var at = bands.Select(band => (Band: band, At: Regex.Match(html, $"<h2[^>]*\\bid=\"ah-{band}-title\"").Index)).ToArray();
        foreach (var (band, index) in at)
        {
            Assert.IsGreaterThan(0, index, $"The {band} band's title is an h2.");
        }
        Assert.AreSequenceEqual(at.Select(b => b.Band).ToArray(), at.OrderBy(b => b.At).Select(b => b.Band).ToArray(), "The bands come in their order.");
    }

    /// <summary>The page's JSON-LD: its nodes' types in order, nothing made up, and the breadcrumb from the home page to it.</summary>
    private static JsonElement[] AssertJsonLdOfAPage(JsonDocument document, string origin, string page, params string[] types)
    {
        var json = document.RootElement.GetRawText();
        foreach (var word in MadeUp)
        {
            Assert.DoesNotContain(word, json, $"The JSON-LD of {page} has no \"{word}\".");
        }

        var graph = document.RootElement.GetProperty("@graph").EnumerateArray().ToArray();
        Assert.AreSequenceEqual(types, graph.Select(node => node.GetProperty("@type").GetString()!).ToArray());

        var webPage = graph.Single(node => node.GetProperty("@type").GetString() is "WebPage" or "AboutPage");
        Assert.AreEqual($"{origin}/en-US{page}", webPage.GetProperty("url").GetString());
        Assert.AreEqual("en-US", webPage.GetProperty("inLanguage").GetString());

        var crumbs = graph.Single(node => node.GetProperty("@type").GetString() == "BreadcrumbList").GetProperty("itemListElement").EnumerateArray()
            .Select(crumb => crumb.GetProperty("item").GetString()).ToArray();
        Assert.AreSequenceEqual(new[] { $"{origin}/en-US/", $"{origin}/en-US{page}" }, crumbs);

        return graph;
    }
}
