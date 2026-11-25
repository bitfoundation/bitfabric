using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// The version tags of bitfoundation/bitplatform, prereleases included, read from its git refs on GitHub and kept for a
/// quarter of an hour: the newest is the code the source code index serves (See DocsMcpCodeIndexer), and every one newer
/// than a project's is a version the GetBitPlatformReleases tool counts. A prerelease is a tag alone in bitplatform, which
/// publishes a GitHub release for a stable version only (See <see cref="BitPlatformReleases"/>).
/// </summary>
public class BitPlatformTags(IHttpClientFactory httpClientFactory, TimeProvider timeProvider, ILogger<BitPlatformTags> logger)
{
    /// <summary>Every tag that starts with v-, in one page today.</summary>
    private const string TagsUrl = "https://api.github.com/repos/bitfoundation/bitplatform/git/matching-refs/tags/v-";

    private const string CommitsUrl = "https://api.github.com/repos/bitfoundation/bitplatform/commits";

    private static readonly TimeSpan failedDateLifetime = TimeSpan.FromMinutes(1);

    private readonly GitHubCache<BitPlatformTag[]> tags = new(timeProvider, logger, "the bit platform tags", cancellationToken => Read(httpClientFactory, cancellationToken));

    /// <summary>The date of each tag asked for, read once: a tag never moves.</summary>
    private readonly ConcurrentDictionary<string, DateTimeOffset> dates = new(StringComparer.Ordinal);

    /// <summary>After a date GitHub didn't give, no other is asked for until then.</summary>
    private DateTimeOffset datesFailedUntil;

    /// <summary>Newest first. Null when GitHub could not be read and nothing was read before.</summary>
    public Task<BitPlatformTag[]?> GetTags(CancellationToken cancellationToken) => tags.Get(cancellationToken);

    /// <summary>Null when GitHub could not be read and nothing was read before.</summary>
    public async Task<BitPlatformTag?> GetNewest(CancellationToken cancellationToken) => (await GetTags(cancellationToken))?.FirstOrDefault();

    /// <summary>
    /// The date of the commit <paramref name="tag"/> names, for a version no GitHub release dates. A request of its own for
    /// each tag, kept for good, so a caller asks for a few at most. Null when GitHub could not be read.
    /// </summary>
    public async Task<DateTimeOffset?> GetDate(BitPlatformTag tag, CancellationToken cancellationToken)
    {
        if (dates.TryGetValue(tag.Tag, out var known)) return known;

        if (timeProvider.GetUtcNow() < datesFailedUntil) return null;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            var commits = await GitHubApi.Read(httpClientFactory, $"{CommitsUrl}?sha={Uri.EscapeDataString(tag.Tag)}&per_page=1", timeout.Token);
            var date = DateTimeOffset.Parse(commits[0]!["commit"]!["committer"]!["date"]!.GetValue<string>(), CultureInfo.InvariantCulture);

            return dates[tag.Tag] = date;
        }
        catch (Exception exp) when (cancellationToken.IsCancellationRequested is false)
        {
            logger.LogWarning(exp, "Reading the date of {Tag} from GitHub failed.", tag.Tag);

            datesFailedUntil = timeProvider.GetUtcNow() + failedDateLifetime;
            return null;
        }
    }

    private static async Task<BitPlatformTag[]> Read(IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        // Each ref is named refs/tags/<tag>.
        BitPlatformTag[] read = [.. (await GitHubApi.ReadList(httpClientFactory, TagsUrl, cancellationToken))
                                    .Select(gitRef => gitRef["ref"]!.GetValue<string>())
                                    .Select(name => BitPlatformTag.TryParse(name[(name.LastIndexOf('/') + 1)..]))
                                    .OfType<BitPlatformTag>()
                                    .OrderDescending()];

        return read.Length > 0 ? read : throw new InvalidOperationException("GitHub listed no version tag of bitfoundation/bitplatform.");
    }
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
