using System.Xml.Linq;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;

/// <summary>
/// robots.txt and the sitemaps it names, read through <see cref="ScanHttp"/> as a crawler reads them: the SEO scan judges
/// them, and the browser scans take a couple of their pages to load besides the home page.
/// </summary>
public partial class SitemapReader(ScanHttp http)
{
    /// <summary>The pages robots.txt's sitemaps, or /sitemap.xml, list on <paramref name="origin"/>, without judging them.</summary>
    public async Task<List<string>> Pages(string origin, int maxPages, CancellationToken cancellationToken)
    {
        List<ScanFindingResult> ignored = [];

        return await ReadSitemaps(origin, await ReadRobots(origin, ignored, cancellationToken), ignored, maxPages, cancellationToken);
    }

    /// <summary>robots.txt's verdict for every crawler, and the sitemaps it names.</summary>
    public async Task<List<string>> ReadRobots(string origin, List<ScanFindingResult> findings, CancellationToken cancellationToken)
    {
        var robots = await http.Get($"{origin}/robots.txt", cancellationToken);

        if (robots is not { IsOk: true, Body: not null } || robots.IsHtml)
        {
            findings.Add(new(ScanFindings.RobotsTxtMissing, ScanFindingSeverity.Improvement, [], [$"{origin}/robots.txt"]));
            return [];
        }

        List<string> sitemaps = [];
        var inGroupForEveryone = false;
        var lastWasAgent = false;

        foreach (var rawLine in robots.Body.Split('\n'))
        {
            var line = rawLine.Split('#', 2)[0].Trim();
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator < 0)
                continue;

            var field = line[..separator].Trim().ToLowerInvariant();
            var value = line[(separator + 1)..].Trim();

            switch (field)
            {
                case "sitemap":
                    sitemaps.Add(value);
                    break;
                case "user-agent":
                    // Consecutive user-agent lines open one group together.
                    inGroupForEveryone = (lastWasAgent && inGroupForEveryone) || value is "*";
                    break;
                case "disallow" when inGroupForEveryone && value is "/":
                    findings.Add(new(ScanFindings.RobotsBlocksAll, ScanFindingSeverity.Urgent, [], [$"{origin}/robots.txt"]));
                    inGroupForEveryone = false;
                    break;
            }

            lastWasAgent = field is "user-agent";
        }

        if (sitemaps.Count is 0)
        {
            findings.Add(new(ScanFindings.RobotsTxtNoSitemap, ScanFindingSeverity.Improvement, [], [$"{origin}/robots.txt"]));
        }

        return sitemaps;
    }

    /// <summary>
    /// The pages of the app's sitemaps, about <paramref name="maxPages"/> at most, following a sitemap index one level;
    /// /sitemap.xml when robots.txt names none.
    /// </summary>
    public async Task<List<string>> ReadSitemaps(string origin, List<string> named, List<ScanFindingResult> findings, int maxPages, CancellationToken cancellationToken)
    {
        Queue<string> sitemaps = new(named.Where(url => SameOrigin(url, origin)).DefaultIfEmpty($"{origin}/sitemap.xml"));
        List<string> pages = [];
        var foundAny = false;
        var read = 0;

        while (sitemaps.TryDequeue(out var sitemapUrl) && read++ < 5 && pages.Count < maxPages)
        {
            var sitemap = await http.Get(sitemapUrl, cancellationToken, followRedirects: true);

            if (sitemap is not { IsOk: true, Body: not null } || sitemap.IsHtml)
                continue;

            try
            {
                var root = XDocument.Parse(sitemap.Body).Root;
                if (root is null)
                    continue;

                var locations = root.Descendants().Where(element => element.Name.LocalName is "loc").Select(element => element.Value.Trim()).Where(url => SameOrigin(url, origin));

                if (root.Name.LocalName is "sitemapindex")
                {
                    foreach (var child in locations)
                    {
                        sitemaps.Enqueue(child);
                    }
                }
                else
                {
                    pages.AddRange(locations);
                }

                foundAny = true;
            }
            catch (System.Xml.XmlException)
            {
                // Not a sitemap after all.
            }
        }

        if (foundAny is false)
        {
            findings.Add(new(ScanFindings.SitemapMissing, ScanFindingSeverity.Improvement, [], [named.FirstOrDefault() ?? $"{origin}/sitemap.xml"]));
        }

        return pages;
    }

    public static bool SameOrigin(string url, string origin)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && string.Equals(uri.GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase);
}
