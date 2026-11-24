using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol;

namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>Tells an agent whether the bit platform version a project is on is the newest, and what changed since.</summary>
public partial class DocsMcpReleasesTool(BitPlatformReleases bitPlatformReleases)
{
    public const string ToolName = "GetBitPlatformReleases";

    private const int MaxNotesLength = 24_000;

    private const int MaxReleaseNotesLength = 6_000;

    [GeneratedRegex(@"^v?-?(?<version>\d+\.\d+\.\d+)(?<prerelease>-\S+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    public static readonly Tool Definition = new()
    {
        Name = ToolName,
        Title = "Check for a newer bit platform release",
        Description = """
            Tells whether a bit platform version is the newest release and, when it is not, returns the release notes of
            every newer release, newest first. bit Boilerplate, bit BlazorUI and every other bit library ship together
            under one version number, so one answer covers all of them. Pass the version a project is on as its bit.json
            records it; without one, the release this connection asked for (the v query of this endpoint) is checked.
            Only stable releases count, so a prerelease ahead of the newest release is up to date. The notes come from the
            GitHub releases of bitfoundation/bitplatform.
            """,
        InputSchema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["version"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "The version to check, such as 10.6.0 or 10.7.0."
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
            return DocsMcpToolResults.Failed($"{version} is not a bit platform version. Pass it as bit.json records it, such as 10.6.0 or 10.7.0.");

        var number = Version.Parse(match.Groups["version"].Value);
        var isPrerelease = match.Groups["prerelease"].Success;

        var all = await bitPlatformReleases.GetReleases(cancellationToken);

        if (all is not { Length: > 0 })
            return DocsMcpToolResults.Failed("The release list could not be read from GitHub. Read https://github.com/bitfoundation/bitplatform/releases instead.");

        var latest = all[0];
        var newer = all.Where(release => release.Number > number || (release.Number == number && isPrerelease)).ToArray();

        if (newer.Length is 0)
            return DocsMcpToolResults.Succeeded($"{version} is up to date: the newest release is {latest.Name}, published {latest.PublishedOn}, {latest.Url}");

        StringBuilder text = new($"{version} is behind: {newer.Length} newer release{(newer.Length is 1 ? "" : "s")}, the newest being {latest.Name}, published {latest.PublishedOn}. Release notes, newest first:");

        foreach (var release in newer)
        {
            text.AppendLine().AppendLine();

            // Past this, a release is only named and linked: an agent reads the newest notes first, and a long gap of
            // releases would otherwise fill its context with the oldest ones.
            if (text.Length > MaxNotesLength)
            {
                text.Append($"{release.Name}, published {release.PublishedOn}: {release.Url}");
                continue;
            }

            text.AppendLine($"# {release.Name}, published {release.PublishedOn}, {release.Url}").AppendLine();
            text.Append(release.Notes.Length > MaxReleaseNotesLength ? $"{release.Notes[..MaxReleaseNotesLength]}\n\n(cut short, the full notes are at {release.Url})" : release.Notes);
        }

        return DocsMcpToolResults.Succeeded(text.ToString());
    }
}
