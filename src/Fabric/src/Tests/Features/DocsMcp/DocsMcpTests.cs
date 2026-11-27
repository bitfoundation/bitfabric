using System.Collections.Concurrent;
using Bit.Fabric.Server.Api.Features.DocsMcp;
using Bit.Fabric.Server.Api.Features.DocsMcp.CodeIndex;
using Bit.Fabric.Server.Api.Infrastructure.SignalR;
using FluentEmail.Core;
using FluentEmail.Core.Interfaces;
using FluentEmail.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Bit.Fabric.Tests.Features.DocsMcp;

/// <summary>
/// The bit platform's documentation MCP server at /docs-mcp, called over HTTP the way a coding agent calls it, with every
/// server it reaches stood in by <see cref="FakeBitPlatform"/>; and the same tools as the in-app assistant calls them.
/// </summary>
[TestClass, TestCategory("IntegrationTest")]
public partial class DocsMcpTests
{
    private const string TeamAddress = "team@bitplatform.test";

    [TestMethod]
    public async Task TheTools_Should_BeListed_EveryLibrarysOnRequest()
    {
        await using var platform = new FakeBitPlatform();
        await using var server = await StartServer(platform);
        await using var mcp = await Connect(server);

        Assert.AreEqual("bitfabric-docs", mcp.ServerInfo!.Name);

        var tools = (await mcp.ListToolsAsync(cancellationToken: TestContext.CancellationToken)).ToDictionary(tool => tool.Name);

        string[] listed = ["SendBitPlatformFeedback", "GetBitPlatformReleases", "SearchBitBlazorUI", "SearchButil", "FindBitPlatformSymbols", "SearchBitPlatformCode",
                           "GetBitPlatformSymbolSource", "microsoft_docs_search", "AskGitHubRepository"];
        Assert.IsEmpty(listed.Except(tools.Keys), $"Listed: {string.Join(", ", tools.Keys)}");
        Assert.DoesNotContain("SearchBmage", tools.Keys, "Every other library's tools are listed on request.");
        Assert.DoesNotContain("read_wiki_structure", tools.Keys, "Only DeepWiki's question tool is exposed, under a name of its own.");
        Assert.DoesNotContain(name => name is "GetProjects" or "GetAppPages", tools.Keys, "The chatbot's tools stay on /mcp.");

        Assert.IsFalse(tools["SendBitPlatformFeedback"].ProtocolTool.Annotations!.ReadOnlyHint, "Feedback reaches people.");
        Assert.IsTrue(tools.Values.Where(tool => tool.Name is not "SendBitPlatformFeedback").All(tool => tool.ProtocolTool.Annotations?.ReadOnlyHint is true), "Everything else only reads.");

        await using var everyLibrary = await Connect(server, "libraries=all");

        var all = (await everyLibrary.ListToolsAsync(cancellationToken: TestContext.CancellationToken)).Select(tool => tool.Name).ToArray();
        Assert.Contains("SearchBmage", all);
        Assert.Contains("SearchBheadless", all);
        Assert.Contains("SearchBitBlazorUI", all);
    }

    [TestMethod]
    public async Task EveryTool_Should_Answer()
    {
        await using var platform = new FakeBitPlatform();
        var sender = new CapturingSender();
        await using var server = await StartServer(platform, sender);
        await using var mcp = await Connect(server, "libraries=all&project=a-project");

        Assert.AreEqual("BlazorUI on BitDataGrid.", await Call(mcp, "SearchBitBlazorUI", new() { ["query"] = "BitDataGrid" }));
        Assert.StartsWith(FakeBitPlatform.BmageAnswer, await Call(mcp, "SearchBmage", new() { ["query"] = "crop" }));
        Assert.AreEqual("The resize recipe of Bmage.", await Call(mcp, "GetBmageRecipe", new() { ["recipe"] = "resize" }));
        Assert.AreEqual("Microsoft Learn on Blazor.", await Call(mcp, "microsoft_docs_search", new() { ["query"] = "Blazor" }));
        Assert.AreEqual("riok/mapperly answers How?.", await Call(mcp, "AskGitHubRepository", new() { ["repoName"] = "riok/mapperly", ["question"] = "How?" }));

        var releases = await Call(mcp, "GetBitPlatformReleases", new() { ["version"] = "10.6.0" });
        Assert.StartsWith("10.6.0 is behind: 4 newer versions, the newest being 11.0.0-pre-24", releases);
        Assert.Contains("BitDataGrid exports to Excel.", releases);

        Assert.AreEqual("The report reached the bit platform team. Tell the user it was sent.",
            await Call(mcp, "SendBitPlatformFeedback", new() { ["title"] = "BitDropdown loses its selection", ["report"] = "Scroll a virtualized list, then pick." }));

        var email = sender.Sent.Single();
        Assert.AreEqual(TeamAddress, email.Data.ToAddresses.Single().EmailAddress);
        Assert.Contains("BitDropdown loses its selection", email.Data.Subject);
        Assert.Contains("Project: a-project", email.Data.Body, "Stamped with the project the agent connected for.");
        Assert.Contains("Scroll a virtualized list, then pick.", email.Data.Body);
        Assert.IsFalse(email.Data.IsHtml, "Nothing an agent writes renders.");

        var refused = await mcp.CallToolAsync("SendBitPlatformFeedback", new Dictionary<string, object?> { ["title"] = "No report" }, cancellationToken: TestContext.CancellationToken);
        Assert.IsTrue(refused.IsError);
        Assert.HasCount(1, sender.Sent, "A report without its text isn't sent.");
    }

    /// <summary>
    /// Every tag of bitplatform newer than the project's version counts, prereleases included, newest first. A version's
    /// notes are those of its GitHub release in bitfabric (bit Boilerplate and bit CLI, from 11.0.0-pre-24 on) or bitplatform
    /// (a stable version), and a version with neither is listed by its tag and the date of its commit.
    /// </summary>
    [TestMethod]
    public async Task TheReleasesTool_Should_CountEveryNewerVersion_PrereleasesIncluded()
    {
        await using var platform = new FakeBitPlatform();
        await using var server = await StartServer(platform);
        await using var mcp = await Connect(server);

        Assert.AreEqual("11.0.0-pre-24 is up to date: the newest version is 11.0.0-pre-24, published 2026-11-24, https://github.com/bitfoundation/bitfabric/releases/tag/v-11.0.0-pre-24",
            await Call(mcp, "GetBitPlatformReleases", new() { ["version"] = "11.0.0-pre-24" }));

        var withoutVersion = await mcp.CallToolAsync("GetBitPlatformReleases", new Dictionary<string, object?>(), cancellationToken: TestContext.CancellationToken);
        Assert.IsTrue(withoutVersion.IsError);
        Assert.StartsWith("Pass version", Text(withoutVersion), "The agent passes the version its project is on; the address names none.");

        var behind = await Call(mcp, "GetBitPlatformReleases", new() { ["version"] = "11.0.0-pre-22" });
        Assert.StartsWith("11.0.0-pre-22 is behind: 2 newer versions, the newest being 11.0.0-pre-24, published 2026-11-24.", behind);
        Assert.Contains("# 11.0.0-pre-24 of bit Boilerplate and bit CLI, published 2026-11-24, https://github.com/bitfoundation/bitfabric/releases/tag/v-11.0.0-pre-24", behind);
        Assert.Contains("bit Boilerplate and bit CLI move to bitfabric.", behind);
        Assert.Contains("11.0.0-pre-23, dated 2026-11-19: no release notes, https://github.com/bitfoundation/bitplatform/releases/tag/v-11.0.0-pre-23", behind);
        Assert.IsLessThan(behind.IndexOf("11.0.0-pre-23, dated", StringComparison.Ordinal), behind.IndexOf("# 11.0.0-pre-24", StringComparison.Ordinal), "Newest first.");

        // A stable version is behind every prerelease since, and may be passed as its tag.
        var stable = await Call(mcp, "GetBitPlatformReleases", new() { ["version"] = "v-10.7.0" });
        Assert.StartsWith("v-10.7.0 is behind: 3 newer versions, the newest being 11.0.0-pre-24", stable);
        Assert.Contains("11.0.0-pre-01, dated 2026-10-27: no release notes", stable);
        Assert.DoesNotContain("BitDataGrid exports to Excel.", stable, "Only the versions newer than the project's.");

        var older = await Call(mcp, "GetBitPlatformReleases", new() { ["version"] = "10.6.0" });
        Assert.StartsWith("10.6.0 is behind: 4 newer versions", older);
        Assert.Contains("# 10.7.0, published 2026-10-25, https://github.com/bitfoundation/bitplatform/releases/tag/v-10.7.0", older);
        Assert.DoesNotContain("A draft.", older);

        // A version no tag names still gets an answer, with or without its prerelease part.
        Assert.StartsWith("10.6.5 is behind: 4 newer versions", await Call(mcp, "GetBitPlatformReleases", new() { ["version"] = "10.6.5" }));
        Assert.StartsWith("11.0.0-pre-99 is up to date: no version is newer", await Call(mcp, "GetBitPlatformReleases", new() { ["version"] = "11.0.0-pre-99" }));
        Assert.StartsWith("11.0.0 is up to date: no version is newer", await Call(mcp, "GetBitPlatformReleases", new() { ["version"] = "11.0.0" }), "A stable version comes after every prerelease of it.");

        var refused = await mcp.CallToolAsync("GetBitPlatformReleases", new Dictionary<string, object?> { ["version"] = "11.0" }, cancellationToken: TestContext.CancellationToken);
        Assert.IsTrue(refused.IsError);
        Assert.StartsWith("11.0 is not a bit platform version", Text(refused));
    }

    /// <summary>A stable version has a release in both repositories: bitplatform's notes come first, then bitfabric's.</summary>
    [TestMethod]
    public async Task TheReleasesTool_Should_ShowBothRepositoriesNotes_OfAStableVersion()
    {
        await using var platform = new FakeBitPlatform();
        platform.Tags.Enqueue("v-11.0.0");
        platform.Releases.Enqueue(new("bitplatform", "v-11.0.0", "2026-12-01T00:00:00Z", "Every library on .NET 11."));
        platform.Releases.Enqueue(new("bitfabric", "v-11.0.0", "2026-12-01T00:00:00Z", "The template on .NET 11."));

        await using var server = await StartServer(platform);
        await using var mcp = await Connect(server);

        var answer = await Call(mcp, "GetBitPlatformReleases", new() { ["version"] = "11.0.0-pre-24" });
        Assert.StartsWith("11.0.0-pre-24 is behind: 1 newer version, the newest being 11.0.0, published 2026-12-01.", answer, "A stable version comes after every prerelease of it.");
        Assert.Contains("# 11.0.0, published 2026-12-01, https://github.com/bitfoundation/bitplatform/releases/tag/v-11.0.0", answer);
        Assert.Contains("# 11.0.0 of bit Boilerplate and bit CLI, published 2026-12-01, https://github.com/bitfoundation/bitfabric/releases/tag/v-11.0.0", answer);

        var bitPlatformNotes = answer.IndexOf("Every library on .NET 11.", StringComparison.Ordinal);
        Assert.IsGreaterThan(0, bitPlatformNotes);
        Assert.IsGreaterThan(bitPlatformNotes, answer.IndexOf("The template on .NET 11.", StringComparison.Ordinal), "bitplatform's notes, then bitfabric's.");
    }

    /// <summary>
    /// The source code tools read the newest tag of bitplatform, a prerelease included, with the template from bitfabric's
    /// tag of the same name. A newer tag replaces it once it is indexed, deleting the sources of the one it replaced; one
    /// that can't be prepared yet leaves the one served answering.
    /// </summary>
    [TestMethod]
    public async Task TheSourceCodeTools_Should_ReadTheNewestTag()
    {
        var dataDirectory = Directory.CreateTempSubdirectory("docs-mcp-");

        try
        {
            await using var platform = new FakeBitPlatform();
            AddArchives(platform, "v-11.0.0-pre-24");

            // GitHub's tags are kept for a quarter of an hour, which this clock moves past.
            var clock = new MovableClock();

            // Indexing is codebase-memory's own: what is under test is which tag gets indexed and served.
            var codebaseMemory = A.Fake<CodebaseMemoryCli>(options => options.WithArgumentsForConstructor([A.Fake<IHttpClientFactory>(), new Server.Api.ServerApiSettings(), NullLogger<CodebaseMemoryCli>.Instance]));
            A.CallTo(() => codebaseMemory.EnsureInstalled(A<CancellationToken>._)).Returns((string?)null);
            // It holds what it indexed, and the indexes of releases an earlier run of the server read.
            var indexed = new ConcurrentDictionary<string, bool>([new("project-v-10.7.0", true), new("project-v-10.6.2", true)]);
            A.CallTo(() => codebaseMemory.Index(A<string>._, A<CancellationToken>._)).ReturnsLazily((string sourcesPath, CancellationToken _) =>
            {
                var project = $"project-{Path.GetFileName(sourcesPath)}";
                indexed[project] = true;
                return (string?)project;
            });
            A.CallTo(() => codebaseMemory.ListProjects(A<CancellationToken>._)).ReturnsLazily(() => (string[]?)[.. indexed.Keys]);
            A.CallTo(() => codebaseMemory.DeleteProject(A<string>._, A<CancellationToken>._)).ReturnsLazily((string project, CancellationToken cancellationToken) => indexed.TryRemove(project, out _));

            await using var server = await StartServer(platform, configureServices: services =>
            {
                services.Replace(ServiceDescriptor.Singleton(codebaseMemory));
                services.Replace(ServiceDescriptor.Singleton(serviceProvider => new BitPlatformTags(serviceProvider.GetRequiredService<IHttpClientFactory>(), clock, NullLogger<BitPlatformTags>.Instance)));
            }, configure: configuration =>
            {
                configuration["DocsMcp:CodeIndex:DataDirectoryPath"] = dataDirectory.FullName;
                configuration["DocsMcp:CodeIndex:RefreshInterval"] = "00:00:00.100";
            });

            var versions = server.ApiApp.Services.GetRequiredService<DocsMcpVersions>();
            var sourcesDirectory = Path.Combine(dataDirectory.FullName, "sources");
            var preSources = Path.Combine(sourcesDirectory, "v-11.0.0-pre-24");

            await Until(() => versions.Served is not null);
            Assert.AreEqual("11.0.0-pre-24", versions.Served!.Name, "The newest tag is a prerelease, which has no GitHub release.");
            Assert.AreEqual("project-v-11.0.0-pre-24", versions.Served.CodebaseMemoryProject);
            await Until(() => indexed.Count is 1);
            Assert.AreSequenceEqual(new[] { "project-v-11.0.0-pre-24" }, indexed.Keys.ToArray(), "The indexes of the releases read before are deleted.");
            Assert.AreEqual("// The template of 11.0.0-pre-24", File.ReadAllText(Path.Combine(preSources, "src/Templates/Boilerplate/Bit.Boilerplate/src/Program.cs")));

            await using (var mcp = await Connect(server))
            {
                Assert.Contains("Its source code tools read 11.0.0-pre-24, the newest bit platform tag indexed so far.", mcp.ServerInstructions!);
            }

            // 11.0.0 is tagged in both repositories.
            AddArchives(platform, "v-11.0.0");
            platform.Tags.Enqueue("v-11.0.0");
            clock.Ahead = TimeSpan.FromMinutes(20);

            await Until(() => versions.Served?.Name is "11.0.0" && Directory.Exists(preSources) is false && indexed.ContainsKey("project-v-11.0.0-pre-24") is false);
            Assert.IsFalse(File.Exists($"{preSources}.extracted"), "Nothing of the tag it replaced is left behind.");
            Assert.AreSequenceEqual(new[] { "project-v-11.0.0" }, indexed.Keys.ToArray(), "Nor its index.");
            Assert.AreSequenceEqual(new[] { "v-11.0.0" }, Directory.EnumerateDirectories(sourcesDirectory).Select(Path.GetFileName).ToArray());

            await using (var mcp = await Connect(server))
            {
                Assert.Contains("Its source code tools read 11.0.0, the newest bit platform tag indexed so far.", mcp.ServerInstructions!);
            }

            using (var served = JsonDocument.Parse(await server.CreateRawHttpClient().GetStringAsync(new Uri(server.ApiAppAddress, "docs-mcp/versions"), TestContext.CancellationToken)))
            {
                Assert.AreEqual("11.0.0", served.RootElement.GetProperty("latest").GetString());
                Assert.AreEqual(1, served.RootElement.GetProperty("versions").GetArrayLength());
            }

            // Tagged in bitplatform but not yet in bitfabric: tried, and left for a later attempt.
            platform.Archives["bitplatform/v-11.0.1-pre-01"] = platform.Archives["bitplatform/v-11.0.0"];
            var archivesRead = platform.Requested.Count(host => host is "codeload.github.com");
            platform.Tags.Enqueue("v-11.0.1-pre-01");
            clock.Ahead = TimeSpan.FromMinutes(40);

            await Until(() => platform.Requested.Count(host => host is "codeload.github.com") >= archivesRead + 2);
            Assert.AreEqual("11.0.0", versions.Served!.Name);
            Assert.IsTrue(File.Exists(Path.Combine(sourcesDirectory, "v-11.0.0.extracted")));
        }
        finally
        {
            dataDirectory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// A tag's sources come from GitHub's archives of it: the project template from bitfabric's from 11.0.0-pre-24 on and
    /// from bitplatform's before, at the same path in the index either way - the template's scaffold-entity skill narrows
    /// its searches to src/Templates - and nothing else of either repository, nor a file that would land outside the
    /// tag's folder.
    /// </summary>
    [TestMethod]
    public async Task ATag_Should_BeExtracted_WithTheTemplateWhereverItLives()
    {
        await using var platform = new FakeBitPlatform();
        platform.Archives["bitplatform/v-11.0.0-pre-23"] = new()
        {
            ["src/Websites/Platform/Startup.cs"] = "class Startup { }",
            ["src/Templates/Boilerplate/Bit.Boilerplate/src/Program.cs"] = "// The template of 11.0.0-pre-23",
            ["src/BlazorUI/Bit.BlazorUI/BitButton.cs"] = "class BitButton { }",
            ["src/Websites/../../../outside.cs"] = "class Outside { }"
        };
        platform.Archives["bitplatform/v-11.0.0-pre-24"] = new()
        {
            ["src/Websites/Platform/Startup.cs"] = "class Startup { }",
            ["src/BlazorUI/Bit.BlazorUI/BitButton.cs"] = "class BitButton { }"
        };
        platform.Archives["bitfabric/v-11.0.0-pre-24"] = new()
        {
            ["src/Boilerplate/Bit.Boilerplate/src/Program.cs"] = "// The template of 11.0.0-pre-24",
            ["src/Cli/Program.cs"] = "// bit CLI",
            ["src/Fabric/src/Server/Program.cs"] = "// bit Fabric"
        };
        platform.Archives["bitplatform/v-11.0.0"] = platform.Archives["bitplatform/v-11.0.0-pre-24"];

        var sources = new BitPlatformSources(HttpClientFactoryOf(platform), NullLogger<BitPlatformSources>.Instance);
        var directory = Directory.CreateTempSubdirectory("docs-mcp-sources-");

        try
        {
            var older = Path.Combine(directory.FullName, "v-11.0.0-pre-23");
            Assert.IsTrue(await sources.Ensure(Tag("v-11.0.0-pre-23"), older, TestContext.CancellationToken));
            Assert.AreSequenceEqual(new[] { "src/Templates/Boilerplate/Bit.Boilerplate/src/Program.cs", "src/Websites/Platform/Startup.cs" }, FilesUnder(older));
            Assert.IsFalse(File.Exists(Path.Combine(directory.FullName, "outside.cs")));

            var newer = Path.Combine(directory.FullName, "v-11.0.0-pre-24");
            Assert.IsTrue(await sources.Ensure(Tag("v-11.0.0-pre-24"), newer, TestContext.CancellationToken));
            Assert.AreSequenceEqual(new[] { "src/Templates/Boilerplate/Bit.Boilerplate/src/Program.cs", "src/Websites/Platform/Startup.cs" }, FilesUnder(newer));
            Assert.AreEqual("// The template of 11.0.0-pre-24", File.ReadAllText(Path.Combine(newer, "src/Templates/Boilerplate/Bit.Boilerplate/src/Program.cs")));

            // Without its bitfabric tag yet, a tag is left for a later attempt rather than indexed without the template.
            Assert.IsFalse(await sources.Ensure(Tag("v-11.0.0"), Path.Combine(directory.FullName, "v-11.0.0"), TestContext.CancellationToken));
            Assert.IsFalse(File.Exists(Path.Combine(directory.FullName, "v-11.0.0.extracted")));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// The command line installs itself from codebase-memory's GitHub release, the archive for this machine checked against
    /// the checksums published with it. One that doesn't match leaves nothing behind to run.
    /// </summary>
    [TestMethod]
    public async Task TheCommandLine_Should_BeInstalled_OnlyAsPublished()
    {
        await using var platform = new FakeBitPlatform { ServesTamperedCli = true };
        var directory = Directory.CreateTempSubdirectory("docs-mcp-cli-");

        try
        {
            Server.Api.ServerApiSettings settings = new() { DocsMcp = new() { CodeIndex = new() { DataDirectoryPath = directory.FullName } } };

            var tampered = new CodebaseMemoryCli(HttpClientFactoryOf(platform), settings, NullLogger<CodebaseMemoryCli>.Instance);
            Assert.AreEqual("codebase-memory 0.12.1 could not be installed", await tampered.EnsureInstalled(TestContext.CancellationToken));
            Assert.IsNull(tampered.ExecutablePath);
            Assert.IsEmpty(Directory.EnumerateFiles(directory.FullName, "*", SearchOption.AllDirectories), "Nothing unchecked is kept, the download included.");

            platform.ServesTamperedCli = false;

            var installed = new CodebaseMemoryCli(HttpClientFactoryOf(platform), settings, NullLogger<CodebaseMemoryCli>.Instance);
            Assert.IsNull(await installed.EnsureInstalled(TestContext.CancellationToken));
            Assert.AreSequenceEqual(FakeBitPlatform.CliExecutable, File.ReadAllBytes(installed.ExecutablePath!));
            Assert.AreEqual(Path.Combine(directory.FullName, "cli", "0.12.1"), Path.GetDirectoryName(installed.ExecutablePath));

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                Assert.IsTrue(File.GetUnixFileMode(installed.ExecutablePath!).HasFlag(UnixFileMode.UserExecute), "It is installed to run.");
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static BitPlatformTag Tag(string tag) => BitPlatformTag.TryParse(tag)!;

    /// <summary>GitHub's archives of <paramref name="tag"/> in both repositories, the template in bitfabric's.</summary>
    private static void AddArchives(FakeBitPlatform platform, string tag)
    {
        platform.Archives[$"bitplatform/{tag}"] = new() { ["src/Websites/Platform/Startup.cs"] = "class Startup { }" };
        platform.Archives[$"bitfabric/{tag}"] = new() { ["src/Boilerplate/Bit.Boilerplate/src/Program.cs"] = $"// The template of {Tag(tag).Name}" };
    }

    /// <summary>Waits for what the index does in the background, half a minute at most.</summary>
    private async Task Until(Func<bool> condition)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));

        while (condition() is false)
        {
            await Task.Delay(100, deadline.Token);
        }
    }

    /// <summary>The system's clock, read <see cref="Ahead"/> of it. Its timers keep real time.</summary>
    private sealed class MovableClock : TimeProvider
    {
        public TimeSpan Ahead { get; set; }

        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Ahead;
    }

    private static string[] FilesUnder(string directory)
        => [.. Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, '/')).Order(StringComparer.Ordinal)];

    /// <summary>For the parts that need no server: every client they create answers from <paramref name="platform"/>.</summary>
    private static IHttpClientFactory HttpClientFactoryOf(FakeBitPlatform platform)
    {
        var factory = A.Fake<IHttpClientFactory>();
        A.CallTo(() => factory.CreateClient(A<string>._)).ReturnsLazily(() => new HttpClient(new FakeBitPlatform.Handler(platform) { InnerHandler = new SocketsHttpHandler() }));
        return factory;
    }

    [TestMethod]
    public async Task AServerThatDoesNotAnswer_Should_LeaveTheOthersAnswering()
    {
        await using var platform = new FakeBitPlatform();
        platform.Down["butil.bitplatform.dev"] = true;
        platform.Down["mcp.deepwiki.com"] = true;

        await using var server = await StartServer(platform);
        await using var mcp = await Connect(server);

        var tools = (await mcp.ListToolsAsync(cancellationToken: TestContext.CancellationToken)).Select(tool => tool.Name).ToArray();
        Assert.DoesNotContain("SearchButil", tools);
        Assert.DoesNotContain("AskGitHubRepository", tools);
        Assert.Contains("SearchBitBlazorUI", tools, "One server down leaves the others' tools listed.");

        // A server that goes down after it was listed: the call says so rather than failing the session.
        platform.Down["blazorui.bitplatform.dev"] = true;

        var result = await mcp.CallToolAsync("SearchBitBlazorUI", new Dictionary<string, object?> { ["query"] = "BitButton" }, cancellationToken: TestContext.CancellationToken);
        Assert.IsTrue(result.IsError);
        Assert.Contains("did not answer", Text(result));

        // Back up, the next call answers.
        platform.Down.TryRemove("blazorui.bitplatform.dev", out _);
        Assert.AreEqual("BlazorUI on BitButton.", await Call(mcp, "SearchBitBlazorUI", new() { ["query"] = "BitButton" }));
    }

    [TestMethod]
    public async Task TheSourceCodeTools_Should_SayWhy_WithoutAnIndex()
    {
        await using var platform = new FakeBitPlatform();
        await using var server = await StartServer(platform);
        await using var mcp = await Connect(server);

        Assert.Contains("source code tools don't answer for now: this server has no source code index configured", mcp.ServerInstructions!);

        var result = await mcp.CallToolAsync("FindBitPlatformSymbols", new Dictionary<string, object?> { ["query"] = "BitButton" }, cancellationToken: TestContext.CancellationToken);
        Assert.IsTrue(result.IsError);
        Assert.Contains("The source code index is not available: this server has no source code index configured", Text(result));

        using var versions = JsonDocument.Parse(await server.CreateRawHttpClient().GetStringAsync(new Uri(server.ApiAppAddress, "docs-mcp/versions"), TestContext.CancellationToken));
        Assert.AreEqual(JsonValueKind.Null, versions.RootElement.GetProperty("latest").ValueKind);
        Assert.AreEqual(0, versions.RootElement.GetProperty("versions").GetArrayLength());
    }

    /// <summary>A release whose command line can't be installed - here a download that doesn't match its checksum - is said so.</summary>
    [TestMethod]
    public async Task TheSourceCodeTools_Should_SayWhy_WhenTheirCommandLineCantBeInstalled()
    {
        var dataDirectory = Directory.CreateTempSubdirectory("docs-mcp-");

        try
        {
            await using var platform = new FakeBitPlatform { ServesTamperedCli = true };
            await using var server = await StartServer(platform, configure: configuration => configuration["DocsMcp:CodeIndex:DataDirectoryPath"] = dataDirectory.FullName);

            var versions = server.ApiApp.Services.GetRequiredService<DocsMcpVersions>();

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));

            while (versions.Unavailable?.Contains("could not be installed") is not true)
            {
                await Task.Delay(100, deadline.Token);
            }

            await using var mcp = await Connect(server);

            var result = await mcp.CallToolAsync("SearchBitPlatformCode", new Dictionary<string, object?> { ["pattern"] = "BitButton" }, cancellationToken: TestContext.CancellationToken);
            Assert.IsTrue(result.IsError);
            Assert.Contains("codebase-memory 0.12.1 could not be installed", Text(result));

            Assert.AreEqual("BlazorUI on BitButton.", await Call(mcp, "SearchBitBlazorUI", new() { ["query"] = "BitButton" }), "Every other tool answers.");
            Assert.IsEmpty(Directory.EnumerateFiles(dataDirectory.FullName, "codebase-memory-mcp*", SearchOption.AllDirectories), "Nothing unchecked is left to run.");
        }
        finally
        {
            dataDirectory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task TheProject_Should_ReachTheAccessCheck()
    {
        await using var platform = new FakeBitPlatform();
        var access = new RecordingAccess();
        await using var server = await StartServer(platform, configureServices: services => services.Replace(ServiceDescriptor.Scoped<IDocsMcpAccess>(_ => access)));

        await using (var licensed = await Connect(server, "project=licensed-project"))
        {
            Assert.AreEqual("BlazorUI on BitButton.", await Call(licensed, "SearchBitBlazorUI", new() { ["query"] = "BitButton" }));
        }

        Assert.Contains("licensed-project", access.Asked);

        await using var unlicensed = await Connect(server, $"project={RecordingAccess.UnlicensedProject}");

        Assert.EndsWith(RecordingAccess.Refusal, unlicensed.ServerInstructions!, "The agent learns why as it connects.");

        var refused = await unlicensed.CallToolAsync("SearchBitBlazorUI", new Dictionary<string, object?> { ["query"] = "BitButton" }, cancellationToken: TestContext.CancellationToken);
        Assert.IsTrue(refused.IsError);
        Assert.AreEqual(RecordingAccess.Refusal, Text(refused));
    }

    /// <summary>
    /// The in-app assistant reads a library's documentation through the same tools, in the same process: the library's tools
    /// for the one it asks about, then a call of one of them. What it says with them is AssistantTests'.
    /// </summary>
    [TestMethod]
    public async Task TheAssistant_Should_ReadALibrarysDocs()
    {
        await using var platform = new FakeBitPlatform();
        await using var server = await StartServer(platform);
        await using var scope = server.ApiApp.Services.CreateAsyncScope();

        var functions = scope.ServiceProvider.GetRequiredService<AppChatbot>().GetAIFunctions().ToDictionary(function => function.Name);

        var bmageTools = await Invoke(functions["GetBitLibraryTools"], new() { ["library"] = "bit Bmage" });
        Assert.Contains("SearchBmage", bmageTools);
        Assert.Contains("GetBmageRecipe", bmageTools);
        Assert.DoesNotContain("SearchBitBlazorUI", bmageTools, "One library's tools at a time.");

        Assert.Contains("no bit platform library named Photoshop", await Invoke(functions["GetBitLibraryTools"], new() { ["library"] = "Photoshop" }));

        using var arguments = JsonDocument.Parse("""{ "query": "an image editor" }""");
        var answer = await Invoke(functions["CallBitLibraryTool"], new() { ["toolName"] = "SearchBmage", ["arguments"] = arguments.RootElement });
        Assert.StartsWith(FakeBitPlatform.BmageAnswer, answer);

        var feedback = await Invoke(functions["CallBitLibraryTool"], new() { ["toolName"] = "SendBitPlatformFeedback", ["arguments"] = null });
        Assert.Contains("not a tool GetBitLibraryTools lists", feedback, "Only the libraries' tools: feedback reaches people.");
    }

    /// <summary>The server, with the far side of /docs-mcp stood in by <paramref name="platform"/>.</summary>
    private async Task<AppTestServer> StartServer(FakeBitPlatform platform, CapturingSender? sender = null, Action<IServiceCollection>? configureServices = null, Action<Microsoft.Extensions.Configuration.ConfigurationManager>? configure = null)
    {
        await platform.Start(TestContext.CancellationToken);

        var server = new AppTestServer();

        await server.Build(configureTestServices: services =>
        {
            // Last in the pipeline but the socket: everything /docs-mcp reaches is answered by the fake.
            services.AddHttpClient(DocsMcpHttp.ClientName).AddHttpMessageHandler(() => new FakeBitPlatform.Handler(platform));

            // These tests are about the documentation, not the license: every project gets it, unless a test says otherwise.
            services.Replace(ServiceDescriptor.Scoped<IDocsMcpAccess, OpenAccess>());

            if (sender is not null)
            {
                services.Replace(ServiceDescriptor.Singleton<ISender>(sender));
            }

            configureServices?.Invoke(services);
        }, configureTestConfiguration: configuration =>
        {
            configuration["DocsMcp:FeedbackEmailAddress"] = TeamAddress;
            configure?.Invoke(configuration);
        }).Start(TestContext.CancellationToken);

        return server;
    }

    private async Task<McpClient> Connect(AppTestServer server, string? query = null)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(server.ApiAppAddress, query is null ? "docs-mcp" : $"docs-mcp?{query}"),
            TransportMode = HttpTransportMode.StreamableHttp
        });

        return await McpClient.CreateAsync(transport, cancellationToken: TestContext.CancellationToken);
    }

    private async Task<string> Call(McpClient mcp, string tool, Dictionary<string, object?>? arguments = null)
    {
        var result = await mcp.CallToolAsync(tool, arguments ?? [], cancellationToken: TestContext.CancellationToken);

        var text = Text(result);
        Assert.IsFalse(result.IsError ?? false, text);

        return text;
    }

    /// <summary>What a function of the assistant returned, as the model reads it.</summary>
    private async Task<string> Invoke(AIFunction function, AIFunctionArguments arguments)
        => await function.InvokeAsync(arguments, TestContext.CancellationToken) switch
        {
            JsonElement { ValueKind: JsonValueKind.String } text => text.GetString()!,
            var result => result?.ToString() ?? ""
        };

    private static string Text(CallToolResult result) => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    /// <summary>Keeps the emails the app sends rather than handing them to an SMTP server.</summary>
    public sealed class CapturingSender : ISender
    {
        public ConcurrentQueue<IFluentEmail> Sent { get; } = [];

        public SendResponse Send(IFluentEmail email, CancellationToken? token = null)
        {
            Sent.Enqueue(email);
            return new SendResponse();
        }

        public Task<SendResponse> SendAsync(IFluentEmail email, CancellationToken? token = null) => Task.FromResult(Send(email, token));
    }

    /// <summary>Answers every project but one, and remembers which it was asked about.</summary>
    private sealed class OpenAccess : IDocsMcpAccess
    {
        public ValueTask<DocsMcpAccessDecision> Decide(string? projectId, CancellationToken cancellationToken) => ValueTask.FromResult(DocsMcpAccessDecision.Allowed);
    }

    private sealed class RecordingAccess : IDocsMcpAccess
    {
        public const string UnlicensedProject = "unlicensed-project";

        public const string Refusal = "This project's license doesn't cover the bit platform's documentation tools.";

        public ConcurrentQueue<string?> Asked { get; } = [];

        public ValueTask<DocsMcpAccessDecision> Decide(string? projectId, CancellationToken cancellationToken)
        {
            Asked.Enqueue(projectId);
            return ValueTask.FromResult(projectId is UnlicensedProject ? new DocsMcpAccessDecision(Refusal) : DocsMcpAccessDecision.Allowed);
        }
    }

    public TestContext TestContext { get; set; } = default!;
}
