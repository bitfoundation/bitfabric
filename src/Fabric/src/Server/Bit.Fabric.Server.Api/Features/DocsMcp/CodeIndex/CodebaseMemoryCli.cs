using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Bit.Fabric.Server.Api.Features.DocsMcp.CodeIndex;

/// <summary>
/// The codebase-memory command line (https://github.com/DeusData/codebase-memory-mcp), which indexes the sources of each
/// served release into one data directory. DocsMcpProxy runs the same executable as a stdio MCP server over those indexes.
/// <para>
/// It installs itself: the release <see cref="DocsMcpCodeIndexOptions.CliVersion"/> names is downloaded from GitHub for this
/// machine, checked against the checksums published with it, and kept in the data directory, so the api's container image
/// needs nothing added. On Linux that is the static "portable" build, which runs on any distribution.
/// </para>
/// </summary>
public class CodebaseMemoryCli(IHttpClientFactory httpClientFactory, ServerApiSettings settings, ILogger<CodebaseMemoryCli> logger)
{
    private DocsMcpCodeIndexOptions Options => settings.DocsMcp.CodeIndex;

    private string DataDirectory => Options.DataDirectoryPath!;

    /// <summary>The executable, once <see cref="EnsureInstalled"/> has found or installed it.</summary>
    public string? ExecutablePath { get; private set; }

    /// <summary>
    /// Points every codebase-memory process of this server at a data directory of its own rather than the default one,
    /// which a developer's editor on the same machine may hold.
    /// </summary>
    public Dictionary<string, string?> Environment => new()
    {
        ["CBM_CACHE_DIR"] = Directory.CreateDirectory(Path.Combine(DataDirectory, "index", "cache")).FullName,
        ["CBM_RUNTIME_DIR"] = Directory.CreateDirectory(Path.Combine(DataDirectory, "index", "runtime")).FullName
    };

    /// <returns>Null once the executable is there; otherwise why it isn't, worded to follow "the source code index is not available:".</returns>
    public async Task<string?> EnsureInstalled(CancellationToken cancellationToken)
    {
        if (ExecutablePath is not null) return null;

        if (string.IsNullOrWhiteSpace(Options.CliPath) is false)
        {
            if (File.Exists(Options.CliPath) is false)
                return $"the codebase-memory executable {Options.CliPath} configured for it does not exist";

            ExecutablePath = Options.CliPath;
            return null;
        }

        if (Platform() is not { } platform)
            return $"codebase-memory has no build for {RuntimeInformation.OSDescription} on {RuntimeInformation.OSArchitecture}";

        var executablePath = Path.Combine(DataDirectory, "cli", Options.CliVersion, platform.Executable);

        if (File.Exists(executablePath) is false)
        {
            try
            {
                await Install(platform, executablePath, cancellationToken);
            }
            catch (Exception exp) when (cancellationToken.IsCancellationRequested is false)
            {
                logger.LogError(exp, "Installing codebase-memory {CliVersion} into {ExecutablePath} failed.", Options.CliVersion, executablePath);
                return $"codebase-memory {Options.CliVersion} could not be installed";
            }
        }

        ExecutablePath = executablePath;
        return null;
    }

    /// <summary>
    /// Indexes one release's sources and returns the name codebase-memory stored it under, which the proxy sends as the
    /// project argument of every source tool call. Null when the indexer failed. A first index of a release takes a
    /// minute or two; a later run of the same directory reuses the persisted one.
    /// </summary>
    public async Task<string?> Index(string sourcesPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(ExecutablePath!) { ArgumentList = { "cli", "index_repository", "--repo-path", sourcesPath } };

        foreach (var (name, value) in Environment)
        {
            startInfo.Environment[name] = value;
        }

        var timeout = TimeSpan.FromMinutes(20);
        var result = await ProcessRunner.Run(startInfo, timeout, cancellationToken);

        if (result.Succeeded is false)
        {
            logger.LogError("Indexing {SourcesPath} with codebase-memory {Outcome}: {Diagnostics}",
                sourcesPath, result.TimedOut ? $"hit its {timeout.TotalMinutes:0} minute limit" : $"exited with {result.ExitCode}", result.Diagnostics);
            return null;
        }

        using var indexed = OutermostJson(result.Output);

        var projectName = indexed is not null && indexed.RootElement.TryGetProperty("project", out var project) ? project.GetString() : null;

        if (string.IsNullOrEmpty(projectName))
        {
            logger.LogError("codebase-memory indexed {SourcesPath} but named no project: {Output}", sourcesPath, result.Output);
            return null;
        }

        return projectName;
    }

    private async Task Install(CliPlatform platform, string executablePath, CancellationToken cancellationToken)
    {
        var releaseUrl = $"https://github.com/DeusData/codebase-memory-mcp/releases/download/v{Options.CliVersion}";
        var http = httpClientFactory.CreateClient(DocsMcpHttp.ClientName);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));

        // "<sha256>  <file name>" per line.
        var checksums = await http.GetStringAsync($"{releaseUrl}/checksums.txt", timeout.Token);
        var expected = checksums.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                                .Where(parts => parts.Length is 2 && parts[1].TrimStart('*') == platform.Archive)
                                .Select(parts => parts[0])
                                .SingleOrDefault()
                       ?? throw new InvalidOperationException($"checksums.txt of codebase-memory {Options.CliVersion} lists no {platform.Archive}.");

        var installDirectory = Directory.CreateDirectory(Path.GetDirectoryName(executablePath)!).FullName;

        // On disk rather than in memory: the executable alone is a few hundred MB. Hashed as it arrives.
        var archivePath = Path.Combine(installDirectory, platform.Archive);
        // Written next to its final name and moved there once complete, so an interrupted install is never taken for one.
        var partialPath = $"{executablePath}.partial";

        try
        {
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                using var response = await http.GetAsync($"{releaseUrl}/{platform.Archive}", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                response.EnsureSuccessStatusCode();

                await using var download = await response.Content.ReadAsStreamAsync(timeout.Token);
                await using var archive = File.Create(archivePath);

                var buffer = new byte[81920];
                int read;

                while ((read = await download.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await archive.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                }

                if (string.Equals(Convert.ToHexString(hash.GetHashAndReset()), expected, StringComparison.OrdinalIgnoreCase) is false)
                    throw new InvalidOperationException($"{platform.Archive} of codebase-memory {Options.CliVersion} does not match its published checksum.");
            }

            await using (var archive = File.OpenRead(archivePath))
            await using (var executable = File.Create(partialPath))
            {
                await ExtractExecutable(archive, platform, executable, timeout.Token);
            }
        }
        finally
        {
            File.Delete(archivePath);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(partialPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                                              | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        File.Move(partialPath, executablePath, overwrite: true);

        logger.LogInformation("codebase-memory {CliVersion} is installed at {ExecutablePath}.", Options.CliVersion, executablePath);
    }

    /// <summary>Copies the one entry the archive is installed for; its license and installer scripts are left in it.</summary>
    private static async Task ExtractExecutable(Stream archive, CliPlatform platform, Stream destination, CancellationToken cancellationToken)
    {
        if (platform.Archive.EndsWith(".zip", StringComparison.Ordinal))
        {
            using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
            var entry = zip.Entries.SingleOrDefault(entry => entry.FullName == platform.Executable)
                        ?? throw new InvalidOperationException($"{platform.Archive} holds no {platform.Executable}.");

            await using var source = await entry.OpenAsync(cancellationToken);
            await source.CopyToAsync(destination, cancellationToken);
            return;
        }

        await using var gzip = new GZipStream(archive, CompressionMode.Decompress, leaveOpen: true);
        await using var tar = new TarReader(gzip);

        while (await tar.GetNextEntryAsync(copyData: false, cancellationToken) is { } entry)
        {
            if (entry.Name.TrimStart('.', '/') != platform.Executable || entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || entry.DataStream is null)
                continue;

            await entry.DataStream.CopyToAsync(destination, cancellationToken);
            return;
        }

        throw new InvalidOperationException($"{platform.Archive} holds no {platform.Executable}.");
    }

    /// <summary>The archive codebase-memory publishes for this machine, as its own installer picks it.</summary>
    private static CliPlatform? Platform()
    {
        var os = OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsMacOS() ? "darwin" : OperatingSystem.IsWindows() ? "windows" : null;
        var architecture = RuntimeInformation.OSArchitecture switch { Architecture.X64 => "amd64", Architecture.Arm64 => "arm64", _ => null };

        if (os is null || architecture is null) return null;

        return os switch
        {
            // The standard Linux build links glibc 2.38 or later; the portable one is static.
            "linux" => new($"codebase-memory-mcp-linux-{architecture}-portable.tar.gz", "codebase-memory-mcp"),
            "windows" => new($"codebase-memory-mcp-windows-{architecture}.zip", "codebase-memory-mcp.exe"),
            _ => new($"codebase-memory-mcp-{os}-{architecture}.tar.gz", "codebase-memory-mcp")
        };
    }

    /// <summary>The command line prints log lines around its json result, so the outermost object is taken out.</summary>
    private static JsonDocument? OutermostJson(string output)
    {
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');

        if (start < 0 || end < start) return null;

        try
        {
            return JsonDocument.Parse(output[start..(end + 1)]);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record CliPlatform(string Archive, string Executable);
}
