using System.Formats.Tar;
using System.IO.Compression;

namespace Bit.Fabric.Server.Api.Features.DocsMcp.CodeIndex;

/// <summary>
/// The sources of one bit platform version, read from the archives GitHub serves for its tag: no git, no clone, only the
/// parts that get indexed written to disk. They come from bitfoundation/bitplatform, and from <see cref="TemplateMovedIn"/>
/// on the project template comes from bitfoundation/bitfabric, under the same tag.
/// </summary>
public class BitPlatformSources(IHttpClientFactory httpClientFactory, ILogger<BitPlatformSources> logger)
{
    /// <summary>
    /// Where the bit Boilerplate project template is in the index of every version, wherever its repository keeps it, so a
    /// search narrowed to it (path_filter src/Templates) reads the same for every version.
    /// </summary>
    internal const string TemplatePath = "src/Templates/Boilerplate/Bit.Boilerplate/";

    /// <summary>
    /// The template and bit CLI left bitfoundation/bitplatform for bitfoundation/bitfabric after 11.0.0-pre-23, so the first
    /// tag that has the template in bitfabric's tag and not in bitplatform's is 11.0.0-pre-24.
    /// </summary>
    internal static readonly BitPlatformTag TemplateMovedIn = BitPlatformTag.TryParse("v-11.0.0-pre-24")!;

    /// <summary>
    /// The parts of bitfoundation/bitplatform that get indexed, and so the only code the source tools reach besides the
    /// template. What is left out answers for itself elsewhere on /docs-mcp: each of <see cref="BitLibraries.All"/> has MCP
    /// tools of its own, written for the library rather than grepped out of it.
    /// </summary>
    private static readonly string[] bitPlatformPaths =
    [
        "src/ResxTranslator/",
        "src/Toolings/BlazorES2019/",
        "src/Toolings/CodeAnalyzers/",
        "src/Toolings/Minifier/",
        "src/Toolings/SourceGenerators/",
        "src/Websites/"
    ];

    /// <summary>
    /// The archives a version is read from, each with the folders taken out of it and where each lands in the index.
    /// </summary>
    internal static SourceArchive[] ArchivesOf(BitPlatformTag tag)
    {
        var bitPlatform = bitPlatformPaths.ToDictionary(path => path, path => path);

        if (tag.CompareTo(TemplateMovedIn) < 0)
        {
            bitPlatform[TemplatePath] = TemplatePath;
            return [new("bitplatform", bitPlatform)];
        }

        return [new("bitplatform", bitPlatform), new("bitfabric", new() { ["src/Boilerplate/Bit.Boilerplate/"] = TemplatePath })];
    }

    /// <summary>
    /// Writes the indexed parts of <paramref name="tag"/> into <paramref name="directory"/>, unless an earlier run finished
    /// doing so: a tag never moves, so what was extracted once stays right.
    /// </summary>
    /// <returns>Whether the directory holds them now.</returns>
    public async Task<bool> Ensure(BitPlatformTag tag, string directory, CancellationToken cancellationToken)
    {
        var marker = MarkerOf(directory);

        if (File.Exists(marker)) return true;

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            var root = Directory.CreateDirectory(directory).FullName + Path.DirectorySeparatorChar;

            foreach (var archive in ArchivesOf(tag))
            {
                var written = await Extract(archive, tag.Tag, root, cancellationToken);

                // A repository whose tag doesn't hold what it should is left for the next attempt rather than indexed without it.
                if (written is 0)
                {
                    logger.LogError("The {Repository} archive of {Tag} holds none of the indexed paths.", archive.Repository, tag.Tag);
                    return false;
                }

                logger.LogInformation("{FileCount} files of the {Repository} archive of {Tag} are extracted into {Directory}.", written, archive.Repository, tag.Tag, directory);
            }

            await File.WriteAllTextAsync(marker, tag.Tag, cancellationToken);

            return true;
        }
        catch (Exception exp) when (cancellationToken.IsCancellationRequested is false)
        {
            logger.LogError(exp, "Extracting the sources of {Tag} into {Directory} failed.", tag.Tag, directory);
            return false;
        }
    }

    /// <summary>
    /// Deletes what <see cref="Ensure"/> wrote into <paramref name="directory"/>, its marker first, so a directory deleted
    /// halfway is never taken for an extracted one.
    /// </summary>
    public void Remove(string directory)
    {
        File.Delete(MarkerOf(directory));

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Next to the directory rather than in it, so the index never reads it. Written last, so an interrupted extraction is
    /// never taken for a finished one.
    /// </summary>
    private static string MarkerOf(string directory) => $"{directory}.extracted";

    /// <returns>How many files it wrote.</returns>
    private async Task<int> Extract(SourceArchive archive, string tag, string root, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(30));

        using var response = await httpClientFactory.CreateClient(DocsMcpHttp.ClientName)
            .GetAsync($"https://codeload.github.com/bitfoundation/{archive.Repository}/tar.gz/refs/tags/{tag}", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();

        await using var gzip = new GZipStream(await response.Content.ReadAsStreamAsync(timeout.Token), CompressionMode.Decompress);
        await using var tar = new TarReader(gzip);

        var written = 0;

        while (await tar.GetNextEntryAsync(copyData: false, timeout.Token) is { } entry)
        {
            // Files only: links are left out, so nothing in the archive points outside the directory.
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || entry.DataStream is null) continue;

            // Every entry sits under one top folder, such as bitplatform-v-10.7.0/.
            var path = entry.Name[(entry.Name.IndexOf('/') + 1)..];

            if (archive.Paths.FirstOrDefault(indexed => path.StartsWith(indexed.Key, StringComparison.Ordinal)) is not { Key: not null } folder) continue;

            var target = Path.GetFullPath(Path.Combine(root, folder.Value + path[folder.Key.Length..]));

            if (target.StartsWith(root, StringComparison.Ordinal) is false) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            await using (var file = File.Create(target))
            {
                await entry.DataStream.CopyToAsync(file, timeout.Token);
            }

            written++;
        }

        return written;
    }

    /// <param name="Repository">A repository of bitfoundation on GitHub.</param>
    /// <param name="Paths">The folders taken out of it, each with the folder it lands in within the index.</param>
    internal sealed record SourceArchive(string Repository, Dictionary<string, string> Paths);
}
