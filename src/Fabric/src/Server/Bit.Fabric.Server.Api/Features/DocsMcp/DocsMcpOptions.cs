namespace Bit.Fabric.Server.Api;

/// <summary>
/// The bit platform documentation MCP server at /docs-mcp. Usable as is: every tool but the source code ones answers
/// without settings, and those say why they don't until <see cref="CodeIndex"/> names a data directory.
/// </summary>
public class DocsMcpOptions
{
    /// <summary>
    /// Where the SendBitPlatformFeedback tool sends what an agent reports, through the app's own email settings. Empty
    /// leaves the tool refusing, so an agent is never told a report reached someone when it reached nobody.
    /// </summary>
    public string? FeedbackEmailAddress { get; set; }

    public DocsMcpCodeIndexOptions CodeIndex { get; set; } = new();
}

/// <summary>
/// The source code index the FindBitPlatformSymbols, SearchBitPlatformCode and GetBitPlatformSymbolSource tools read: the
/// codebase-memory command line over the sources of the newest tag of bitfoundation/bitplatform, a prerelease included.
/// </summary>
public class DocsMcpCodeIndexOptions
{
    /// <summary>
    /// Holds the command line, the sources of the version served and their index (See DocsMcpCodeIndexer). It must be
    /// writable by the app and outlive a deployment, as indexing a version takes minutes: in the published Docker Compose
    /// project it is on the api's <c>docs-mcp</c> volume (See DockerComposePublishingExtensions). Empty turns the index off,
    /// and the source code tools then say so.
    /// </summary>
    public string? DataDirectoryPath { get; set; }

    /// <summary>How often a newer tag, pushed since the one served, is looked for.</summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// The release of https://github.com/DeusData/codebase-memory-mcp installed into <see cref="DataDirectoryPath"/>, its
    /// archive checked against the checksums published with it. A new version is installed next to the old one.
    /// </summary>
    public string CliVersion { get; set; } = "0.12.1";

    /// <summary>An executable to run instead of the one installed from <see cref="CliVersion"/>, such as a developer's own.</summary>
    public string? CliPath { get; set; }
}
