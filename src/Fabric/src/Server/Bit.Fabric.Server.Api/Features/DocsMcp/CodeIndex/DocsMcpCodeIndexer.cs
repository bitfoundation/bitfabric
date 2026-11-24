namespace Bit.Fabric.Server.Api.Features.DocsMcp.CodeIndex;

/// <summary>
/// Keeps an index of every stable release of bitfoundation/bitplatform from <see cref="DocsMcpCodeIndexOptions.MinimumVersion"/>
/// on, so /docs-mcp?v=10.7.0 answers with the code of 10.7.0. Runs in the background: startup never waits for it, a release
/// that can't be prepared is only left out and tried again later, and without
/// <see cref="DocsMcpCodeIndexOptions.DataDirectoryPath"/> it does nothing at all.
/// <para>
/// The data directory holds <c>cli/&lt;version&gt;/</c> (the installed command line), <c>sources/&lt;tag&gt;/</c> (each
/// release's indexed sources) and <c>index/</c> (codebase-memory's own data). In the published Docker Compose project it is
/// the api's <c>docs-mcp</c> volume, so a deployment keeps them (See DockerComposePublishingExtensions).
/// </para>
/// </summary>
public class DocsMcpCodeIndexer(ServerApiSettings settings,
    BitPlatformReleases bitPlatformReleases,
    BitPlatformSources sources,
    CodebaseMemoryCli codebaseMemory,
    DocsMcpVersions versions,
    TimeProvider timeProvider,
    ILogger<DocsMcpCodeIndexer> logger) : BackgroundService
{
    /// <summary>How soon a release that could not be prepared, or a command line that could not be installed, is tried again.</summary>
    private static readonly TimeSpan retryInterval = TimeSpan.FromMinutes(15);

    private DocsMcpCodeIndexOptions Options => settings.DocsMcp.CodeIndex;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(Options.DataDirectoryPath))
        {
            logger.LogInformation("No data directory is configured for the source code index of /docs-mcp, so its source code tools say they are not available.");
            return;
        }

        // Everything below runs off the startup path.
        await Task.Yield();

        try
        {
            while (stoppingToken.IsCancellationRequested is false)
            {
                var complete = await TryRefresh(stoppingToken);

                await Task.Delay(complete ? Options.RefreshInterval : retryInterval, timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    /// <summary>
    /// <see cref="Refresh"/>, with a pass that throws logged and counted as a failed one: thrown out of the loop above, it
    /// would end it, leaving the source code tools without a release until the app restarts.
    /// </summary>
    private async Task<bool> TryRefresh(CancellationToken cancellationToken)
    {
        try
        {
            return await Refresh(cancellationToken);
        }
        catch (Exception exp) when (cancellationToken.IsCancellationRequested is false)
        {
            logger.LogError(exp, "Preparing the source code index of /docs-mcp failed, retrying in {RetryInterval}.", retryInterval);
            return false;
        }
    }

    /// <returns>Whether every release at or above the minimum is served now.</returns>
    private async Task<bool> Refresh(CancellationToken cancellationToken)
    {
        if (await codebaseMemory.EnsureInstalled(cancellationToken) is { } problem)
        {
            versions.ReportProblem(problem);
            return false;
        }

        if (await bitPlatformReleases.GetReleases(cancellationToken) is not { } releases)
        {
            versions.ReportProblem("the bit platform releases could not be read from GitHub; retry in a few minutes");
            return false;
        }

        // Three parts, since a configured 10.6 would otherwise carry a -1 build that no release can match.
        var minimum = Version.TryParse(Options.MinimumVersion, out var parsed) ? new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0)) : new Version(10, 6, 0);

        var complete = true;

        // Newest first, so the release a caller that names none gets is the first one to come up.
        foreach (var release in releases.Where(release => release.Number >= minimum))
        {
            if (versions.Served.Any(served => served.Name == release.Name)) continue;

            var sourcesPath = Path.Combine(Options.DataDirectoryPath!, "sources", release.Tag);

            if (await sources.Ensure(release, sourcesPath, cancellationToken) is false || await codebaseMemory.Index(sourcesPath, cancellationToken) is not { } project)
            {
                complete = false;
                continue;
            }

            versions.Add(new(release, sourcesPath, project));

            logger.LogInformation("The source code tools of /docs-mcp serve release {VersionName}.", release.Name);
        }

        if (versions.Served.Count is 0)
        {
            versions.ReportProblem("no release could be indexed yet; retry later");
        }

        return complete;
    }
}
