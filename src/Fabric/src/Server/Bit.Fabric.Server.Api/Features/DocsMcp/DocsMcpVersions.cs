namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// The releases the source code index serves, as DocsMcpCodeIndexer finishes indexing each, and the ?v= a caller sends
/// resolved against them. Empty until the first one is ready, and on a server with no index configured, when
/// <see cref="Unavailable"/> says why.
/// </summary>
public sealed class DocsMcpVersions(ServerApiSettings settings)
{
    // One record, so a reader never sees a new generation next to the previous set.
    private Snapshot snapshot = new([], 0, string.IsNullOrWhiteSpace(settings.DocsMcp.CodeIndex.DataDirectoryPath)
        ? "this server has no source code index configured"
        : "the source code index is still being built; retry in a few minutes");

    /// <summary>Newest first.</summary>
    public IReadOnlyList<DocsMcpVersion> Served => snapshot.Served;

    /// <summary>
    /// Changes on every change to <see cref="Served"/>, so the proxy drops the tool lists it cached before the index was
    /// ready - the source code tools are listed with the index's own schemas only once there is one.
    /// </summary>
    public int Generation => snapshot.Generation;

    public DocsMcpVersion? Latest => snapshot.Served.FirstOrDefault();

    /// <summary>Why the source code tools can't answer, while no release is served; null once one is.</summary>
    public string? Unavailable => snapshot.Served.Length is 0 ? snapshot.Problem : null;

    /// <summary>A release that is not served, and a caller that names none, both get the newest one.</summary>
    public DocsMcpVersion? Resolve(string? requested)
    {
        var served = snapshot.Served;

        return (string.IsNullOrWhiteSpace(requested) ? null : served.FirstOrDefault(version => version.Name == requested.Trim())) ?? served.FirstOrDefault();
    }

    /// <summary>
    /// Names the release the source code tools read for a caller, for the server instructions: the only way a caller learns
    /// that the ?v= it sent is not served and an older release answers instead - a project created from a prerelease asks
    /// for a version that is only served once it is released.
    /// </summary>
    public string Describe(string? requested)
    {
        if (Resolve(requested) is not { } version)
            return $"Its source code tools don't answer for now: {Unavailable}. Every other tool does.";

        if (string.IsNullOrWhiteSpace(requested))
            return $"Its source code tools read release {version.Name}, the newest one, as this connection names no release (the v query of this endpoint).";

        if (version.Name == requested.Trim())
            return $"Its source code tools read release {version.Name}, the one this connection asked for.";

        return $"This connection asked for release {requested.Trim()}, which this endpoint's source code index does not hold (it is not released yet, or not ready here), so its source code tools read {version.Name}, the newest release it holds: code added after {version.Name} is missing from their answers rather than absent from the repository.";
    }

    /// <summary>Serves <paramref name="version"/>, as DocsMcpCodeIndexer finishes indexing it.</summary>
    public void Add(DocsMcpVersion version)
    {
        var current = snapshot;

        snapshot = current with
        {
            Served = [.. current.Served.Where(served => served.Name != version.Name).Append(version).OrderByDescending(served => served.Release.Number)],
            Generation = current.Generation + 1
        };
    }

    /// <summary>What the source code tools answer while no release is served.</summary>
    public void ReportProblem(string problem) => snapshot = snapshot with { Problem = problem };

    private sealed record Snapshot(DocsMcpVersion[] Served, int Generation, string Problem);
}

/// <param name="Release">The release, as GitHub lists it.</param>
/// <param name="SourcesPath">The directory the release's sources were extracted to and indexed from.</param>
/// <param name="CodebaseMemoryProject">The name codebase-memory stored the index under, which every source tool call names.</param>
public sealed record DocsMcpVersion(BitPlatformRelease Release, string SourcesPath, string CodebaseMemoryProject)
{
    /// <summary>The three part version a caller passes as ?v=, such as 10.7.0.</summary>
    public string Name => Release.Name;
}
