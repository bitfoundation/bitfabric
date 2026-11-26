using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Caching.Memory;

namespace Bit.Fabric.Server.Web.Infrastructure.Services;

/// <summary>
/// Every public page as clean Markdown for AI agents and the assistant (bitfabric-internal/specs/home-page.md, "The AI
/// evaluation"): the page's English address is requested from this host itself, so the copy is exactly what the page
/// pre-renders, then only the page's own content is kept - not the header, the navigation panel, the bottom bar, buttons,
/// icons or anything marked <c>data-md="skip"</c>, like a mock app screen - and written as Markdown. A new page gets its copy
/// with no code of its own. Each copy is kept an hour; the CDN keeps it longer.
/// </summary>
public partial class PageMarkdown(IServer server, IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<PageMarkdown> logger)
{
    public const string HttpClientName = nameof(PageMarkdown);

    /// <summary>The address a page's copy is served at: <c>/features.md</c>, and <c>/index.md</c> for the home page.</summary>
    public static string MarkdownUrl(string page) => page is "/" ? "/index.md" : $"{page}.md";

    /// <summary>
    /// What drops out of a page before it is written as Markdown: the controls, the pictures without words, what is marked to
    /// skip, and the "(opens in a new tab)" BitLink adds for a screen reader (the test reads every copy for it, should the
    /// library's class change).
    /// </summary>
    private const string Skipped = "script, style, noscript, template, iframe, svg, canvas, video, audio, button, input, select, textarea, nav, dialog, img:not([alt]), img[alt=''], [aria-hidden='true'], [data-md='skip'], [role='dialog'], .bit-lnk-hnt";

    private static readonly HtmlParser parser = new();

    private readonly SemaphoreSlim allPages = new(1, 1);

    private static readonly ReverseMarkdown.Converter converter = new(new ReverseMarkdown.Config
    {
        GithubFlavored = true,
        RemoveComments = true,
        SmartHrefHandling = true,
        UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.Bypass,
        ListBulletChar = '-'
    });

    /// <summary>
    /// The page's copy, with its links on <paramref name="origin"/>; null when the page can't be read. It is made and kept
    /// once per page on this host's own address, whatever address it is asked for on, and that address is replaced with
    /// <paramref name="origin"/> as it is served: the page itself may write its own address in its text, as the home page's
    /// prompt does.
    /// </summary>
    public async Task<string?> Of(string page, Uri origin, CancellationToken cancellationToken)
    {
        var own = OwnAddress();

        var markdown = await cache.GetOrCreateAsync($"PageMarkdown:{page}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

            var path = page is "/" ? "/en-US/" : $"/en-US{page}";

            try
            {
                using var client = httpClientFactory.CreateClient(HttpClientName);
                client.BaseAddress = own;

                using var response = await client.GetAsync(path, cancellationToken);

                if (response.IsSuccessStatusCode is false)
                {
                    logger.LogWarning("The Markdown copy of {Page} couldn't read the page: {StatusCode}.", page, (int)response.StatusCode);
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                    return null;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken);

                return ToMarkdown(html, new Uri(own, path));
            }
            catch (Exception exp) when (exp is HttpRequestException or TaskCanceledException && cancellationToken.IsCancellationRequested is false)
            {
                logger.LogWarning(exp, "The Markdown copy of {Page} couldn't read the page.", page);
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return null;
            }
        });

        return markdown?.Replace(own.GetLeftPart(UriPartial.Authority), origin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every public page's copy, one after another, for <c>/llms-full.txt</c> and the assistant: the home page first, then the
    /// sitemap's order, each under its address. Null when a page can't be read, so an incomplete site is never served or cached.
    /// One request makes them at a time; the others wait and find them in the cache.
    /// </summary>
    public async Task<string?> All(IEnumerable<string> pages, Uri origin, CancellationToken cancellationToken)
    {
        await allPages.WaitAsync(cancellationToken);

        try
        {
            var text = new StringBuilder();
            text.Append("# bit Fabric: every page of bitfabric.co\n\n");
            text.Append("> Each public page of the site as Markdown, in English, one after another under its address. The site's index is llms.txt.\n");

            // /contents is the home page as it was, every band of it again: its own copy has it, the whole site leaves it out.
            foreach (var page in pages.Where(page => page != PageUrls.Contents).OrderBy(page => page is "/" ? 0 : 1))
            {
                if (await Of(page, origin, cancellationToken) is not { Length: > 0 } markdown)
                    return null;

                text.Append("\n\n---\n\n");
                text.Append(markdown);
            }

            return text.ToString();
        }
        finally
        {
            allPages.Release();
        }
    }

    /// <summary>A pre-rendered page's own content as Markdown, under a front matter with its title, description and address.</summary>
    public static string ToMarkdown(string html, Uri pageUrl)
    {
        var document = parser.ParseDocument(html);

        var title = document.Title?.Trim();
        var description = document.QuerySelector("meta[name='description']")?.GetAttribute("content")?.Trim();

        // The page's own content: the app's shell puts the page in main's .body, beside the header and the navigation panel.
        var content = document.QuerySelector("main .body") ?? document.QuerySelector("main") ?? document.Body;

        if (content is null)
            return string.Empty;

        // What only a screen reader hears beside a control, like "Copy the link to this section", says nothing about the page:
        // the library's hidden AriaDescription, whose id ends in -dsc. A description everyone sees, like a switch's line on
        // what it does, stays.
        var descriptions = content.QuerySelectorAll("[aria-describedby]")
                                  .SelectMany(described => described.GetAttribute("aria-describedby")!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                                  .Where(id => id.EndsWith("-dsc", StringComparison.Ordinal))
                                  .ToHashSet();

        // A question of an accordion, like the home page's FAQ, is the title inside its header's button, which goes with the
        // other buttons below: it is kept as a line of its own above its answer.
        foreach (var header in content.QuerySelectorAll("button.bit-acd-hdr").ToArray())
        {
            if (header.QuerySelector(".bit-acd-ttl")?.TextContent.Trim() is not { Length: > 0 } question)
                continue;

            var line = document.CreateElement("p");
            line.InnerHtml = $"<strong>{System.Net.WebUtility.HtmlEncode(question)}</strong>";
            header.Replace(line);
        }

        // A switch says whether it is on, as a reader of the page sees it, like the cost calculator's optional items.
        foreach (var toggle in content.QuerySelectorAll("input[type='checkbox'], [role='switch'], [role='checkbox']").ToArray())
        {
            var on = toggle.HasAttribute("checked") || toggle.GetAttribute("aria-checked") is "true";
            toggle.Replace(document.CreateTextNode(on ? " (on) " : " (off) "));
        }

        foreach (var skipped in content.QuerySelectorAll(Skipped).Concat(content.QuerySelectorAll("[id]").Where(element => descriptions.Contains(element.Id!))).ToArray())
        {
            skipped.Remove();
        }

        foreach (var link in content.QuerySelectorAll("a").ToArray())
        {
            var href = link.GetAttribute("href");

            // A link that leads nowhere on its own - a script, a section of this same page - keeps its words.
            if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#') || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
                || Uri.TryCreate(pageUrl, href, out var absolute) is false
                || (string.IsNullOrEmpty(absolute.Fragment) is false && absolute.GetLeftPart(UriPartial.Path) == pageUrl.GetLeftPart(UriPartial.Path)))
            {
                link.Replace([.. link.ChildNodes]);
                continue;
            }

            link.SetAttribute("href", absolute.ToString());
        }

        foreach (var image in content.QuerySelectorAll("img[src]").ToArray())
        {
            if (Uri.TryCreate(pageUrl, image.GetAttribute("src"), out var absolute))
                image.SetAttribute("src", absolute.ToString());
        }

        // A list of facts, like the company's name and address, becomes a list of "term: value" lines, which Markdown has.
        foreach (var list in content.QuerySelectorAll("dl").ToArray())
        {
            var items = document.CreateElement("ul");

            foreach (var term in list.QuerySelectorAll("dt"))
            {
                var item = document.CreateElement("li");
                item.InnerHtml = $"<strong>{term.InnerHtml}</strong>: {string.Join(", ", NextValues(term).Select(value => value.InnerHtml))}";
                items.AppendChild(item);
            }

            list.Replace(items);
        }

        // Words in spans side by side, like a choice's label, its badge and its line, read as one run: a space keeps them apart.
        foreach (var span in content.QuerySelectorAll("span").ToArray())
        {
            if (span.NextElementSibling is not null && span.NextSibling == span.NextElementSibling)
                span.After(document.CreateTextNode(" "));

            if (span.PreviousSibling is { NodeType: NodeType.Text } text && text.TextContent.Length > 0 && char.IsWhiteSpace(text.TextContent[^1]) is false)
                span.Before(document.CreateTextNode(" "));
        }

        var markdown = converter.Convert(content.InnerHtml);

        var front = new StringBuilder("---\n");
        if (string.IsNullOrEmpty(title) is false) front.Append($"title: {Yaml(title)}\n");
        if (string.IsNullOrEmpty(description) is false) front.Append($"description: {Yaml(description)}\n");
        front.Append($"url: {pageUrl}\n---\n\n");

        return front + Tidy(markdown);
    }

    /// <summary>The values a term of a list of facts has: the dd elements after it, up to the next term.</summary>
    private static IEnumerable<IElement> NextValues(IElement term)
    {
        for (var next = term.NextElementSibling; next is not null && next.LocalName is "dd"; next = next.NextElementSibling)
        {
            yield return next;
        }
    }

    /// <summary>The Markdown with no space at line ends and no more than one empty line in a row.</summary>
    private static string Tidy(string markdown)
    {
        var lines = markdown.ReplaceLineEndings("\n").Split('\n').Select(line => line.TrimEnd());

        return ManyEmptyLines().Replace(string.Join('\n', lines), "\n\n").Trim() + "\n";
    }

    private static string Yaml(string value) => $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    /// <summary>The address this host listens on, as seen from itself: a wildcard host becomes localhost.</summary>
    private Uri OwnAddress()
    {
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];

        var address = addresses.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) ?? addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("This host listens on no address to read its pages from.");

        return new Uri(WildcardHost().Replace(address, "://localhost"));
    }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyEmptyLines();

    [GeneratedRegex(@"://(\+|\*|0\.0\.0\.0|\[::\])")]
    private static partial Regex WildcardHost();
}
