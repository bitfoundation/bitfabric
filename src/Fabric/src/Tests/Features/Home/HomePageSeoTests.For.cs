using System.Net;
using System.Text.Json;
using Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;
using Bit.Fabric.Client.Core.Components.Pages.Home.Parts;
using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Tests.Features.Home;

/// <summary>
/// /for and its pages as a crawler reads them (bitfabric-internal/specs/home-page.md, "The AI agent pages", 2026-11-21): each
/// answers at its own address with its own title, description, one h1 and numbered Sources list, every number in it leads to
/// a source the list has, each source has its address and the day it was read, and the JSON-LD has real values only. The
/// hub's table names what each agent reads, from the agents' own classes. No page says what it must not: ".NET" or "C#"
/// (bitfabric-internal/specs/product.md, Q09), "backend", "months" (building never takes months), or that a model is
/// "cheaper" (what a project costs is tokens per task and per project, never a token's price). An unknown agent answers 404.
/// </summary>
public partial class HomePageSeoTests
{
    /// <summary>
    /// The hub and a page per agent and model, each a literal route of ForPage. A property, as PublicPages in another part of
    /// this class reads it, and the order in which the parts' fields are set isn't defined.
    /// </summary>
    private static string[] ForPages =>
    [
        PageUrls.For, PageUrls.ForGitHubCopilot, PageUrls.ForCodex, PageUrls.ForClaudeCode, PageUrls.ForGeminiCli, PageUrls.ForAntigravity,
        PageUrls.ForCursor, PageUrls.ForJunie, PageUrls.ForWindsurf, PageUrls.ForLocalModels, PageUrls.ForChineseModels
    ];

    private const string AgentsCheckedOn = "21 November 2026";

    [TestMethod]
    public async Task EachAgentPage_Should_AnswerWithItsTitleWhatTheAgentReadsAndItsSources()
    {
        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");
        var origin = server.WebAppAddress.ToString().TrimEnd('/');
        var localizer = server.WebApp.Services.GetRequiredService<IStringLocalizer<AppStrings>>();
        var pages = ForHub.Pages(localizer);

        Assert.AreSequenceEqual(ForPages.Skip(1).ToArray(), pages.Select(page => page.Url).ToArray(), "Every address has its page, and every page its address.");

        foreach (var subject in AgentPagesOf(localizer))
        {
            var page = subject.Url;
            var isHub = page == PageUrls.For;
            var html = await GetPage(server, Googlebot, $"/en-US{page}");
            var head = Head(html);
            var text = WebUtility.HtmlDecode(WithoutScripts(html));

            Assert.AreEqual(subject.PageTitle, Match(head, "<title>([^<]*)</title>"), $"{page}'s title");
            Assert.AreEqual(subject.Description, Meta(head, "name", "description"), $"{page}'s description");
            Assert.AreEqual($"{origin}/en-US{page}", Match(head, "<link rel=\"canonical\" href=\"([^\"]*)\""), $"{page} is its own canonical address.");
            AssertOneH1(html, subject.Title);
            Assert.Contains(subject.Lead, text, $"{page} says what the visitor gets first.");
            Assert.Contains(string.Format(AppStrings.CompareCheckedOn, AgentsCheckedOn), text, $"{page} says when it was checked.");
            Assert.Contains($"href=\"/en-US{PageUrls.NewProject}\"", html, $"{page} leads to Get started.");

            if (isHub)
            {
                AssertBandsInOrder(html, "matrix", "agents", "models", "questions", "sources");

                // The table: a row per agent, each leading to its page, and each cell what the agent's own class says
                var table = Regex.Match(html, "<table\\b[^>]*\\bfor-matrix\\b.*?</table>", RegexOptions.Singleline);
                Assert.IsTrue(table.Success, "The hub has its table.");
                var tableText = WebUtility.HtmlDecode(table.Value);
                var rowHeaders = Regex.Matches(table.Value, "<th scope=\"row\"[^>]*>.*?</th>", RegexOptions.Singleline)
                    .Select(match => WebUtility.HtmlDecode(Regex.Replace(match.Value, "<[^>]+>", string.Empty)).Trim()).ToArray();
                var agents = ForHub.Agents(localizer);
                Assert.AreSequenceEqual(agents.Select(agent => agent.Name).ToArray(), rowHeaders, "The table's rows, an agent each, in order.");
                foreach (var agent in agents)
                {
                    Assert.Contains($"href=\"/en-US{agent.Url}\"", table.Value, $"The table leads to {agent.Url}.");
                    foreach (var line in agent.Lines)
                    {
                        Assert.Contains(line.Label, tableText, $"The table says {agent.Name} reads {line.Label}.");
                    }
                    Assert.Contains(agent.SkillCall is { } call ? ForShared.Call(call, ForShared.FirstSkill) : AppStrings.ForCallByDescription, tableText, $"The table says how {agent.Name} calls a skill.");
                    if (agent.ToolsNote is { } note)
                    {
                        Assert.Contains(note.Text, tableText, $"The table says where {agent.Name} takes its tools.");
                    }
                }
                foreach (var other in pages)
                {
                    Assert.Contains($"href=\"/en-US{other.Url}\"", html, $"The hub leads to {other.Url}.");
                    Assert.Contains(other.CardLine, text, $"The hub's card for {other.Url} has its line.");
                }
            }
            else
            {
                string[] bands = ["reads", .. subject.Models is null ? Array.Empty<string>() : ["models"], "skills", "setup", "cost", "others", "questions", "sources"];
                AssertBandsInOrder(html, bands);

                Assert.Contains(subject.DoesWell.Text, text, $"{page} says what the agent does well.");
                foreach (var line in subject.Lines)
                {
                    Assert.Contains(line.Label, text, $"{page} names {line.Label}.");
                    Assert.Contains(line.Text, text, $"{page} says what {line.Label} gives.");
                }
                Assert.IsNotEmpty(subject.Lines, $"{page} names what the agent reads, or where the model goes.");
                Assert.Contains(subject.SkillLine.Text, text, $"{page} says how a skill is called.");
                if (subject.SkillCall is { } call)
                {
                    Assert.Contains(ForShared.Call(call, ForShared.FirstSkill), text, $"{page} shows the first skill's call.");
                }

                // How to start: the two shared steps, then the agent's own, the last asking for the first feature
                var steps = Regex.Matches(html, "<li\\b[^>]*\\bfor-step\\b").Count;
                Assert.AreEqual(2 + subject.Steps.Length, steps, $"{page}'s steps");
                Assert.AreEqual(AppStrings.ForStepAskTitle, subject.Steps[^1].Title, $"{page}'s last step asks for the first feature.");

                // What it costs, with the way on to /pricing rather than restating it
                Assert.Contains(subject.Cost.Text, text, $"{page} says what the agent costs.");
                Assert.Contains(AppStrings.ForCostFabricText, text, $"{page} says what bit Fabric costs.");
                Assert.Contains($"href=\"/en-US{PageUrls.Pricing}\"", html, $"{page} leads to /pricing.");

                // The other pages, and the way back to the hub
                foreach (var other in pages.Where(other => other.Url != page))
                {
                    Assert.Contains($"href=\"/en-US{other.Url}\"", html, $"{page} leads to {other.Url}.");
                }
                Assert.Contains($"href=\"/en-US{PageUrls.For}\"", html, $"{page} leads back to the hub.");
            }

            Assert.IsGreaterThanOrEqualTo(3, subject.Questions.Length, $"{page} has three to five questions.");
            Assert.IsLessThanOrEqualTo(5, subject.Questions.Length, $"{page} has three to five questions.");
            foreach (var question in subject.Questions)
            {
                Assert.Contains(question.Question, text, $"{page} asks \"{question.Question}\".");
                Assert.Contains(question.Answer, text, $"{page} answers \"{question.Question}\" in the page.");
            }

            // JSON-LD with real values only: the page, citing its sources, and the way back through /for
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
            Assert.AreEqual("2026-11-21", webPage.GetProperty("dateModified").GetString(), $"{page} was checked the day its sources were read.");
            var citations = webPage.GetProperty("citation").EnumerateArray().Select(citation => citation.GetProperty("url").GetString()).ToArray();
            Assert.AreSequenceEqual(SourceIdsOf(localizer, subject).Select(id => ForSources.All[id].Urls[0]).ToArray(), citations, $"{page}'s JSON-LD cites the sources it lists.");
            if (subject.ProductUrl is not null)
            {
                var about = webPage.GetProperty("about").EnumerateArray().ToArray();
                Assert.AreEqual($"{origin}/#software", about[0].GetProperty("@id").GetString(), $"{page} is about bit Fabric.");
                Assert.AreEqual(subject.ProductUrl, about[1].GetProperty("url").GetString(), $"{page} names the agent by its own address.");
            }

            var crumbs = graph[1].GetProperty("itemListElement").EnumerateArray().Select(crumb => crumb.GetProperty("item").GetString()).ToArray();
            string[] expected = isHub
                ? [$"{origin}/en-US/", $"{origin}/en-US{PageUrls.For}"]
                : [$"{origin}/en-US/", $"{origin}/en-US{PageUrls.For}", $"{origin}/en-US{page}"];
            Assert.AreSequenceEqual(expected, crumbs, $"{page}'s breadcrumb");
        }

        // The Persian pages are pages of their own, in Persian
        var persian = await GetPage(server, Googlebot, $"/fa-IR{PageUrls.ForChineseModels}");
        Assert.Contains("<html lang=\"fa-IR\"", persian);
        Assert.Contains(AppStrings.ResourceManager.GetString(nameof(AppStrings.ForChineseLead), new CultureInfo("fa-IR"))!, WebUtility.HtmlDecode(persian), "The Persian page has its Persian lead.");
    }

    /// <summary>
    /// The ways in (bitfabric-internal/specs/home-page.md, "The AI agent pages", 2026-11-21): /features' agents band, the home
    /// page's How band, the pages about the visitor's own agent among the comparisons, llms.txt, and the navigation panel lead
    /// to /for.
    /// </summary>
    [TestMethod]
    public async Task TheAgentPages_Should_BeReachedFromTheSite()
    {
        await using var server = await StartPrerenderingServer();
        var link = $"href=\"/en-US{PageUrls.For}\"";

        foreach (var page in new[] { "/", PageUrls.Features, PageUrls.CompareEmptyProject, PageUrls.CompareStarterKits })
        {
            var html = await GetPage(server, Googlebot, $"/en-US{page}");
            Assert.Contains(link, html, $"{page} leads to /for.");
            Assert.Contains(AppStrings.ForSeeEveryAgent, WebUtility.HtmlDecode(html), $"{page} says where the link leads.");
        }

        // A comparison that isn't about the visitor's own agent doesn't lead there from its hero
        Assert.DoesNotContain(AppStrings.ForSeeEveryAgent, WebUtility.HtmlDecode(await GetPage(server, Googlebot, $"/en-US{PageUrls.CompareLovable}")), "/compare/lovable keeps its hero to the comparison.");

        // On /features the link stays in the tab, as it leads to a page of this site
        var features = await GetPage(server, Googlebot, $"/en-US{PageUrls.Features}");
        var anchor = Regex.Match(features, $"<a\\b[^>]*{Regex.Escape(link)}[^>]*>");
        Assert.IsTrue(anchor.Success, "/features has the link.");
        Assert.DoesNotContain("_blank", anchor.Value, "/features opens /for in the same tab.");

        using var client = NoRedirectClient(server);
        var llms = await client.GetStringAsync("/llms.txt", TestContext.CancellationToken);
        var origin = server.WebAppAddress.ToString().TrimEnd('/');
        foreach (var page in ForPages)
        {
            Assert.Contains($"({origin}/en-US{page})", llms, $"llms.txt lists {page}.");
        }
    }

    /// <summary>
    /// Every small number beside a fact leads to an entry of the page's Sources list with the same number, every entry is cited
    /// by one, and every entry has its name, its address and the day it was read: a reader can check each fact about an agent.
    /// </summary>
    [TestMethod]
    public async Task EveryNumberOfAnAgentPage_Should_LeadToASourceWithItsAddressAndTheDayItWasRead()
    {
        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");
        var localizer = server.WebApp.Services.GetRequiredService<IStringLocalizer<AppStrings>>();

        foreach (var subject in AgentPagesOf(localizer))
        {
            var page = subject.Url;
            var html = await GetPage(server, Googlebot, $"/en-US{page}");
            var ids = SourceIdsOf(localizer, subject);

            var entries = Regex.Matches(html, "<li\\b[^>]*\\bid=\"(?<id>src-[^\"]+)\"[^>]*>(?<body>.*?)</li>", RegexOptions.Singleline)
                .Select(match => (Id: match.Groups["id"].Value, Body: WebUtility.HtmlDecode(match.Groups["body"].Value)))
                .ToArray();
            Assert.AreSequenceEqual(ids.Select(AppHomeSourceList.AnchorOf).ToArray(), entries.Select(entry => entry.Id).ToArray(), $"{page} lists its sources in the order it cites them.");
            Assert.IsGreaterThan(0, entries.Length, $"{page} has sources.");

            for (var i = 0; i < entries.Length; i++)
            {
                var (id, body) = entries[i];
                var source = ForSources.All[ids[i]];
                Assert.Contains($">{i + 1}</span>", body, $"{page}'s source {id} is numbered {i + 1}.");
                Assert.Contains(source.Name, body, $"{page}'s source {id} has its name.");
                foreach (var url in source.Urls)
                {
                    Assert.StartsWith("https://", url, $"{id}'s address");
                    Assert.Contains($"href=\"{url}\"", body, $"{page}'s source {id} links {url}.");
                }
                Assert.Contains(string.Format(AppStrings.CompareReadOn, AgentsCheckedOn), body, $"{page}'s source {id} says the day it was read.");
            }

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
    /// The words no agent page may say, in what the page says (its text, what its attributes read out and its JSON-LD's names
    /// and descriptions), in English and Persian. The sources' own addresses aren't the page's words, so they don't count.
    /// </summary>
    [TestMethod]
    public async Task NoAgentPage_Should_SayTheWordsItMustNot()
    {
        await using var server = await StartPrerenderingServer();

        foreach (var culture in new[] { "en-US", "fa-IR" })
        {
            foreach (var page in ForPages)
            {
                var html = await GetPage(server, Googlebot, $"/{culture}{page}");

                var withoutAddresses = Regex.Replace(html, "<a\\b[^>]*\\bcmp-source-url\\b[^>]*>.*?</a>", " ", RegexOptions.Singleline);
                var json = Regex.Replace(JsonLdOf(html), "\"(?:url|item|@id)\":\"[^\"]*\"", string.Empty);
                var says = WebUtility.HtmlDecode(string.Join('\n',
                    Regex.Replace(Regex.Replace(WithoutScripts(withoutAddresses), "<!--.*?-->|<style\\b.*?</style>", " ", RegexOptions.Singleline), "<[^>]+>", " "),
                    string.Join('\n', Regex.Matches(withoutAddresses, "\\s(?:alt|aria-label|aria-description|title|content|placeholder)=\"(?<value>[^\"]*)\"").Select(match => match.Groups["value"].Value)),
                    json));

                foreach (var word in new[] { ".NET", "C#", "backend", "months", "cheaper", "cheapest", "ماه‌ها", "ارزان" })
                {
                    Assert.DoesNotContain(word, says, StringComparison.OrdinalIgnoreCase, $"/{culture}{page} says nothing of \"{word}\".");
                }
            }
        }
    }

    /// <summary>An address under /for that names no agent ends at the not-found page with a 404, like any unknown address (bitfabric-internal/specs/home-page.md, 2026-11-20).</summary>
    [TestMethod]
    public async Task AnUnknownAgent_Should_EndAtTheNotFoundPage()
    {
        await using var server = await StartPrerenderingServer();
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = server.WebAppAddress };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(Googlebot);

        foreach (var path in new[] { "/en-US/for/no-such-agent", "/fa-IR/for/no-such-agent", "/for/no-such-agent" })
        {
            using var response = await client.GetAsync(path, TestContext.CancellationToken);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, $"{path} ends at {response.RequestMessage!.RequestUri} with {(int)response.StatusCode}.");
            Assert.Contains(PageUrls.NotFound, response.RequestMessage!.RequestUri!.AbsolutePath, $"{path} ends at the not-found page.");
        }
    }

    /// <summary>The hub and every page under it, built the way the page builds them, in the culture the test set.</summary>
    private static AgentPage[] AgentPagesOf(IStringLocalizer<AppStrings> localizer) => [ForHub.Build(localizer), .. ForHub.Pages(localizer)];

    /// <summary>The sources a page lists, in its order: the hub's table's and questions', or the page's own.</summary>
    private static string[] SourceIdsOf(IStringLocalizer<AppStrings> localizer, AgentPage page)
        => page.Url == PageUrls.For ? ForHub.SourceIds(page, ForHub.Matrix(localizer, ForHub.Agents(localizer))) : page.SourceIds;
}
