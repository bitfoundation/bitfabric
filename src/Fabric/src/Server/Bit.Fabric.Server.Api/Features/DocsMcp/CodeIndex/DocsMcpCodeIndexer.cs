namespace Bit.Fabric.Server.Api.Features.DocsMcp.CodeIndex;

/// <summary>
/// Keeps an index of the newest bit platform code: the newest tag of bitfoundation/bitplatform, a prerelease included, which
/// every source code tool call of /docs-mcp reads. A newer tag replaces it once it is indexed, and the sources and index of
/// the one it replaces are deleted. Runs in the background: startup never waits for it, a tag that can't be prepared leaves
/// the one served before answering and is tried again later, and without
/// <see cref="DocsMcpCodeIndexOptions.DataDirectoryPath"/> it does nothing at all.
/// <para>
/// The data directory holds <c>cli/&lt;version&gt;/</c> (the installed command line), <c>sources/&lt;tag&gt;/</c> (the
/// served tag's indexed sources) and <c>index/</c> (codebase-memory's own data). In the published Docker Compose project it
/// is the api's <c>docs-mcp</c> volume, so a deployment keeps them (See DockerComposePublishingExtensions).
/// </para>
/// </summary>
public class DocsMcpCodeIndexer(ServerApiSettings settings,
    BitPlatformTags bitPlatformTags,
    BitPlatformSources sources,
    CodebaseMemoryCli codebaseMemory,
    DocsMcpVersions versions,
    TimeProvider timeProvider,
    ILogger<DocsMcpCodeIndexer> logger) : BackgroundService
{
    /// <summary>How soon a tag that could not be prepared, or a command line that could not be installed, is tried again.</summary>
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
    /// would end it, and no newer tag would be served until the app restarts.
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

    /// <returns>Whether the newest tag is served now.</returns>
    private async Task<bool> Refresh(CancellationToken cancellationToken)
    {
        if (await codebaseMemory.EnsureInstalled(cancellationToken) is { } problem)
        {
            versions.ReportProblem(problem);
            return false;
        }

        if (await bitPlatformTags.GetNewest(cancellationToken) is not { } newest)
        {
            versions.ReportProblem("the bit platform tags could not be read from GitHub; retry in a few minutes");
            return false;
        }

        if (versions.Served?.Name != newest.Name)
        {
            var sourcesPath = Path.Combine(SourcesDirectory, newest.Tag);

            if (await sources.Ensure(newest, sourcesPath, cancellationToken) is false || await codebaseMemory.Index(sourcesPath, cancellationToken) is not { } project)
            {
                versions.ReportProblem($"{newest.Name}, the newest bit platform code, could not be indexed yet; retry later");
                return false;
            }

            var replaced = versions.Served;

            versions.Serve(new(newest, sourcesPath, project));

            logger.LogInformation("The source code tools of /docs-mcp read {VersionName} now, in place of {ReplacedVersionName}.", newest.Name, replaced?.Name ?? "nothing");
        }

        RemoveSourcesOtherThan(versions.Served!);
        await RemoveIndexesOtherThan(versions.Served!, cancellationToken);

        return true;
    }

    private string SourcesDirectory => Path.Combine(Options.DataDirectoryPath!, "sources");

    /// <summary>
    /// Deletes the sources of every tag but the served one's: the one it replaced, and any an earlier run of the app left
    /// behind. Every pass, so one that couldn't be deleted is tried again on the next.
    /// </summary>
    private void RemoveSourcesOtherThan(DocsMcpVersion served)
    {
        if (Directory.Exists(SourcesDirectory) is false) return;

        foreach (var directory in Directory.EnumerateDirectories(SourcesDirectory))
        {
            if (Path.GetFileName(directory) == served.Tag.Tag) continue;

            try
            {
                sources.Remove(directory);

                logger.LogInformation("The sources of {Tag}, which the source code tools of /docs-mcp no longer read, are deleted.", Path.GetFileName(directory));
            }
            catch (Exception exp) when (exp is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exp, "Deleting {Directory} failed, retrying on the next pass.", directory);
            }
        }
    }

    /// <summary>
    /// Deletes codebase-memory's index of every project but the served one: the tag it replaced, and any an earlier run of
    /// the app left behind, such as the releases it read before it served the newest code only. The data directory is this
    /// server's alone, so every other project in it is one of those. Every pass, so one that couldn't be deleted is tried
    /// again on the next.
    /// </summary>
    private async Task RemoveIndexesOtherThan(DocsMcpVersion served, CancellationToken cancellationToken)
    {
        if (await codebaseMemory.ListProjects(cancellationToken) is not { } projects) return;

        foreach (var project in projects.Where(project => project != served.CodebaseMemoryProject))
        {
            if (await codebaseMemory.DeleteProject(project, cancellationToken))
            {
                logger.LogInformation("The index of {Project}, which the source code tools of /docs-mcp no longer read, is deleted.", project);
            }
        }
    }
}
