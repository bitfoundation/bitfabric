using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// The stable releases of bitfoundation/bitplatform, read from its GitHub releases and kept for a quarter of an hour: what
/// the GetBitPlatformReleases tool compares a version against. Every library, the project template and its tools ship
/// together under one version number. The source code index reads the newest tag instead, a prerelease included (See
/// <see cref="BitPlatformTags"/>).
/// </summary>
public partial class BitPlatformReleases(IHttpClientFactory httpClientFactory, TimeProvider timeProvider, ILogger<BitPlatformReleases> logger)
{
    private const string ReleasesUrl = "https://api.github.com/repos/bitfoundation/bitplatform/releases?per_page=100";

    private static readonly TimeSpan cacheLifetime = TimeSpan.FromMinutes(15);

    /// <summary>A read that failed is retried this much later rather than on every call in between.</summary>
    private static readonly TimeSpan failedReadLifetime = TimeSpan.FromMinutes(1);

    /// <summary>Release tags only: v-1.6.2-pre-01 and the like are prereleases, which the releases tool doesn't count.</summary>
    [GeneratedRegex(@"^v-(?<version>\d+\.\d+\.\d+)$")]
    private static partial Regex ReleaseTagRegex();

    private readonly SemaphoreSlim sync = new(1, 1);

    private BitPlatformRelease[]? releases;

    private DateTimeOffset expiresAt;

    /// <summary>Newest first. Null when GitHub could not be read and nothing was read before.</summary>
    public async Task<BitPlatformRelease[]?> GetReleases(CancellationToken cancellationToken)
    {
        if (timeProvider.GetUtcNow() < expiresAt) return releases;

        await sync.WaitAsync(cancellationToken);

        try
        {
            if (timeProvider.GetUtcNow() < expiresAt) return releases;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesUrl);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await httpClientFactory.CreateClient(DocsMcpHttp.ClientName).SendAsync(request, timeout.Token);
            response.EnsureSuccessStatusCode();

            var listed = JsonNode.Parse(await response.Content.ReadAsStringAsync(timeout.Token))!.AsArray();

            releases = [.. listed.Select(release => (Release: release!, Match: ReleaseTagRegex().Match(release!["tag_name"]!.GetValue<string>())))
                                 .Where(listing => listing.Match.Success && listing.Release["draft"]!.GetValue<bool>() is false && listing.Release["prerelease"]!.GetValue<bool>() is false)
                                 .Select(listing => new BitPlatformRelease(Version.Parse(listing.Match.Groups["version"].Value),
                                                                           listing.Release["tag_name"]!.GetValue<string>(),
                                                                           listing.Release["html_url"]!.GetValue<string>(),
                                                                           DateTimeOffset.Parse(listing.Release["published_at"]!.GetValue<string>(), CultureInfo.InvariantCulture),
                                                                           listing.Release["body"]?.GetValue<string>()?.Trim() ?? ""))
                                 .OrderByDescending(release => release.Number)];

            expiresAt = timeProvider.GetUtcNow() + cacheLifetime;
        }
        catch (Exception exp) when (cancellationToken.IsCancellationRequested is false)
        {
            logger.LogError(exp, "Reading the bit platform releases from GitHub failed.");

            expiresAt = timeProvider.GetUtcNow() + failedReadLifetime;
        }
        finally
        {
            sync.Release();
        }

        return releases;
    }
}

/// <param name="Number">Three parts, such as 10.7.0.</param>
/// <param name="Tag">Its git tag, such as v-10.7.0.</param>
/// <param name="Url">Its page on GitHub.</param>
/// <param name="PublishedAt">When it was published on GitHub.</param>
/// <param name="Notes">Its release notes, in Markdown.</param>
public sealed record BitPlatformRelease(Version Number, string Tag, string Url, DateTimeOffset PublishedAt, string Notes)
{
    /// <summary>The three part version, such as 10.7.0.</summary>
    public string Name { get; } = Number.ToString(3);

    public string PublishedOn => PublishedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
