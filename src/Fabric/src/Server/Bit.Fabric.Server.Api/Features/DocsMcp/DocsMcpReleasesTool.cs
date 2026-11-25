using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol;

namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// Tells an agent whether the bit platform version a project is on is the newest, a prerelease included, and what changed
/// since: the versions are bitfoundation/bitplatform's tags (See <see cref="BitPlatformTags"/>), and the notes those of
/// bitplatform's and bitfabric's GitHub releases (See <see cref="BitPlatformReleases"/>).
/// </summary>
public partial class DocsMcpReleasesTool(BitPlatformTags bitPlatformTags, BitPlatformReleases bitPlatformReleases)
{
    public const string ToolName = "GetBitPlatformReleases";

    private const int MaxNotesLength = 24_000;

    private const int MaxReleaseNotesLength = 6_000;

    /// <summary>
    /// How many of the versions no release dates get a date read for: a request each, and a project far behind is behind
    /// by dozens of old prereleases that have no release notes.
    /// </summary>
    private const int MaxDatedTags = 10;

    [GeneratedRegex(@"^v?-?(?<version>\d+\.\d+\.\d+)(-pre-(?<prerelease>\d+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    public static readonly Tool Definition = new()
    {
        Name = ToolName,
        Title = "Check for a newer bit platform version",
        Description = """
            Tells whether a bit platform version is the newest and, when it is not, how many versions are newer and what
            changed in each, newest first. Prereleases count: bit platform ships one often, and its MCP servers answer from
            the newest code, so a project on an older prerelease is behind. bit Boilerplate, bit BlazorUI and every other
            bit library ship together under one version number, so one answer covers all of them. Pass the version a
            project is on as its bit.json records it; without one, the version this connection asked for (the v query of
            this endpoint) is checked. The versions are the tags of bitfoundation/bitplatform. The notes are those of the
            GitHub releases of bitfoundation/bitplatform, for a stable version, and of bitfoundation/bitfabric, which holds
            bit Boilerplate and bit CLI, for every version from 11.0.0-pre-24 on; a version neither has a release for is
            listed with its tag and date alone.
            """,
        InputSchema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["version"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "The version to check, as bit.json records it, such as 11.0.0-pre-24 or 11.0.0."
                }
            }
        }),
        Annotations = new()
        {
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true,
            OpenWorldHint = true
        }
    };

    /// <param name="request">The call, with the version to check, if it names one.</param>
    /// <param name="requestedVersion">The v query of the connection, checked when the call names no version.</param>
    /// <param name="cancellationToken">Ends the call.</param>
    public async ValueTask<CallToolResult> Call(CallToolRequestParams request, string? requestedVersion, CancellationToken cancellationToken)
    {
        var version = DocsMcpToolResults.Argument(request, "version") ?? requestedVersion?.Trim();

        if (string.IsNullOrEmpty(version))
            return DocsMcpToolResults.Failed("Pass version: the bit platform version the project is on, as its bit.json records it.");

        if (VersionRegex().Match(version) is not { Success: true } match)
            return DocsMcpToolResults.Failed($"{version} is not a bit platform version. Pass it as bit.json records it, such as 11.0.0-pre-24 or 11.0.0.");

        // As its tag would read, so it compares with the tags: v-11.0.0-pre-22 for 11.0.0-pre-22, V-11.0.0-PRE-22 or v11.0.0-pre-22.
        var current = BitPlatformTag.TryParse($"v-{match.Groups["version"].Value}{(match.Groups["prerelease"].Success ? $"-pre-{match.Groups["prerelease"].Value}" : "")}")!;

        var tags = await bitPlatformTags.GetTags(cancellationToken);
        var releases = await bitPlatformReleases.GetReleases(cancellationToken);

        if (tags is not { Length: > 0 } || releases is null)
            return DocsMcpToolResults.Failed("The bit platform versions could not be read from GitHub. Read https://github.com/bitfoundation/bitplatform/tags and https://github.com/bitfoundation/bitfabric/releases instead.");

        var newest = tags[0];
        // Newest first, as the tags are.
        var newer = tags.TakeWhile(tag => tag.CompareTo(current) > 0).ToArray();
        // bitplatform's first, as the releases are.
        var releasesOf = releases.ToLookup(release => release.Name, StringComparer.Ordinal);
        var dates = await DatesOf(newer.Length is 0 ? [newest] : newer, releasesOf, cancellationToken);

        string Dated(BitPlatformTag tag)
            => releasesOf[tag.Name].FirstOrDefault() is { } release ? $", published {release.PublishedOn}"
             : dates.TryGetValue(tag.Name, out var date) ? $", dated {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
             : "";

        string Link(BitPlatformTag tag) => releasesOf[tag.Name].FirstOrDefault()?.Url ?? TagUrl(tag);

        if (newer.Length is 0)
        {
            return DocsMcpToolResults.Succeeded(current.CompareTo(newest) is 0
                ? $"{version} is up to date: the newest version is {newest.Name}{Dated(newest)}, {Link(newest)}"
                : $"{version} is up to date: no version is newer, though none is tagged {current.Name} either; the newest is {newest.Name}{Dated(newest)}, {Link(newest)}");
        }

        StringBuilder text = new($"{version} is behind: {newer.Length} newer version{(newer.Length is 1 ? "" : "s")}, the newest being {newest.Name}{Dated(newest)}. Newest first, with the release notes of each that has them:");

        foreach (var tag in newer)
        {
            text.AppendLine().AppendLine();

            if (releasesOf[tag.Name].Any() is false)
            {
                text.Append($"{tag.Name}{Dated(tag)}: no release notes, {TagUrl(tag)}");
                continue;
            }

            var first = true;

            foreach (var release in releasesOf[tag.Name])
            {
                if (first is false)
                {
                    text.AppendLine().AppendLine();
                }

                first = false;

                var title = release.Repository is "bitfabric" ? $"{release.Name} of bit Boilerplate and bit CLI" : release.Name;

                // Past this, a release is only named and linked: an agent reads the newest notes first, and a long gap of
                // versions would otherwise fill its context with the oldest ones.
                if (text.Length > MaxNotesLength)
                {
                    text.Append($"{title}, published {release.PublishedOn}: {release.Url}");
                    continue;
                }

                text.AppendLine($"# {title}, published {release.PublishedOn}, {release.Url}").AppendLine();
                text.Append(release.Notes.Length > MaxReleaseNotesLength ? $"{release.Notes[..MaxReleaseNotesLength]}\n\n(cut short, the full notes are at {release.Url})" : release.Notes);
            }
        }

        return DocsMcpToolResults.Succeeded(text.ToString());
    }

    /// <summary>The dates of the newest <see cref="MaxDatedTags"/> of <paramref name="tags"/> that no release dates, by name.</summary>
    private async Task<Dictionary<string, DateTimeOffset>> DatesOf(BitPlatformTag[] tags, ILookup<string, BitPlatformRelease> releasesOf, CancellationToken cancellationToken)
    {
        var dated = await Task.WhenAll(tags.Where(tag => releasesOf[tag.Name].Any() is false)
                                           .Take(MaxDatedTags)
                                           .Select(async tag => (tag.Name, Date: await bitPlatformTags.GetDate(tag, cancellationToken))));

        return dated.Where(entry => entry.Date is not null).ToDictionary(entry => entry.Name, entry => entry.Date!.Value, StringComparer.Ordinal);
    }

    /// <summary>GitHub's page of a tag, which it shows for a tag without a release too.</summary>
    private static string TagUrl(BitPlatformTag tag) => $"https://github.com/bitfoundation/bitplatform/releases/tag/{tag.Tag}";
}
