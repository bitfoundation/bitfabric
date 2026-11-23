using System.Net;
using System.Text.Json;
using Bit.Fabric.Client.Core.Components.Pages.Home;
using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Tests.Features.Home;

/// <summary>
/// /cost and /cost.md as a crawler and an AI agent read them (bitfabric-internal/specs/home-page.md, "The cost page",
/// 2026-11-21): the case in its bands, every figure about Anthropic's models with the number of its source, each source with
/// its address and the day it was read, nothing the page must never say, the Markdown saying what the page says, and the
/// ways to it from the pricing page, the home page and every comparison page.
/// </summary>
public partial class HomePageSeoTests
{
    /// <summary>
    /// What the page must never say (bitfabric-internal/.agents/product-marketing.md, Claims;
    /// bitfabric-internal/specs/product.md, 2026-11-20): that building takes months; that its own argued benefits are
    /// unmeasured; the technology underneath; a computer to buy; or a referral.
    /// </summary>
    private static readonly string[] CostNeverSays =
        ["for months", "over months", "haven't measured", "not measured", ".NET", "C#", "laptop", "referral", "affiliate"];

    /// <summary>Anthropic's figures the page quotes, each as written, from AppCostCase's constants and their sources.</summary>
    private static readonly string[] CostFigures =
        ["$2 / $10", "$4 / $20", "$0.11", "$0.12", "7%", "87.6%", "87.4%", "76%", "Sonnet 5.5", "Opus 5.5"];

    [TestMethod]
    public async Task TheCostPage_Should_ArgueTheCase_WithEveryFigureLeadingToADatedSource()
    {
        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");
        var origin = server.WebAppAddress.ToString().TrimEnd('/');
        var localizer = server.WebApp.Services.GetRequiredService<IStringLocalizer<AppStrings>>();

        var html = await GetPage(server, Googlebot, $"/en-US{PageUrls.Cost}");
        var head = Head(html);

        Assert.AreEqual(AppStrings.CostPageTitle, Match(head, "<title>([^<]*)</title>"));
        Assert.AreEqual(AppStrings.CostPageDescription, Meta(head, "name", "description"));
        Assert.Contains($"<link rel=\"alternate\" type=\"text/markdown\" href=\"{origin}{AppCostCase.MarkdownUrl}\"", head, "The page leads agents to its Markdown copy.");
        AssertOneH1(html, AppStrings.CostTitle);
        // One argument in three steps: tokens per task, tokens per project, so the project costs less; then the questions
        // and the sources
        AssertBandsInOrder(html, "per-task", "per-project", "costs-less", "questions", "sources");

        // The hero speaks of the whole project; Opus and Sonnet are the first step's example, never the title's subject
        foreach (var model in new[] { "Opus", "Sonnet" })
        {
            Assert.DoesNotContain(model, AppStrings.CostTitle, $"The title says nothing of {model}.");
            Assert.DoesNotContain(model, AppStrings.CostLead, $"The lead says nothing of {model}.");
            Assert.DoesNotContain(model, AppStrings.CostPageTitle, $"The page's title says nothing of {model}.");
        }

        var text = WebUtility.HtmlDecode(WithoutScripts(html));
        foreach (var fact in CostFigures.Concat(
        [
            AppStrings.CostLead, AppStrings.CostModelsIntro, AppStrings.CostModelsWhy, AppStrings.CostProjectIntro,
            AppStrings.CostBuiltTitle, AppStrings.AppHomeDayOneIdentity, AppStrings.CostRoundsTitle, AppStrings.CostRoundsLine,
            AppStrings.CostLessText, AppStrings.CompareAnswerTokens, AppStrings.CompareAnswerCheaper,
            AppCostCase.TaskLine(localizer), AppCostCase.Earlier(localizer)
        ]))
        {
            Assert.IsTrue(Contains(text, fact), $"The page says \"{fact}\".");
        }

        // The conclusion leads to the bill, on the pricing page
        var conclusion = Regex.Match(html, "<section\\b[^>]*\\bid=\"ah-costs-less\".*?</section>", RegexOptions.Singleline).Value;
        Assert.Contains($"href=\"/en-US{PageUrls.Pricing}\"", conclusion, "The conclusion leads to the bill on /pricing.");
        foreach (var word in CostNeverSays)
        {
            Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase, $"The page says nothing of \"{word}\".");
        }

        // The sources, in the order the page first cites them: each numbered, with its name, its address, the day it was read,
        // and the day it was published when it says
        var sources = AppCostCase.Sources(localizer);
        var entries = Regex.Matches(html, "<li\\b[^>]*\\bid=\"source-(?<number>\\d+)\"[^>]*>(?<body>.*?)</li>", RegexOptions.Singleline)
            .Select(match => (Number: int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture), Body: WebUtility.HtmlDecode(match.Groups["body"].Value)))
            .ToArray();
        Assert.AreSequenceEqual(Enumerable.Range(1, sources.Length).ToArray(), entries.Select(entry => entry.Number).ToArray(), "Every source is listed once, numbered in order.");
        for (var i = 0; i < sources.Length; i++)
        {
            var (number, body) = entries[i];
            var source = sources[i];
            Assert.StartsWith("https://", source.Url, $"{source.Id}'s address");
            Assert.Contains(source.Name, body, $"Source {number} has its name.");
            Assert.Contains($"href=\"{source.Url}\"", body, $"Source {number} links {source.Url}.");
            Assert.Contains(string.Format(AppStrings.CompareReadOn, AppStrings.CostReadDate), body, $"Source {number} says the day it was read.");
            if (source.PublishedOn is not null)
            {
                Assert.Contains(string.Format(AppStrings.CostPublishedOn, source.PublishedOn), body, $"Source {number} says the day it was published.");
            }
        }
        Assert.AreEqual("24 November 2025", sources.Single(source => source.Id == "ANT-1").PublishedOn, "Opus 4.5's launch post is dated as Anthropic dated it.");

        // The numbers beside the figures: each leads to the entry with the same number, on this page, and every source is cited
        var refs = Regex.Matches(html, "<a\\b(?<attrs>[^>]*)>\\s*<sup\\b[^>]*>(?<number>\\d+)</sup>\\s*</a>")
            .Select(match => (Href: WebUtility.HtmlDecode(Regex.Match(match.Groups["attrs"].Value, "href=\"(?<href>[^\"]*)\"").Groups["href"].Value), Number: int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture)))
            .ToArray();
        foreach (var (href, number) in refs)
        {
            Assert.AreEqual($"/en-US{PageUrls.Cost}#source-{number}", href, $"Number {number} leads to its source, on this page.");
        }
        var cited = refs.Select(reference => reference.Number).ToArray();
        foreach (var number in Enumerable.Range(1, sources.Length))
        {
            Assert.Contains(number, cited, $"The page cites source {number}.");
        }

        // A WebPage about bit Fabric that cites the pages it quotes, and the way back to the home page
        using var document = JsonDocument.Parse(JsonLdOf(html));
        var graph = AssertJsonLdOfAPage(document, origin, PageUrls.Cost, "WebPage", "BreadcrumbList");
        Assert.AreEqual($"{origin}/#software", graph[0].GetProperty("about").GetProperty("@id").GetString(), "The page is about bit Fabric.");
        Assert.AreEqual("2026-11-21", graph[0].GetProperty("dateModified").GetString(), "The day its sources were read.");
        Assert.AreSequenceEqual(sources.Select(source => source.Url).ToArray(),
            graph[0].GetProperty("citation").EnumerateArray().Select(citation => citation.GetProperty("url").GetString()!).ToArray(), "It cites every source.");

        // The Persian page is a page of its own, in Persian, and says nothing it mustn't either
        var persian = WebUtility.HtmlDecode(await GetPage(server, Googlebot, $"/fa-IR{PageUrls.Cost}"));
        Assert.Contains("<html lang=\"fa-IR\"", persian);
        Assert.Contains(AppStrings.ResourceManager.GetString(nameof(AppStrings.CostLead), new CultureInfo("fa-IR"))!, persian, "The Persian page has its Persian lead.");
        foreach (var word in CostNeverSays)
        {
            Assert.DoesNotContain(word, WithoutScripts(persian), StringComparison.OrdinalIgnoreCase, $"The Persian page says nothing of \"{word}\".");
        }

        // The ways to it: the example month on /pricing, the home page's cost band, and every comparison page's and agent page's cost band
        foreach (var page in new[] { PageUrls.Home, PageUrls.Pricing }.Concat(ComparePages).Concat(ForPages.Skip(1)))
        {
            var from = await GetPage(server, Googlebot, $"/en-US{page}");
            Assert.Contains($"href=\"/en-US{PageUrls.Cost}\"", from, $"/en-US{page} leads to the cost page.");
        }
    }

    /// <summary>
    /// /cost.md is the page as Markdown for AI agents, written by the code that renders the page from the same texts and the same
    /// figures, so the two agree: every figure is in both, each source is listed with its address, and the links are this host's.
    /// </summary>
    [TestMethod]
    public async Task CostMd_Should_SayWhatTheCostPageSays()
    {
        await using var server = await StartPrerenderingServer();
        using var client = NoRedirectClient(server);
        var origin = server.WebAppAddress.ToString().TrimEnd('/');

        using var response = await client.GetAsync(AppCostCase.MarkdownUrl, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(response.Content.Headers.ContentType);
        Assert.AreEqual("text/markdown", response.Content.Headers.ContentType.MediaType);
        var markdown = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");
        var html = WebUtility.HtmlDecode(await GetPage(server, Googlebot, $"/en-US{PageUrls.Cost}"));
        var localizer = server.WebApp.Services.GetRequiredService<IStringLocalizer<AppStrings>>();

        Assert.StartsWith($"# {AppStrings.CostTitle}\n", markdown, "It opens with the page's h1.");
        // The three steps, in the page's order
        Assert.Contains($"## 1. {AppStrings.CostModelsTitle}\n", markdown, "Step 1 is a section of its own.");
        Assert.IsLessThan(markdown.IndexOf($"## 2. {AppStrings.CostProjectTitle}", StringComparison.Ordinal), markdown.IndexOf($"## 1. {AppStrings.CostModelsTitle}", StringComparison.Ordinal), "Tokens per task come first.");
        Assert.IsLessThan(markdown.IndexOf($"## 3. {AppStrings.CostLessTitle}", StringComparison.Ordinal), markdown.IndexOf($"## 2. {AppStrings.CostProjectTitle}", StringComparison.Ordinal), "Then tokens per project, then the conclusion.");
        Assert.Contains($"[{AppStrings.CostLessLink}]({origin}/en-US{PageUrls.Pricing})", markdown, "The conclusion leads to the bill.");

        foreach (var text in CostFigures.Concat([AppStrings.CostLead, AppStrings.CostRoundsLine, AppStrings.CostModelsWhy, AppStrings.CostProjectIntro, AppStrings.CostLessText, AppCostCase.TaskLine(localizer), AppCostCase.Earlier(localizer), AppStrings.CompareAnswerTokens]))
        {
            Assert.Contains(text, markdown, $"The Markdown says \"{text}\".");
            Assert.Contains(text, html, $"The page says \"{text}\" too.");
        }
        foreach (var (source, number) in AppCostCase.Sources(localizer).Select((source, i) => (source, i + 1)))
        {
            Assert.Contains($"{number}. [{source.Name}]({source.Url})", markdown, $"Source {number} is listed with its address.");
            Assert.Contains($" [{number}]", markdown, $"Source {number} is cited.");
        }
        Assert.Contains(string.Format(AppStrings.CostSourcesIntro, AppStrings.CostReadDate), markdown, "It says the day the sources were read.");

        Assert.Contains($"{origin}/en-US{PageUrls.Cost}", markdown, "It names the page, on this host.");
        Assert.Contains($"({origin}/en-US{PageUrls.Pricing})", markdown, "It links the pricing page, on this host.");
        Assert.Contains($"({origin}/en-US{PageUrls.NewProject})", markdown, "It links Get started, on this host.");
        foreach (var word in CostNeverSays.Append("{{").Append("PLACEHOLDER"))
        {
            Assert.DoesNotContain(word, markdown, StringComparison.OrdinalIgnoreCase, $"The Markdown says nothing of \"{word}\".");
        }
    }
}
