namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// The GitHub releases of bitfoundation/bitplatform and bitfoundation/bitfabric, kept for a quarter of an hour: the release
/// notes the GetBitPlatformReleases tool shows for a version. bitplatform publishes one for each stable version, with the
/// notes of every library; bitfabric one for each version from 11.0.0-pre-24 on, prereleases included, with the notes of
/// bit Boilerplate and bit CLI, which it holds. Every library, the project template and its tools ship together under one
/// version number, and which versions there are is <see cref="BitPlatformTags"/>' to say.
/// </summary>
public class BitPlatformReleases(IHttpClientFactory httpClientFactory, TimeProvider timeProvider, ILogger<BitPlatformReleases> logger)
{
    private readonly GitHubCache<BitPlatformRelease[]> bitPlatform = new(timeProvider, logger, "the releases of bitfoundation/bitplatform", cancellationToken => Read(httpClientFactory, "bitplatform", cancellationToken));

    private readonly GitHubCache<BitPlatformRelease[]> bitFabric = new(timeProvider, logger, "the releases of bitfoundation/bitfabric", cancellationToken => Read(httpClientFactory, "bitfabric", cancellationToken));

    /// <summary>bitplatform's, then bitfabric's. Null when either could not be read and nothing of it was read before.</summary>
    public async Task<BitPlatformRelease[]?> GetReleases(CancellationToken cancellationToken)
    {
        var bitPlatformReleases = await bitPlatform.Get(cancellationToken);
        var bitFabricReleases = await bitFabric.Get(cancellationToken);

        return bitPlatformReleases is null || bitFabricReleases is null ? null : [.. bitPlatformReleases, .. bitFabricReleases];
    }

    /// <summary>Its published releases of a version tag (v-X.Y.Z or v-X.Y.Z-pre-NN), drafts left out.</summary>
    private static async Task<BitPlatformRelease[]> Read(IHttpClientFactory httpClientFactory, string repository, CancellationToken cancellationToken)
        => [.. (await GitHubApi.ReadList(httpClientFactory, $"https://api.github.com/repos/bitfoundation/{repository}/releases?per_page=100", cancellationToken))
               .Where(release => release["draft"]!.GetValue<bool>() is false)
               .Select(release => (Release: release, Tag: BitPlatformTag.TryParse(release["tag_name"]!.GetValue<string>())))
               .Where(listing => listing.Tag is not null)
               .Select(listing => new BitPlatformRelease(listing.Tag!,
                                                         repository,
                                                         listing.Release["html_url"]!.GetValue<string>(),
                                                         DateTimeOffset.Parse(listing.Release["published_at"]!.GetValue<string>(), CultureInfo.InvariantCulture),
                                                         listing.Release["body"]?.GetValue<string>()?.Trim() ?? ""))];
}

/// <param name="Tag">The version it was published for.</param>
/// <param name="Repository">The repository of bitfoundation it is a release of: bitplatform or bitfabric.</param>
/// <param name="Url">Its page on GitHub.</param>
/// <param name="PublishedAt">When it was published on GitHub.</param>
/// <param name="Notes">Its release notes, in Markdown.</param>
public sealed record BitPlatformRelease(BitPlatformTag Tag, string Repository, string Url, DateTimeOffset PublishedAt, string Notes)
{
    /// <summary>The version, such as 11.0.0-pre-24 or 11.0.0.</summary>
    public string Name => Tag.Name;

    public string PublishedOn => PublishedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
