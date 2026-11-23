using System.Net;
using System.Text.Json;
using Bit.Fabric.Client.Core.Components.Pages.Home.Compare;
using Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;
using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Tests.Features.Home;

/// <summary>
/// /compare and its pages as a crawler reads them (bitfabric-internal/specs/home-page.md, "The comparison pages",
/// 2026-11-19): each answers at its own address with its own title, description, one h1, its table and its numbered
/// Sources list, every number in it leads to a source the list has, each source has its address and the day it was read,
/// and the JSON-LD has real values only. No page says what these pages must not: "backend", "your code" (no builder keeps
/// it), ".NET" or "C#" (bitfabric-internal/specs/product.md, Q09) or "unlike" (no competitor is put down). An unknown
/// subject answers 404.
/// </summary>
public partial class HomePageSeoTests
{
    /// <summary>The hub and a page per subject, each a literal route of ComparePage.</summary>
    private static readonly string[] ComparePages =
        [PageUrls.Compare, PageUrls.CompareLovable, PageUrls.CompareBolt, PageUrls.CompareBase44, PageUrls.CompareReplit, PageUrls.CompareV0, PageUrls.CompareEmptyProject, PageUrls.CompareStarterKits];

    private const string CheckedOn = "19 November 2026";

    [TestMethod]
    public async Task EachComparisonPage_Should_AnswerWithItsTitleItsTableAndItsSources()
    {
        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");
        var origin = server.WebAppAddress.ToString().TrimEnd('/');
        var localizer = server.WebApp.Services.GetRequiredService<IStringLocalizer<AppStrings>>();

        foreach (var subject in SubjectsOf(localizer))
        {
            var page = subject.Url;
            var html = await GetPage(server, Googlebot, $"/en-US{page}");
            var head = Head(html);
            var text = WebUtility.HtmlDecode(WithoutScripts(html));

            Assert.AreEqual(subject.PageTitle, Match(head, "<title>([^<]*)</title>"), $"{page}'s title");
            Assert.AreEqual(subject.Description, Meta(head, "name", "description"), $"{page}'s description");
            Assert.AreEqual($"{origin}/en-US{page}", Match(head, "<link rel=\"canonical\" href=\"([^\"]*)\""), $"{page} is its own canonical address.");
            AssertOneH1(html, subject.Title);
            Assert.Contains(subject.Answer, text, $"{page} answers first.");
            Assert.Contains(string.Format(AppStrings.CompareCheckedOn, CheckedOn), text, $"{page} says when it was checked.");

            string[] bands = [
                "side-by-side",
                .. page == PageUrls.Compare ? ["one-by-one"] : Array.Empty<string>(),
                .. subject.Extra is null ? Array.Empty<string>() : ["more-than-code"],
                "by-design", "cost", "questions", "sources"];
            AssertBandsInOrder(html, bands);

            // The table: its columns, bit Fabric's last, what the other does well as its first row, and every fact
            var table = Regex.Match(html, "<table\\b[^>]*\\bcmp-table\\b.*?</table>", RegexOptions.Singleline);
            Assert.IsTrue(table.Success, $"{page} has its table.");
            var tableText = WebUtility.HtmlDecode(table.Value);
            Assert.AreEqual("bit Fabric", subject.Columns[^1].Name, $"{page}'s last column is bit Fabric's.");
            foreach (var column in subject.Columns)
            {
                Assert.Contains(column.Name, tableText, $"{page}'s table has the column \"{column.Name}\".");
            }
            Assert.AreEqual(CompareTopic.DoesWell, subject.Rows[0].Topic, $"{page}'s first row is what each does well.");
            var rowHeaders = Regex.Matches(table.Value, "<th scope=\"row\"[^>]*>(?<label>[^<]*)</th>").Select(match => WebUtility.HtmlDecode(match.Groups["label"].Value)).ToArray();
            Assert.AreSequenceEqual(subject.Rows.Select(row => CompareShared.TopicLabel(localizer, row.Topic)).ToArray(), rowHeaders, $"{page}'s rows, in order.");
            foreach (var fact in subject.Rows.SelectMany(row => row.Cells))
            {
                Assert.Contains(fact.Text, tableText, $"{page}'s table says \"{fact.Text}\".");
            }
            Assert.Contains(subject.SourceLine, text, $"{page} says where its facts come from.");

            // How bit Fabric is built on purpose, what it costs with a way on to /pricing, and the questions, their answers in the page
            foreach (var gap in subject.Gaps)
            {
                Assert.Contains(gap.Title, text, $"{page} says that bit Fabric is built this way: \"{gap.Title}\".");
            }
            Assert.Contains(AppStrings.CompareCostText, text, $"{page} says what bit Fabric costs.");
            Assert.Contains($"href=\"/en-US{PageUrls.Pricing}\"", html, $"{page} leads to /pricing rather than restating it.");
            Assert.IsGreaterThanOrEqualTo(3, subject.Questions.Length, $"{page} has three to five questions.");
            Assert.IsLessThanOrEqualTo(5, subject.Questions.Length, $"{page} has three to five questions.");
            foreach (var question in subject.Questions)
            {
                Assert.Contains(question.Question, text, $"{page} asks \"{question.Question}\".");
                Assert.Contains(question.Answer, text, $"{page} answers \"{question.Question}\" in the page.");
            }
            Assert.Contains($"href=\"/en-US{PageUrls.NewProject}\"", html, $"{page} ends with Get started.");

            // JSON-LD with real values only: the page, citing its sources, and the way back through /compare
            using var document = JsonDocument.Parse(JsonLdOf(html));
            var json = document.RootElement.GetRawText();
            foreach (var word in MadeUp)
            {
                Assert.DoesNotContain(word, json, $"The JSON-LD of {page} has no \"{word}\".");
            }
            var graph = document.RootElement.GetProperty("@graph").EnumerateArray().ToArray();
            Assert.AreSequenceEqual(new[] { "WebPage", "BreadcrumbList" }, graph.Select(node => node.GetProperty("@type").GetString()!).ToArray(), $"{page}'s JSON-LD");
            var webPage = graph[0];
            Assert.AreEqual($"{origin}/en-US{page}", webPage.GetProperty("url").GetString());
            Assert.AreEqual("en-US", webPage.GetProperty("inLanguage").GetString());
            Assert.AreEqual("2026-11-19", webPage.GetProperty("dateModified").GetString(), $"{page} was checked the day its sources were read.");
            var citations = webPage.GetProperty("citation").EnumerateArray().Select(citation => citation.GetProperty("url").GetString()).ToArray();
            Assert.AreSequenceEqual(subject.SourceIds.Select(id => CompareSources.All[id].Urls[0]).ToArray(), citations, $"{page}'s JSON-LD cites the sources it lists.");
            if (subject.ProductUrl is not null)
            {
                var about = webPage.GetProperty("about").EnumerateArray().ToArray();
                Assert.AreEqual($"{origin}/#software", about[0].GetProperty("@id").GetString(), $"{page} is about bit Fabric.");
                Assert.AreEqual(subject.ProductUrl, about[1].GetProperty("url").GetString(), $"{page} names the product it compares by its own address.");
            }

            var crumbs = graph[1].GetProperty("itemListElement").EnumerateArray().Select(crumb => crumb.GetProperty("item").GetString()).ToArray();
            string[] expected = page == PageUrls.Compare
                ? [$"{origin}/en-US/", $"{origin}/en-US{PageUrls.Compare}"]
                : [$"{origin}/en-US/", $"{origin}/en-US{PageUrls.Compare}", $"{origin}/en-US{page}"];
            Assert.AreSequenceEqual(expected, crumbs, $"{page}'s breadcrumb");

            // A subject's page leads back to the hub, and the hub to each subject
            Assert.Contains($"href=\"/en-US{PageUrls.Compare}\"", html, $"{page} leads to the hub.");
        }

        var hub = await GetPage(server, Googlebot, $"/en-US{PageUrls.Compare}");
        foreach (var page in ComparePages.Skip(1))
        {
            Assert.Contains($"href=\"/en-US{page}\"", hub, $"The hub leads to {page}.");
        }

        // The home page's comparison band leads to the hub
        Assert.Contains($"href=\"/en-US{PageUrls.Compare}\"", await GetPage(server, Googlebot, "/en-US/"), "The home page leads to /compare.");

        // The Persian pages are pages of their own, in Persian
        var persian = await GetPage(server, Googlebot, $"/fa-IR{PageUrls.CompareLovable}");
        Assert.Contains("<html lang=\"fa-IR\"", persian);
        Assert.Contains(AppStrings.ResourceManager.GetString(nameof(AppStrings.CompareLovableAnswer), new CultureInfo("fa-IR"))!, WebUtility.HtmlDecode(persian), "The Persian page has its Persian answer.");
    }

    /// <summary>
    /// Every small number beside a fact leads to an entry of the page's Sources list with the same number, every entry is cited
    /// by one, and every entry has its name, its address and the day it was read (the burden of proof is bit Fabric's, so a
    /// reader must be able to check each fact).
    /// </summary>
    [TestMethod]
    public async Task EveryNumberOfAComparison_Should_LeadToASourceWithItsAddressAndTheDayItWasRead()
    {
        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");
        var localizer = server.WebApp.Services.GetRequiredService<IStringLocalizer<AppStrings>>();

        foreach (var subject in SubjectsOf(localizer))
        {
            var page = subject.Url;
            var html = await GetPage(server, Googlebot, $"/en-US{page}");

            // The list, in order: each entry's anchor and what it says
            var entries = Regex.Matches(html, "<li\\b[^>]*\\bid=\"(?<id>src-[^\"]+)\"[^>]*>(?<body>.*?)</li>", RegexOptions.Singleline)
                .Select(match => (Id: match.Groups["id"].Value, Body: WebUtility.HtmlDecode(match.Groups["body"].Value)))
                .ToArray();
            Assert.AreSequenceEqual(subject.SourceIds.Select(ComparePage.AnchorOf).ToArray(), entries.Select(entry => entry.Id).ToArray(), $"{page} lists its sources in the order it cites them.");
            Assert.IsGreaterThan(0, entries.Length, $"{page} has sources.");

            for (var i = 0; i < entries.Length; i++)
            {
                var (id, body) = entries[i];
                var source = CompareSources.All[subject.SourceIds[i]];
                Assert.Contains($">{i + 1}</span>", body, $"{page}'s source {id} is numbered {i + 1}.");
                Assert.Contains(source.Name, body, $"{page}'s source {id} has its name.");
                foreach (var url in source.Urls)
                {
                    Assert.StartsWith("https://", url, $"{id}'s address");
                    Assert.Contains($"href=\"{url}\"", body, $"{page}'s source {id} links {url}.");
                }
                Assert.Contains(string.Format(AppStrings.CompareReadOn, CheckedOn), body, $"{page}'s source {id} says the day it was read.");
            }

            // The numbers beside the facts: each leads to the entry with the same number, on this page
            var refs = Regex.Matches(html, "<a\\b(?<attrs>[^>]*)>\\s*<sup\\b[^>]*>(?<number>\\d+)</sup>\\s*</a>")
                .Select(match => (Href: WebUtility.HtmlDecode(Regex.Match(match.Groups["attrs"].Value, "href=\"(?<href>[^\"]*)\"").Groups["href"].Value), Number: int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture)))
                .ToArray();
            Assert.IsGreaterThan(0, refs.Length, $"{page}'s facts carry their sources' numbers.");
            foreach (var (href, number) in refs)
            {
                Assert.IsTrue(number >= 1 && number <= entries.Length, $"{page}'s number {number} is in its list of {entries.Length}.");
                Assert.AreEqual($"/en-US{page}#{entries[number - 1].Id}", href, $"{page}'s number {number} leads to its source, on this page.");
            }
            var cited = refs.Select(reference => reference.Href.Split('#')[^1]).ToArray();
            foreach (var (id, _) in entries)
            {
                Assert.Contains(id, cited, $"{page} cites its source {id}.");
            }
        }
    }

    /// <summary>
    /// The words no comparison may say, in what the page says (its text, what its attributes read out, its JSON-LD's names
    /// and descriptions), in English and Persian. The sources' own addresses aren't the page's words, so they don't count:
    /// Base44's documentation has "backend" in its path.
    /// </summary>
    [TestMethod]
    public async Task NoComparisonPage_Should_SayTheWordsItMustNot()
    {
        await using var server = await StartPrerenderingServer();

        foreach (var culture in new[] { "en-US", "fa-IR" })
        {
            foreach (var page in ComparePages)
            {
                var html = await GetPage(server, Googlebot, $"/{culture}{page}");

                var withoutAddresses = Regex.Replace(html, "<a\\b[^>]*\\bcmp-source-url\\b[^>]*>.*?</a>", " ", RegexOptions.Singleline);
                var json = Regex.Replace(JsonLdOf(html), "\"(?:url|item|@id)\":\"[^\"]*\"", string.Empty);
                var says = WebUtility.HtmlDecode(string.Join('\n',
                    Regex.Replace(Regex.Replace(WithoutScripts(withoutAddresses), "<!--.*?-->|<style\\b.*?</style>", " ", RegexOptions.Singleline), "<[^>]+>", " "),
                    string.Join('\n', Regex.Matches(withoutAddresses, "\\s(?:alt|aria-label|aria-description|title|content|placeholder)=\"(?<value>[^\"]*)\"").Select(match => match.Groups["value"].Value)),
                    json));

                foreach (var word in new[] { "backend", "your code", ".NET", "C#", "unlike" })
                {
                    Assert.DoesNotContain(word, says, StringComparison.OrdinalIgnoreCase, $"/{culture}{page} says nothing of \"{word}\".");
                }
            }
        }
    }

    /// <summary>
    /// An address under /compare that names no subject ends at the not-found page with a 404, like any unknown address
    /// (bitfabric-internal/specs/home-page.md, 2026-11-20): no comparison page is rendered for it.
    /// </summary>
    [TestMethod]
    public async Task AnUnknownComparison_Should_EndAtTheNotFoundPage()
    {
        await using var server = await StartPrerenderingServer();
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = server.WebAppAddress };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(Googlebot);

        foreach (var path in new[] { "/en-US/compare/no-such-builder", "/fa-IR/compare/no-such-builder", "/compare/no-such-builder" })
        {
            using var response = await client.GetAsync(path, TestContext.CancellationToken);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, $"{path} ends at {response.RequestMessage!.RequestUri} with {(int)response.StatusCode}.");
            Assert.Contains(PageUrls.NotFound, response.RequestMessage!.RequestUri!.AbsolutePath, $"{path} ends at the not-found page.");
        }
    }

    /// <summary>The hub and the seven subjects, built the way the page builds them, in the culture the test set.</summary>
    private static CompareSubject[] SubjectsOf(IStringLocalizer<AppStrings> localizer) => [HubComparison.Build(localizer), .. HubComparison.Subjects(localizer)];
}
