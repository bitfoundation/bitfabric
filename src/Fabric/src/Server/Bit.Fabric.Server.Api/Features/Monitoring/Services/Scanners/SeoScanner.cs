using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;

/// <summary>
/// The SEO scan (bitfabric-internal/specs/scanners.md, Decisions, 2026-11-13): the home page and up to 10 pages of the
/// app's sitemap, read as their server renders them, the way a search engine's first pass does, without a browser; and
/// robots.txt. A finding is given once per kind, with the pages it was seen on, and the score is 100 less a weight per
/// kind of finding.
/// </summary>
public partial class SeoScanner(ScanHttp http, SitemapReader sitemapReader, ILogger<SeoScanner> logger) : IProjectScanner
{
    private const int MaxSitemapPages = 10;

    /// <summary>Less visible text than this in a page's HTML is an app shell waiting for its scripts, not a page.</summary>
    private const int MinPrerenderedText = 150;

    public ProjectScanKind Kind => ProjectScanKind.Seo;

    public async Task<ProjectScanResult> Scan(Project project, ProjectScan scan, CancellationToken cancellationToken)
    {
        var home = await http.Get($"{project.Address}/", cancellationToken, followRedirects: true);

        if (home is null || home.IsOk is false || home.IsHtml is false || home.Body is null)
            return ProjectScanResult.Unreachable();

        List<ScanFindingResult> findings = [];
        var origin = home.Url.GetLeftPart(UriPartial.Authority);

        var sitemaps = await sitemapReader.ReadRobots(origin, findings, cancellationToken);
        var urls = await sitemapReader.ReadSitemaps(origin, sitemaps, findings, MaxSitemapPages * 2, cancellationToken);

        List<Page> pages = [Page.Of(home)];

        foreach (var url in urls.Where(url => url != home.Url.ToString()).Distinct().Take(MaxSitemapPages))
        {
            try
            {
                var response = await http.Get(url, cancellationToken, followRedirects: true);

                if (response is { IsOk: true, IsHtml: true, Body: not null } && pages.All(page => page.Path != response.Url.PathAndQuery))
                {
                    pages.Add(Page.Of(response));
                }
            }
            catch (Exception exp) when (exp is not OperationCanceledException)
            {
                logger.LogWarning(exp, "Reading {Url} of project {ProjectId} failed.", url, project.Id);
            }
        }

        CheckPages(pages, findings);

        ProjectScanResult result = new();
        result.Findings.AddRange(findings.OrderBy(finding => finding.Severity));
        result.Score = Math.Max(0, 100 - findings.Sum(finding => finding.Severity switch
        {
            ScanFindingSeverity.Urgent => 25,
            ScanFindingSeverity.Important => 10,
            _ => 3
        }));
        result.Judge(goodFrom: 90, poorBelow: 50);

        return result;
    }

    private static void CheckPages(List<Page> pages, List<ScanFindingResult> findings)
    {
        void Each(string key, ScanFindingSeverity severity, Func<Page, bool> fails)
        {
            if (pages.Where(fails).Select(page => page.Path).ToArray() is { Length: > 0 } where)
            {
                findings.Add(new(key, severity, [], where));
            }
        }

        Each(ScanFindings.NoIndex, ScanFindingSeverity.Urgent, page => page.NoIndex);
        Each(ScanFindings.NotPrerendered, ScanFindingSeverity.Important, page => page.TextLength < MinPrerenderedText);
        Each(ScanFindings.TitleMissing, ScanFindingSeverity.Important, page => page.Title is null);
        Each(ScanFindings.DescriptionMissing, ScanFindingSeverity.Important, page => page.Description is null);
        Each(ScanFindings.LangMissing, ScanFindingSeverity.Important, page => page.HasLang is false);
        Each(ScanFindings.TitleLength, ScanFindingSeverity.Improvement, page => page.Title is { Length: < 15 or > 65 });
        Each(ScanFindings.DescriptionLength, ScanFindingSeverity.Improvement, page => page.Description is { Length: < 50 or > 165 });
        Each(ScanFindings.CanonicalMissing, ScanFindingSeverity.Improvement, page => page.HasCanonical is false);
        // A page under a culture's path, as a bit app's are, exists in its other languages too.
        Each(ScanFindings.HreflangMissing, ScanFindingSeverity.Improvement, page => page.IsUnderACulture && page.HasHreflang is false);

        // The same page in two cultures, such as /en-US/terms and /en-GB/terms, is one page in two languages, which hreflang
        // ties together, not two pages that share a title: only different pages count (found on Test, 2026-11-13).
        foreach (var group in pages.Where(page => page.Title is not null).GroupBy(page => page.Title).Where(group => group.Select(PageOf).Distinct().Count() > 1))
        {
            findings.Add(new(ScanFindings.DuplicateTitles, ScanFindingSeverity.Improvement, [group.Key!], [.. group.Select(page => page.Path)]));
        }

        foreach (var group in pages.Where(page => page.Description is not null).GroupBy(page => page.Description).Where(group => group.Select(PageOf).Distinct().Count() > 1))
        {
            findings.Add(new(ScanFindings.DuplicateDescriptions, ScanFindingSeverity.Improvement, [], [.. group.Select(page => page.Path)]));
        }

        var missingPreview = pages.Where(page => page.MissingOpenGraph.Length > 0).ToArray();
        if (missingPreview.Length > 0)
        {
            var tags = missingPreview.SelectMany(page => page.MissingOpenGraph).Distinct().Order(StringComparer.Ordinal);
            findings.Add(new(ScanFindings.SocialPreviewMissing, ScanFindingSeverity.Improvement, [string.Join(", ", tags)], [.. missingPreview.Select(page => page.Path)]));
        }

        var withoutAlt = pages.Where(page => page.ImagesWithoutAlt > 0).ToArray();
        if (withoutAlt.Length > 0)
        {
            findings.Add(new(ScanFindings.ImagesWithoutAlt, ScanFindingSeverity.Improvement, [withoutAlt.Sum(page => page.ImagesWithoutAlt).ToString(CultureInfo.InvariantCulture)], [.. withoutAlt.Select(page => page.Path)]));
        }
    }

    /// <summary>What a search engine reads of one page.</summary>
    private sealed record Page(string Path, string? Title, string? Description, bool HasLang, bool HasCanonical, bool HasHreflang, bool IsUnderACulture,
                               bool NoIndex, int TextLength, string[] MissingOpenGraph, int ImagesWithoutAlt)
    {
        public static Page Of(ScanResponse response)
        {
            var document = new HtmlParser().ParseDocument(response.Body!);

            string? Meta(string attribute, string name) => document.QuerySelectorAll("meta")
                .FirstOrDefault(meta => string.Equals(meta.GetAttribute(attribute), name, StringComparison.OrdinalIgnoreCase))
                ?.GetAttribute("content")?.Trim() is { Length: > 0 } content ? content : null;

            var links = document.QuerySelectorAll("link[rel]").ToArray();
            bool HasLink(string rel) => links.Any(link => link.GetAttribute("rel")!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(rel, StringComparer.OrdinalIgnoreCase));

            var robots = $"{Meta("name", "robots")} {Meta("name", "googlebot")} {response.Header("X-Robots-Tag")}";

            string[] missingOpenGraph = [.. new[] { "og:title", "og:image" }.Where(property => Meta("property", property) is null)];

            return new(
                Path: response.Url.PathAndQuery,
                Title: document.Title?.Trim() is { Length: > 0 } title ? title : null,
                Description: Meta("name", "description"),
                HasLang: string.IsNullOrWhiteSpace(document.DocumentElement.GetAttribute("lang")) is false,
                HasCanonical: HasLink("canonical"),
                HasHreflang: links.Any(link => link.HasAttribute("hreflang")),
                IsUnderACulture: CulturePath().IsMatch(response.Url.AbsolutePath),
                NoIndex: robots.Contains("noindex", StringComparison.OrdinalIgnoreCase) || robots.Contains("none", StringComparison.OrdinalIgnoreCase),
                TextLength: VisibleTextLength(document),
                MissingOpenGraph: missingOpenGraph,
                ImagesWithoutAlt: document.QuerySelectorAll("img").Count(image => image.HasAttribute("alt") is false));
        }

        /// <summary>The text a reader sees in the body, scripts, styles and templates left out, whitespace collapsed.</summary>
        private static int VisibleTextLength(IDocument document)
        {
            if (document.Body is null)
                return 0;

            foreach (var hidden in document.Body.QuerySelectorAll("script, style, noscript, template").ToArray())
            {
                hidden.Remove();
            }

            return Whitespace().Replace(document.Body.TextContent, " ").Trim().Length;
        }
    }

    /// <summary>A path that starts with a culture, such as /en-US/ or /fa-IR/about, as a bit app's pages do.</summary>
    /// <summary>A page's path without its culture, so its cultures are one page.</summary>
    private static string PageOf(Page page) => CulturePath().Replace(page.Path, "/", 1);

    [GeneratedRegex("^/[a-z]{2,3}(-[A-Za-z]{2,4})?(/|$)")]
    private static partial Regex CulturePath();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
