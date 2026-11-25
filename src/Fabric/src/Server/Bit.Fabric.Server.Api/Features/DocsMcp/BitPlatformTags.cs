using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// The newest version tag of bitfoundation/bitplatform, a prerelease included, read from its git refs on GitHub and kept
/// for a quarter of an hour: the code the source code index serves (See DocsMcpCodeIndexer). GitHub has a release for a
/// stable version alone, so a prerelease is found among the tags, not among the releases <see cref="BitPlatformReleases"/> reads.
/// </summary>
public class BitPlatformTags(IHttpClientFactory httpClientFactory, TimeProvider timeProvider, ILogger<BitPlatformTags> logger)
{
    /// <summary>Every tag that starts with v-, in one answer today; a Link header's next page is followed should GitHub page it.</summary>
    private const string TagsUrl = "https://api.github.com/repos/bitfoundation/bitplatform/git/matching-refs/tags/v-";

    private static readonly TimeSpan cacheLifetime = TimeSpan.FromMinutes(15);

    /// <summary>A read that failed is retried this much later rather than on every call in between.</summary>
    private static readonly TimeSpan failedReadLifetime = TimeSpan.FromMinutes(1);

    /// <summary>Bounds the pages followed, so a Link header that never ends can't hold the read.</summary>
    private const int MaxPages = 20;

    private readonly SemaphoreSlim sync = new(1, 1);

    private BitPlatformTag? newest;

    private DateTimeOffset expiresAt;

    /// <summary>Null when GitHub could not be read and nothing was read before.</summary>
    public async Task<BitPlatformTag?> GetNewest(CancellationToken cancellationToken)
    {
        if (timeProvider.GetUtcNow() < expiresAt) return newest;

        await sync.WaitAsync(cancellationToken);

        try
        {
            if (timeProvider.GetUtcNow() < expiresAt) return newest;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            var http = httpClientFactory.CreateClient(DocsMcpHttp.ClientName);
            List<BitPlatformTag> tags = [];
            string? url = TagsUrl;

            for (var page = 0; url is not null && page < MaxPages; page++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.ParseAdd("application/vnd.github+json");

                using var response = await http.SendAsync(request, timeout.Token);
                response.EnsureSuccessStatusCode();

                var refs = JsonNode.Parse(await response.Content.ReadAsStringAsync(timeout.Token))!.AsArray();

                // Each ref is named refs/tags/<tag>.
                tags.AddRange(refs.Select(gitRef => gitRef!["ref"]!.GetValue<string>())
                                  .Select(name => BitPlatformTag.TryParse(name[(name.LastIndexOf('/') + 1)..]))
                                  .OfType<BitPlatformTag>());

                url = NextPage(response);
            }

            newest = tags.Max() ?? throw new InvalidOperationException("GitHub listed no version tag of bitfoundation/bitplatform.");

            expiresAt = timeProvider.GetUtcNow() + cacheLifetime;
        }
        catch (Exception exp) when (cancellationToken.IsCancellationRequested is false)
        {
            logger.LogError(exp, "Reading the bit platform tags from GitHub failed.");

            expiresAt = timeProvider.GetUtcNow() + failedReadLifetime;
        }
        finally
        {
            sync.Release();
        }

        return newest;
    }

    /// <summary>The address of the next page a Link header names, as GitHub pages its lists: &lt;url&gt;; rel="next".</summary>
    private static string? NextPage(HttpResponseMessage response)
        => response.Headers.TryGetValues("Link", out var links)
            ? links.SelectMany(link => link.Split(','))
                   .Select(link => link.Split(';', StringSplitOptions.TrimEntries))
                   .Where(parts => parts.Length > 1 && parts.Skip(1).Contains("rel=\"next\""))
                   .Select(parts => parts[0].TrimStart('<').TrimEnd('>'))
                   .FirstOrDefault()
            : null;
}

/// <summary>A version tag of bitfoundation/bitplatform, such as v-11.0.0-pre-24 or v-11.0.0.</summary>
/// <param name="Tag">The tag as it is in git, such as v-11.0.0-pre-24.</param>
/// <param name="Number">Three parts, such as 11.0.0.</param>
/// <param name="Prerelease">The prerelease's number, such as 24 of 11.0.0-pre-24; null for a stable version.</param>
public sealed partial record BitPlatformTag(string Tag, Version Number, int? Prerelease) : IComparable<BitPlatformTag>
{
    [GeneratedRegex(@"^v-(?<version>\d+\.\d+\.\d+)(-pre-(?<prerelease>\d+))?$")]
    private static partial Regex TagRegex();

    /// <summary>The version a project's bit.json records, such as 11.0.0-pre-24 or 11.0.0.</summary>
    public string Name => Tag["v-".Length..];

    /// <returns>Null for a tag that is neither v-X.Y.Z nor v-X.Y.Z-pre-NN.</returns>
    public static BitPlatformTag? TryParse(string tag)
        => TagRegex().Match(tag) is { Success: true } match
            ? new(tag, Version.Parse(match.Groups["version"].Value), match.Groups["prerelease"].Success ? int.Parse(match.Groups["prerelease"].Value, CultureInfo.InvariantCulture) : null)
            : null;

    /// <summary>By number, a stable version coming after every prerelease of the same number: 11.0.0-pre-24, then 11.0.0.</summary>
    public int CompareTo(BitPlatformTag? other)
    {
        if (other is null) return 1;

        var byNumber = Number.CompareTo(other.Number);

        if (byNumber is not 0) return byNumber;

        return (Prerelease, other.Prerelease) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            ({ } prerelease, { } otherPrerelease) => prerelease.CompareTo(otherPrerelease)
        };
    }
}
