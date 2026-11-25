namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// The one version the source code index serves: the newest tag of bitfoundation/bitplatform, a prerelease included, as
/// DocsMcpCodeIndexer finishes indexing it. Every source code tool call reads it. None until the first one is ready, and on
/// a server with no index configured, when <see cref="Unavailable"/> says why.
/// </summary>
public sealed class DocsMcpVersions(ServerApiSettings settings)
{
    // One record, so a reader never sees a new generation next to the version served before it.
    private Snapshot snapshot = new(null, 0, string.IsNullOrWhiteSpace(settings.DocsMcp.CodeIndex.DataDirectoryPath)
        ? "this server has no source code index configured"
        : "the source code index is still being built; retry in a few minutes");

    public DocsMcpVersion? Served => snapshot.Served;

    /// <summary>
    /// Changes whenever <see cref="Served"/> does, so the proxy drops the tool lists it cached before the index was ready -
    /// the source code tools are listed with the index's own schemas only once there is one.
    /// </summary>
    public int Generation => snapshot.Generation;

    /// <summary>Why the source code tools can't answer, while no version is served; null once one is.</summary>
    public string? Unavailable => snapshot.Served is null ? snapshot.Problem : null;

    /// <summary>Names the version the source code tools read, for the server instructions.</summary>
    public string Describe()
        => Served is { } served
            ? $"Its source code tools read {served.Name}, the newest bit platform tag indexed so far."
            : $"Its source code tools don't answer for now: {Unavailable}. Every other tool does.";

    /// <summary>Serves <paramref name="version"/> in place of the one served before, as DocsMcpCodeIndexer finishes indexing it.</summary>
    public void Serve(DocsMcpVersion version)
    {
        var current = snapshot;

        snapshot = current with { Served = version, Generation = current.Generation + 1 };
    }

    /// <summary>What the source code tools answer while no version is served.</summary>
    public void ReportProblem(string problem) => snapshot = snapshot with { Problem = problem };

    private sealed record Snapshot(DocsMcpVersion? Served, int Generation, string Problem);
}

/// <param name="Tag">The tag of bitfoundation/bitplatform it was read from.</param>
/// <param name="SourcesPath">The directory the tag's sources were extracted to and indexed from.</param>
/// <param name="CodebaseMemoryProject">The name codebase-memory stored the index under, which every source tool call names.</param>
public sealed record DocsMcpVersion(BitPlatformTag Tag, string SourcesPath, string CodebaseMemoryProject)
{
    /// <summary>The version, such as 11.0.0-pre-24 or 11.0.0.</summary>
    public string Name => Tag.Name;
}
