using System.Net;
using Bit.Fabric.Client.Core.Components.Pages.Home;
using Bit.Fabric.Server.Web.Infrastructure.Services;

namespace Bit.Fabric.Tests.Features.Home;

/// <summary>
/// Every public page as clean Markdown, and all of them in /llms-full.txt (bitfabric-internal/specs/home-page.md, "The AI
/// evaluation"): made from what the page pre-renders, with the page's own words and none of the app's chrome.
/// </summary>
public partial class HomePageSeoTests
{
    /// <summary>What belongs to the app's chrome or controls, and never to a page's copy.</summary>
    private static readonly string[] MarkdownChrome = ["Sign in (Popup)", "Update ready to install", "(opens in a new tab)", "Copy the link to this section", "<div", "<span", "<button", "<svg"];

    /// <summary>The longest /llms-full.txt may grow before someone looks at what made it grow: what the assistant reads with every question.</summary>
    private const int LlmsFullMaxLength = 400_000;

    [TestMethod]
    public async Task EveryPage_Should_HaveACleanMarkdownCopy_AndLlmsFullAllOfThem()
    {
        await using var server = await StartPrerenderingServer();

        using var client = new HttpClient { BaseAddress = server.WebAppAddress };
        var origin = server.WebAppAddress.ToString().TrimEnd('/');

        var pages = SitemapPages.All();
        Assert.IsGreaterThan(20, pages.Length, "The sitemap's pages are the ones with a copy.");

        // /pricing.md, /cost.md and /cli.md are written by hand, and their own tests read them.
        string[] handWritten = [AppPricing.MarkdownUrl, AppCostCase.MarkdownUrl, AppCliGuide.MarkdownUrl];

        foreach (var page in pages.Where(page => handWritten.Contains(PageMarkdown.MarkdownUrl(page)) is false))
        {
            var url = PageMarkdown.MarkdownUrl(page);
            using var response = await client.GetAsync(url, TestContext.CancellationToken);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"{url} answers.");
            var type = response.Content.Headers.ContentType;
            Assert.IsNotNull(type);
            Assert.AreEqual("text/markdown", type.MediaType, $"{url} is Markdown.");

            var markdown = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

            if (Environment.GetEnvironmentVariable("PAGE_MARKDOWN_DUMP") is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
                await File.WriteAllTextAsync(Path.Combine(folder, url.Trim('/').Replace('/', '_')), markdown, TestContext.CancellationToken);
            }

            foreach (var chrome in MarkdownChrome)
            {
                Assert.DoesNotContain(chrome, markdown, $"{url} has none of the app's chrome or controls: \"{chrome}\".");
            }

            Assert.MatchesRegex(@"(?m)^#{1,6} \S", markdown, $"{url} keeps its page's headings.");
            Assert.DoesNotMatchRegex(@"\]\(/", markdown, $"{url}'s links are absolute, so an agent can follow them.");
        }

        var features = await client.GetStringAsync(PageMarkdown.MarkdownUrl(PageUrls.Features), TestContext.CancellationToken);
        Assert.StartsWith("---\ntitle: ", features, "A copy starts with its page's title, description and address.");
        Assert.Contains($"url: {origin}/en-US{PageUrls.Features}", features);
        Assert.Contains("Keycloak", features, "The features' words are in their copy.");
        Assert.Contains("kiosk", features, StringComparison.OrdinalIgnoreCase);

        // /new pre-renders every step and every question's help, not only the first step, so its copy has the whole wizard.
        var wizard = await client.GetStringAsync(PageMarkdown.MarkdownUrl(PageUrls.NewProject), TestContext.CancellationToken);
        foreach (var line in new[] { AppStrings.NewProjectCustomersTitle, AppStrings.NewProjectErrorsTitle, AppStrings.NewProjectDevicesTitle, AppStrings.NewProjectHelpErrorsTitle, AppStrings.NewProjectHelpDevicesAdvice })
        {
            Assert.Contains(line, wizard, $"/new.md has \"{line}\".");
        }

        var company = await client.GetStringAsync(PageMarkdown.MarkdownUrl(PageUrls.Trust), TestContext.CancellationToken);
        Assert.Contains("- **Name**: BitPlatform", company, "A list of facts reads as term and value.");

        // What describes this running app rather than bit Fabric stays out, and a switch says whether it is on.
        var about = await client.GetStringAsync(PageMarkdown.MarkdownUrl(PageUrls.About), TestContext.CancellationToken);
        Assert.DoesNotContain(AppStrings.AboutThisAppTitle, about, "The installed app's version is no part of what bit Fabric is.");
        var home = await client.GetStringAsync(PageMarkdown.MarkdownUrl(PageUrls.Home), TestContext.CancellationToken);
        Assert.Contains("(off)", home, "The cost calculator's optional items say they are off, so a reader doesn't add them to the bill.");

        var full = await client.GetStringAsync("/llms-full.txt", TestContext.CancellationToken);

        foreach (var page in pages.Where(page => page != PageUrls.Contents))
        {
            Assert.Contains($"url: {origin}/en-US{(page is "/" ? "/" : page)}\n", full, $"/llms-full.txt has {page}.");
        }

        Assert.IsLessThan(LlmsFullMaxLength, full.Length, $"/llms-full.txt is {full.Length} characters.");

        var llms = await client.GetStringAsync("/llms.txt", TestContext.CancellationToken);
        Assert.Contains($"{origin}/llms-full.txt", llms, "llms.txt leads to the whole site in one file.");
    }
}
