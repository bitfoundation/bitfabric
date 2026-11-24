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
/// codebase-memory command line over the sources of each served release of bitfoundation/bitplatform.
/// </summary>
public class DocsMcpCodeIndexOptions
{
    /// <summary>
    /// Holds the command line, the sources of every served release and their index (See DocsMcpCodeIndexer). It must be
    /// writable by the app and outlive a deployment, as indexing a release takes minutes: in the published Docker Compose
    /// project it is on the api's <c>docs-mcp</c> volume (See DockerComposePublishingExtensions). Empty turns the index off,
    /// and the source code tools then say so.
    /// </summary>
    public string? DataDirectoryPath { get; set; }

    /// <summary>
    /// The lowest release served. Every stable release at or above it gets an index of its own, and a caller that names no
    /// release, or one that is not served, gets the newest of them.
    /// </summary>
    public string MinimumVersion { get; set; } = "10.6.0";

    /// <summary>How often a release published since the app started is picked up.</summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// The release of https://github.com/DeusData/codebase-memory-mcp installed into <see cref="DataDirectoryPath"/>, its
    /// archive checked against the checksums published with it. A new version is installed next to the old one.
    /// </summary>
    public string CliVersion { get; set; } = "0.12.1";

    /// <summary>An executable to run instead of the one installed from <see cref="CliVersion"/>, such as a developer's own.</summary>
    public string? CliPath { get; set; }
}
