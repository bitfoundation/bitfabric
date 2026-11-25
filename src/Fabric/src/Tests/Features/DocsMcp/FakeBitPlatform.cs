using System.Collections.Concurrent;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Bit.Fabric.Server.Api.Features.DocsMcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Bit.Fabric.Tests.Features.DocsMcp;

/// <summary>
/// The far side of /docs-mcp, as it meets it over the network: every bit library's documentation server, Microsoft Learn's
/// and DeepWiki's MCP servers - real MCP servers, run in this process on a loopback port - and GitHub's releases and tags,
/// the archives of their sources and codebase-memory's downloads. Its <see cref="Handler"/> sits in the DocsMcp http client's
/// pipeline and answers every request itself, so no test reaches the internet: anything it doesn't stand in for fails.
/// </summary>
public sealed class FakeBitPlatform : IAsyncDisposable
{
    /// <summary>One per handler pipeline the http client factory builds, all answering from the same fake.</summary>
    public sealed class Handler(FakeBitPlatform platform) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => platform.Answer(request, base.SendAsync, cancellationToken);
    }

    public const string BmageAnswer = "Bmage edits images in the browser: it crops, resizes, rotates, redacts faces and exports PNG, JPEG and WebP.";

    private WebApplication? servers;

    private Uri? serversAddress;

    /// <summary>Hosts that answer 503 to everything, as a server that is down does.</summary>
    public ConcurrentDictionary<string, bool> Down { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every host asked for, in order.</summary>
    public ConcurrentQueue<string> Requested { get; } = [];

    /// <summary>codebase-memory's archives don't match the checksums published with them, as a tampered download wouldn't.</summary>
    public bool ServesTamperedCli { get; set; }

    /// <summary>
    /// The archive GitHub serves for a tag, keyed by repository and tag ("bitplatform/v-10.7.0"), each file by its path in
    /// the repository. An archive not here is a tag that doesn't exist.
    /// </summary>
    public ConcurrentDictionary<string, Dictionary<string, string>> Archives { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The version tags of bitfoundation/bitplatform: its releases' and its prereleases', which have no release. A test
    /// pushes one by adding it.
    /// </summary>
    public ConcurrentQueue<string> Tags { get; } = new(["v-10.6.0", "v-10.7.0", "v-11.0.0-pre-01", "v-11.0.0-pre-23", "v-11.0.0-pre-24"]);

    public async Task Start(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        builder.Services.AddMcpServer().WithHttpTransport(options =>
        {
            options.Stateless = true;
            options.ConfigureSessionOptions = (httpContext, mcpOptions, _) =>
            {
                mcpOptions.ToolCollection = [];
                foreach (var tool in ToolsOf((string)httpContext.GetRouteValue("host")!))
                {
                    mcpOptions.ToolCollection.Add(tool);
                }
                return Task.CompletedTask;
            };
        });

        servers = builder.Build();
        servers.MapMcp("/{host}/mcp");

        await servers.StartAsync(cancellationToken);

        serversAddress = new(servers.Urls.Single());
    }

    /// <summary>What a server of the bit platform, or one it develops against, lists: a tool or two per library, by its host.</summary>
    private static IEnumerable<McpServerTool> ToolsOf(string host)
    {
        if (host is "learn.microsoft.com")
        {
            yield return McpServerTool.Create((string query) => $"Microsoft Learn on {query}.", new() { Name = "microsoft_docs_search", Description = "Searches Microsoft Learn." });
            yield break;
        }

        if (host is "mcp.deepwiki.com")
        {
            yield return McpServerTool.Create((string repoName, string question) => $"{repoName} answers {question}.", new() { Name = "ask_wiki_question", Description = "Asks a GitHub repository." });
            yield return McpServerTool.Create((string repoName) => $"The wiki of {repoName}.", new() { Name = "read_wiki_structure", Description = "Lists a wiki's pages." });
            yield break;
        }

        var library = BitLibraries.All.Single(library => library.Address.Host == host);

        if (library.Name is "Bmage")
        {
            yield return McpServerTool.Create((string query) => $"{BmageAnswer} ({query})", new() { Name = "SearchBmage", Description = "Finds what Bmage does for a task." });
            yield return McpServerTool.Create((string recipe) => $"The {recipe} recipe of Bmage.", new() { Name = "GetBmageRecipe", Description = "Returns a worked Bmage recipe." });
            yield break;
        }

        var name = library.Name is "BlazorUI" ? "BitBlazorUI" : library.Name;
        yield return McpServerTool.Create((string query) => $"{library.Name} on {query}.", new() { Name = $"Search{name}", Description = $"Searches {library.Name}." });
    }

    private async Task<HttpResponseMessage> Answer(HttpRequestMessage request, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        Requested.Enqueue(uri.Host);

        if (Down.ContainsKey(uri.Host))
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { RequestMessage = request };

        if (uri.Host is "api.github.com" && uri.AbsolutePath is "/repos/bitfoundation/bitplatform/releases")
            return Content(request, Encoding.UTF8.GetBytes(Releases), "application/json");

        // Every tag that starts with v-, in one page and in the order GitHub lists them: by name, so v-11.0.0 comes before v-11.0.0-pre-24.
        if (uri.Host is "api.github.com" && uri.AbsolutePath is "/repos/bitfoundation/bitplatform/git/matching-refs/tags/v-")
        {
            var refs = new JsonArray([.. Tags.Order(StringComparer.Ordinal).Select(tag => (JsonNode)new JsonObject
            {
                ["ref"] = $"refs/tags/{tag}",
                ["object"] = new JsonObject { ["type"] = "commit" }
            })]);

            return Content(request, Encoding.UTF8.GetBytes(refs.ToJsonString()), "application/json");
        }

        // /bitfoundation/<repository>/tar.gz/refs/tags/<tag>, every file under one top folder, as GitHub's archives have them.
        if (uri.Host is "codeload.github.com" && uri.Segments is [_, "bitfoundation/", var repository, "tar.gz/", "refs/", "tags/", var tag])
        {
            return Archives.TryGetValue($"{repository.TrimEnd('/')}/{tag}", out var files)
                ? Content(request, TarGz(files.ToDictionary(file => $"{repository.TrimEnd('/')}-{tag}/{file.Key}", file => Encoding.UTF8.GetBytes(file.Value))), "application/x-gzip")
                : new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };
        }

        if (uri.Host is "github.com" && uri.AbsolutePath.StartsWith("/DeusData/codebase-memory-mcp/releases/download/", StringComparison.Ordinal))
            return CliDownload(request, uri.Segments.Last());

        if (uri.Host is "learn.microsoft.com" or "mcp.deepwiki.com" || uri.Host.EndsWith(".bitplatform.dev", StringComparison.Ordinal))
        {
            // To the server standing in for this host, which tells them apart by the host in the path.
            request.RequestUri = new Uri(serversAddress!, $"{uri.Host}/mcp");
            return await send(request, cancellationToken);
        }

        throw new InvalidOperationException($"The tests reach nothing on the internet, and {uri} has no stand-in.");
    }

    /// <summary>The release list as GitHub's api returns it, with a prerelease and a draft that don't count.</summary>
    private const string Releases = """
        [
            { "tag_name": "v-11.0.0-pre-01", "draft": false, "prerelease": true, "html_url": "https://github.com/bitfoundation/bitplatform/releases/tag/v-11.0.0-pre-01", "published_at": "2026-11-01T00:00:00Z", "body": "A prerelease." },
            { "tag_name": "v-10.8.0", "draft": true, "prerelease": false, "html_url": "https://github.com/bitfoundation/bitplatform/releases/tag/v-10.8.0", "published_at": "2026-11-02T00:00:00Z", "body": "A draft." },
            { "tag_name": "v-10.7.0", "draft": false, "prerelease": false, "html_url": "https://github.com/bitfoundation/bitplatform/releases/tag/v-10.7.0", "published_at": "2026-10-25T00:00:00Z", "body": "BitDataGrid exports to Excel." },
            { "tag_name": "v-10.6.0", "draft": false, "prerelease": false, "html_url": "https://github.com/bitfoundation/bitplatform/releases/tag/v-10.6.0", "published_at": "2026-09-13T00:00:00Z", "body": "The first release with MCP servers." }
        ]
        """;

    /// <summary>
    /// codebase-memory's release: checksums.txt, and an archive per platform holding a stand-in executable - a few bytes no
    /// test runs, as the install is what's under test.
    /// </summary>
    private HttpResponseMessage CliDownload(HttpRequestMessage request, string fileName)
    {
        string[] archives = [.. new[] { "linux-amd64-portable.tar.gz", "linux-arm64-portable.tar.gz", "darwin-amd64.tar.gz", "darwin-arm64.tar.gz", "windows-amd64.zip", "windows-arm64.zip" }.Select(platform => $"codebase-memory-mcp-{platform}")];

        if (fileName is "checksums.txt")
            return Content(request, Encoding.UTF8.GetBytes(string.Join('\n', archives.Select(archive => $"{Convert.ToHexStringLower(SHA256.HashData(CliArchive(archive, tampered: false)))}  {archive}"))), "text/plain");

        if (archives.Contains(fileName))
            return Content(request, CliArchive(fileName, ServesTamperedCli), "application/octet-stream");

        return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };
    }

    public static readonly byte[] CliExecutable = Encoding.UTF8.GetBytes("a stand-in for codebase-memory");

    /// <summary>
    /// Each archive is built once: an archive's headers carry the time it was written (a tar's to a fraction of a second),
    /// so the one served has to be the very one checksums.txt was computed from.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string FileName, bool Tampered), byte[]> cliArchives = new();

    private static byte[] CliArchive(string fileName, bool tampered) => cliArchives.GetOrAdd((fileName, tampered), key => BuildCliArchive(key.FileName, key.Tampered));

    private static byte[] BuildCliArchive(string fileName, bool tampered)
    {
        var executable = tampered ? Encoding.UTF8.GetBytes("something else") : CliExecutable;

        if (fileName.EndsWith(".zip", StringComparison.Ordinal))
        {
            using var zipped = new MemoryStream();
            using (var zip = new ZipArchive(zipped, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (name, content) in new[] { ("codebase-memory-mcp.exe", executable), ("LICENSE", "MIT"u8.ToArray()) })
                {
                    using var entry = zip.CreateEntry(name).Open();
                    entry.Write(content);
                }
            }
            return zipped.ToArray();
        }

        return TarGz(new() { ["codebase-memory-mcp"] = executable, ["LICENSE"] = "MIT"u8.ToArray() });
    }

    private static byte[] TarGz(Dictionary<string, byte[]> files)
    {
        using var compressed = new MemoryStream();

        using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Pax))
        {
            foreach (var (name, content) in files)
            {
                tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(content) });
            }
        }

        return compressed.ToArray();
    }

    private static HttpResponseMessage Content(HttpRequestMessage request, byte[] content, string mediaType)
        => new(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(content) { Headers = { ContentType = new(mediaType) } } };

    public async ValueTask DisposeAsync()
    {
        if (servers is not null)
        {
            await servers.DisposeAsync();
        }
    }
}
