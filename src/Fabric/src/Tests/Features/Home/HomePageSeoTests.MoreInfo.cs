using System.Net;

namespace Bit.Fabric.Tests.Features.Home;

/// <summary>
/// A card's "More info" (bitfabric-internal/specs/home-page.md, "A card's More info"): the card shows one plain line, and the
/// fuller facts a reader checking the feature asks for open under a toggle inside it. Closed, they are in the prerendered
/// page as hidden="until-found", so search engines, find-in-page and the page's Markdown copy read them, in every language.
/// </summary>
public partial class HomePageSeoTests
{
    /// <summary>The cards with a "More info" today, by their feature's id, and what it says, in English.</summary>
    private static readonly (string Card, string[] Lines)[] MoreInfos =
    [
        ("libraries", [AppStrings.AppHomeAnyLibraryLead, AppStrings.AppHomeAnyLibraryCSharp, AppStrings.AppHomeAnyLibraryJavaScript, AppStrings.AppHomeAnyLibraryJavaAndKotlin, AppStrings.AppHomeAnyLibraryObjectiveC, AppStrings.AppHomeAddingALibraryTakesALittleSetup])
    ];

    [TestMethod]
    public async Task ACardsMoreInfo_Should_BeInThePrerenderedPage_AndItsMarkdownCopy_WhileClosed()
    {
        await using var server = await StartPrerenderingServer();

        var html = await GetFeaturesPage(server, Googlebot);

        var page = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        foreach (var (card, lines) in MoreInfos)
        {
            var id = $"ah-more-{card}";

            // A toggle that says it is closed and which part it opens, and that part, closed but in the page
            var toggle = page.GetElementById($"{id}-toggle");
            Assert.IsNotNull(toggle, $"The {card} card has its More info toggle.");
            Assert.AreEqual("false", toggle.GetAttribute("aria-expanded"), $"The {card} card's More info says it is closed: {toggle.OuterHtml}");
            Assert.AreEqual($"{id}-content", toggle.GetAttribute("aria-controls"), $"The {card} card's More info names the part it opens.");
            Assert.Contains(AppStrings.AppHomeMoreInfo, toggle.TextContent, "The toggle reads More info.");

            var part = page.GetElementById($"{id}-content");
            Assert.IsNotNull(part, $"The {card} card's More info is in the page.");
            Assert.AreEqual("until-found", part.GetAttribute("hidden"), $"The {card} card's More info is in the page, closed.");
            Assert.IsFalse(part.HasAttribute("aria-hidden"), "Closed, it isn't hidden from the Markdown copy, which leaves aria-hidden parts out.");

            foreach (var line in lines)
            {
                Assert.Contains(line, part.TextContent, $"The {card} card's More info has \"{line}\" inside it, in the prerendered page.");
            }
        }

        // The same in Persian, right to left
        var persian = WebUtility.HtmlDecode(await GetPage(server, Googlebot, $"/fa-IR{PageUrls.Features}"));
        var fa = new CultureInfo("fa-IR");
        foreach (var key in new[] { nameof(AppStrings.AppHomeMoreInfo), nameof(AppStrings.AppHomeAnyLibraryYouNeedText), nameof(AppStrings.AppHomeAnyLibraryJavaAndKotlin), nameof(AppStrings.AppHomeAnyLibraryLead) })
        {
            var line = AppStrings.ResourceManager.GetString(key, fa)!;
            Assert.AreNotEqual(AppStrings.ResourceManager.GetString(key, CultureInfo.InvariantCulture), line, $"{key} has its Persian.");
            Assert.Contains(line, persian, $"The Persian page has {key}.");
        }

        // The page's Markdown copy, and with it /llms-full.txt, carries it as a list of facts; the toggle, a button, isn't there
        using var client = new HttpClient { BaseAddress = server.WebAppAddress };
        var markdown = await client.GetStringAsync($"{PageUrls.Features}.md", TestContext.CancellationToken);
        foreach (var (card, lines) in MoreInfos)
        {
            foreach (var line in lines)
            {
                Assert.Contains(line, markdown, $"/features.md has the {card} card's \"{line}\".");
            }
        }
        Assert.Contains($"- **JavaScript**: {AppStrings.AppHomeAnyLibraryJavaScript}", markdown, "A fact reads as its term and its line.");
        Assert.DoesNotContain(AppStrings.AppHomeMoreInfo, markdown, "The toggle's label is no part of the copy.");

        var full = await client.GetStringAsync("/llms-full.txt", TestContext.CancellationToken);
        Assert.Contains(AppStrings.AppHomeAnyLibraryLead, full, "/llms-full.txt has the libraries card's More info.");
    }
}
