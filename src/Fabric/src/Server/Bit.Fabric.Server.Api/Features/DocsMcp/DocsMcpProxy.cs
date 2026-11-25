using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bit.Fabric.Server.Api.Features.DocsMcp.CodeIndex;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// Backs /docs-mcp by fanning it out to the MCP servers below, so one connection exposes the tools of all of them: the
/// documentation server of every bit library (<see cref="BitLibraries"/>), Microsoft Learn and DeepWiki, which the bit
/// platform team develops against, and the source code index, a codebase-memory process this server runs over stdio (See
/// DocsMcpCodeIndexer). An upstream may be narrowed down to a subset of its tools, each then exposed under a name and a
/// description written here. The chatbot calls the same instance in the same process (See DocsMcpChatbotTools).
/// <para>
/// One session per upstream, shared by every caller: the tools are documentation lookups that carry no per-caller state,
/// so a session per caller would only multiply the handshakes, and the stdio session owns the one codebase-memory process.
/// </para>
/// </summary>
public class DocsMcpProxy(IHttpClientFactory httpClientFactory,
    CodebaseMemoryCli codebaseMemory,
    DocsMcpVersions versions,
    ServerApiSettings settings,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory,
    ILogger<DocsMcpProxy> logger) : IAsyncDisposable
{
    /// <summary>
    /// The description DeepWiki gives its ask_wiki_question tool says no more than that it answers questions about a GitHub
    /// repository, which leaves the agent to guess whether a repository worth asking exists at all. Naming the third party
    /// repositories the bit platform team relies on, and what each one is the right source for, turns it into a tool an
    /// agent reaches for on its own instead of one it only uses when it is told to.
    /// </summary>
    private static readonly string askQuestionDescription = $"""
        Asks a question about a public GitHub repository and answers it from that repository's own source code and
        documentation. Prefer it over a web search whenever the question is about one of these libraries:
        - riok/mapperly: object mapping between entities and DTOs, focusing on its partial static classes and extension methods approach.
        - microsoft/aspire: .NET Aspire AppHost orchestration, resource configuration, replacing Docker resources with their Azure equivalents, service discovery and integrations. It significantly outperforms Microsoft Learn for code level questions.
        - HangfireIO/Hangfire: background job scheduling, recurring jobs, filters, storage configuration and distributed processing.
        - ZiggyCreatures/FusionCache: hybrid caching, L2 cache backplane, distributed locking, OpenTelemetry integration and cache factory configuration.
        - keycloak/keycloak: Keycloak and realm configuration.
        - microsoft/agent-framework: agent creation, multi agent orchestration, workflows, tools and function calling, MCP, A2A communication, memory and context, and provider integrations.
        Do not ask it about the bit platform libraries themselves: {BitLibraries.Names(BitLibraries.All)} each have
        their own dedicated tools, {ListedLibrariesTools} on this server and every other library's on its own server at https://<name>.bitplatform.dev/mcp, and those are written from the library rather than inferred from it.
        """;

    /// <summary>"BlazorUI's and Butil's": the libraries whose tools every caller gets listed.</summary>
    private static string ListedLibrariesTools => BitLibraries.List(BitLibraries.Listed.Select(library => $"{library.Name}'s"), "and");

    /// <summary>
    /// Ends the description of every source code tool: an agent may also have a codebase-memory server of its own, which
    /// answers about its own project.
    /// </summary>
    private static readonly string bitPlatformSourceScope = $"It reads the newest bit platform code - the newest tag of bitfoundation/bitplatform, a prerelease included, which the server instructions name - whatever release this connection asked for (the v query of this endpoint), and never the caller's own workspace, not even a project created from a bit template. It holds the bit Boilerplate project template (src/Templates/Boilerplate/Bit.Boilerplate, src/Boilerplate/Bit.Boilerplate of the bitfoundation/bitfabric GitHub repository), and of the bitfoundation/bitplatform GitHub repository the bit platform websites, the build time projects and one bit library, BlazorES2019 (src/Toolings/BlazorES2019), but none of the other bit libraries: {BitLibraries.Names(BitLibraries.All)} each answer for themselves through their own tools, {ListedLibrariesTools} on this server and every other library's on its own server at https://<name>.bitplatform.dev/mcp, so a question about {BitLibraries.List(BitLibraries.All.Select(library => library.Topic), "or")} goes to those rather than to a search here.";

    /// <summary>
    /// Only three read tools are exposed: trace_path binds calls by bare method name, which misleads across this monorepo,
    /// and get_architecture only counts nodes. Each has a stand-in schema of its main arguments, listed while no version is
    /// served: the tool is there to call, and its answer says why it can't answer yet.
    /// </summary>
    private static readonly ExposedTool[] codeIndexTools =
    [
        new("search_graph", "FindBitPlatformSymbols", $"Finds classes, methods, routes and other symbols by keywords (query) or by a regex over their names (name_pattern), returning the qualified names GetBitPlatformSymbolSource takes. Page with offset while has_more is true. {bitPlatformSourceScope}",
            StandIn(("query", "Keywords to look the symbols up by."), ("name_pattern", "A regex over the symbols' names."))),
        new("search_code", "SearchBitPlatformCode", $"Greps for a text, or a regex with regex set, and returns the matches grouped by their enclosing method or class, with signatures and line numbers. The one to use for literal or non-code text; narrow it with file_pattern or path_filter. It matches case-sensitively; for any case, set regex and start the pattern with (?i). {bitPlatformSourceScope}",
            StandIn(("pattern", "The text, or with regex set the regex, to look for."))),
        new("get_code_snippet", "GetBitPlatformSymbolSource", $"Returns the full source of one class, method or other symbol, addressed by a qualified name FindBitPlatformSymbols or SearchBitPlatformCode returned. A short name answers with candidates when it is ambiguous. {bitPlatformSourceScope}",
            StandIn(("qualified_name", "The qualified name FindBitPlatformSymbols or SearchBitPlatformCode returned.")))
    ];

    /// <summary>
    /// Tool definitions only change when an upstream server is redeployed, so the merged list is cached. This lifetime
    /// bounds how long a newly published tool takes to show up on /docs-mcp.
    /// </summary>
    private static readonly TimeSpan toolsCacheLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// After a failed refresh the merged list is missing the tools of at least one server, so it is kept only briefly: the
    /// next caller retries the servers that failed instead of waiting out the full <see cref="toolsCacheLifetime"/>.
    /// </summary>
    private static readonly TimeSpan failedToolsCacheLifetime = TimeSpan.FromMinutes(1);

    /// <summary>How long one upstream may take to list its tools before it is left out of that list.</summary>
    private static readonly TimeSpan listTimeout = TimeSpan.FromSeconds(20);

    /// <summary>How long one tool call may take upstream before the caller is told it did not answer.</summary>
    private static readonly TimeSpan callTimeout = TimeSpan.FromMinutes(2);

    /// <summary>The tools this server answers itself, ahead of every upstream's in the list.</summary>
    private static readonly Tool[] ownTools = [DocsMcpFeedbackTool.Definition, DocsMcpReleasesTool.Definition];

    /// <summary>
    /// In the order their tools are listed. The source code index's tools take a project argument naming the index to
    /// answer from, a deployment detail no caller should have to know: it is stripped from the listed schemas and filled in
    /// with the version served on the way through. Renamed, as a developer's own codebase-memory server
    /// answers about their project under the original names.
    /// </summary>
    private readonly Upstream[] upstreams =
    [
        .. BitLibraries.Listed.Select(library => new Upstream($"the bit {library.Name} documentation server", library.Address, library)),
        new("the source code index", url: null, exposedTools: codeIndexTools),
        new("Microsoft Learn", new("https://learn.microsoft.com/api/mcp")),
        // Only ask_wiki_question is exposed: it answers against the whole repository by itself, while read_wiki_structure and
        // read_wiki_contents dump the generated wiki of a repository, a slower and far more token hungry way to the same
        // answer. Renamed, as a developer may have DeepWiki's own server installed next to this one.
        new("DeepWiki", new("https://mcp.deepwiki.com/mcp"), exposedTools: [new("ask_wiki_question", "AskGitHubRepository", askQuestionDescription)]),
        .. BitLibraries.OnRequest.Select(library => new Upstream($"the bit {library.Name} documentation server", library.Address, library))
    ];

    private readonly SemaphoreSlim toolSetSync = new(1, 1);

    private ToolSet toolSet = new();

    /// <param name="everyLibrary">Every library's tools, or <see cref="BitLibraries.Listed"/>'s alone.</param>
    /// <param name="cancellationToken">Ends the wait for the upstreams to list theirs.</param>
    public async ValueTask<IReadOnlyList<Tool>> ListTools(bool everyLibrary, CancellationToken cancellationToken)
    {
        var current = await GetToolSet(cancellationToken);

        return everyLibrary ? current.Tools : current.DefaultTools;
    }

    /// <summary>The tools of one library's documentation server, for the chatbot. Empty while that server can't be listed.</summary>
    public async ValueTask<IReadOnlyList<Tool>> ListTools(BitLibrary library, CancellationToken cancellationToken)
    {
        var current = await GetToolSet(cancellationToken);

        return [.. current.Tools.Where(tool => current.UpstreamPerToolName.GetValueOrDefault(tool.Name)?.Library == library)];
    }

    /// <summary>The library whose documentation server provides <paramref name="toolName"/>, or null for any other tool.</summary>
    public async ValueTask<BitLibrary?> LibraryOf(string toolName, CancellationToken cancellationToken)
        => (await GetToolSet(cancellationToken)).UpstreamPerToolName.GetValueOrDefault(toolName)?.Library;

    /// <summary>Answers one call of an upstream's tool, reading the source code of <paramref name="version"/> for a source tool.</summary>
    /// <exception cref="McpException">No upstream provides the tool.</exception>
    public async ValueTask<CallToolResult> CallTool(string toolName, IDictionary<string, JsonElement>? arguments, DocsMcpVersion? version, CancellationToken cancellationToken)
    {
        if ((await GetToolSet(cancellationToken)).UpstreamPerToolName.TryGetValue(toolName, out var upstream) is false)
            throw new McpException($"Unknown tool: '{toolName}'.");

        if (upstream.IsCodeIndex)
        {
            if (version is null)
                return DocsMcpToolResults.Failed($"The source code index is not available: {versions.Unavailable}. Read the code on https://github.com/bitfoundation/bitplatform instead.");

            // Stripped from the listed schema, so it is filled in here - overwriting any value a caller sends anyway, since
            // the version served is the one index that answers.
            arguments = new Dictionary<string, JsonElement>(arguments ?? new Dictionary<string, JsonElement>())
            {
                ["project"] = JsonSerializer.SerializeToElement(version.CodebaseMemoryProject)
            };
        }

        // Only the name and the arguments are forwarded: the rest (progress token, meta) belongs to the session between
        // the caller and this server, not to the one between this server and the upstream.
        CallToolRequestParams upstreamRequest = new() { Name = upstream.GetToolName(toolName), Arguments = arguments };

        CallToolResult result;

        try
        {
            result = await CallUpstream(upstream, upstreamRequest, cancellationToken);
        }
        catch (Exception exp) when (cancellationToken.IsCancellationRequested is false)
        {
            // The upstream session is long lived and shared, so a redeploy of the upstream can end it at any moment.
            // Reconnecting and retrying once turns that into a slower call rather than a failed one. A tool that itself
            // fails doesn't land here: it reports that through an IsError result instead.
            logger.LogWarning(exp, "Calling {ToolName} on {McpServerName} failed, reconnecting and retrying once.", toolName, upstream.Name);

            await Disconnect(upstream);

            try
            {
                result = await CallUpstream(upstream, upstreamRequest, cancellationToken);
            }
            catch (Exception retryExp) when (cancellationToken.IsCancellationRequested is false)
            {
                logger.LogError(retryExp, "Calling {ToolName} on {McpServerName} failed again.", toolName, upstream.Name);

                await Disconnect(upstream);

                return DocsMcpToolResults.Failed($"{upstream.Name} did not answer, so {toolName} could not either. Try it again in a minute.");
            }
        }

        foreach (var block in result.Content.OfType<TextContentBlock>())
        {
            block.Text = AdaptResultText(upstream, version, block.Text, result.IsError is true);
        }

        // get_code_snippet repeats its answer as structured content.
        if (result.StructuredContent is { } structuredContent)
        {
            result.StructuredContent = JsonSerializer.Deserialize<JsonElement>(AdaptResultText(upstream, version, structuredContent.GetRawText(), result.IsError is true));
        }

        return result;
    }

    private async Task<CallToolResult> CallUpstream(Upstream upstream, CallToolRequestParams request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(callTimeout);

        return await (await GetClient(upstream, timeout.Token)).CallToolAsync(request, timeout.Token);
    }

    /// <summary>
    /// The index prefixes every qualified name with its project name and answers with absolute paths: details of this
    /// machine that callers do not need, as a qualified name resolves without that prefix too. Tool names are renamed in
    /// errors only, since an answer can quote source code that mentions them.
    /// </summary>
    private string AdaptResultText(Upstream upstream, DocsMcpVersion? version, string text, bool isError)
    {
        if (upstream.IsCodeIndex && version is not null)
        {
            text = text.Replace($"{version.CodebaseMemoryProject}.", null, StringComparison.Ordinal);

            var sourcesRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(version.SourcesPath)).Replace('\\', '/');
            text = text.Replace($"{sourcesRoot}/", null, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        return isError ? RenameTools(upstream, text) : text;
    }

    /// <summary>A schema, or the hint of an error, may point at a sibling tool by the name only its own server knows it under.</summary>
    private static string RenameTools(Upstream upstream, string text)
    {
        if (upstream.ExposedTools is null) return text;

        foreach (var exposedTool in upstream.ExposedTools.Values)
        {
            text = Regex.Replace(text, $@"\b{Regex.Escape(exposedTool.Name)}\b", exposedTool.ExposedName);
        }

        return text;
    }

    private async ValueTask<ToolSet> GetToolSet(CancellationToken cancellationToken)
    {
        var current = toolSet;

        if (timeProvider.GetUtcNow() < current.ExpiresAt && current.Generation == versions.Generation) return current;

        await toolSetSync.WaitAsync(cancellationToken);

        try
        {
            current = toolSet;

            if (timeProvider.GetUtcNow() < current.ExpiresAt && current.Generation == versions.Generation) return current;

            // A retry after a failed refresh lists only the servers that failed: the tools of the others are as current as
            // the last full refresh, and listing every server again would put all of their round trips in front of a
            // caller every minute for as long as one of them stays down. A new version served lists the index again.
            var isFullRefresh = current.Generation != versions.Generation || timeProvider.GetUtcNow() >= current.RefreshedAt + toolsCacheLifetime;
            Dictionary<Upstream, IReadOnlyList<Tool>> listed = isFullRefresh ? [] : current.ToolsPerUpstream;

            var toolsPerUpstream = await Task.WhenAll(upstreams.Select(async upstream => (Upstream: upstream, Tools: listed.GetValueOrDefault(upstream) ?? await ListTools(upstream, cancellationToken))));

            List<Tool> tools = [.. ownTools];
            List<Tool> defaultTools = [.. ownTools];
            Dictionary<string, Upstream> upstreamPerToolName = new(StringComparer.Ordinal);

            foreach (var (upstream, upstreamTools) in toolsPerUpstream)
            {
                foreach (var tool in upstreamTools ?? [])
                {
                    if (ownTools.Any(own => own.Name == tool.Name) || upstreamPerToolName.TryAdd(tool.Name, upstream) is false)
                    {
                        // Tool names are the only address an MCP client has, so two servers claiming the same name cannot
                        // both be exposed. The first one wins and the clash is reported.
                        logger.LogWarning("The {ToolName} tool of {McpServerName} is not exposed because {OtherMcpServerName} already provides a tool with that name.",
                            tool.Name, upstream.Name, upstreamPerToolName.GetValueOrDefault(tool.Name)?.Name ?? "this server");
                        continue;
                    }

                    tools.Add(tool);

                    if (upstream.Library is null || BitLibraries.Listed.Contains(upstream.Library))
                    {
                        defaultTools.Add(tool);
                    }
                }
            }

            var now = timeProvider.GetUtcNow();
            var refreshedAt = isFullRefresh ? now : current.RefreshedAt;
            var fullRefreshAt = refreshedAt + toolsCacheLifetime;

            toolSet = new()
            {
                Tools = [.. tools],
                DefaultTools = [.. defaultTools],
                UpstreamPerToolName = upstreamPerToolName,
                ToolsPerUpstream = toolsPerUpstream.Where(listing => listing.Tools is not null).ToDictionary(listing => listing.Upstream, listing => listing.Tools!),
                Generation = versions.Generation,
                RefreshedAt = refreshedAt,
                ExpiresAt = toolsPerUpstream.Any(listing => listing.Tools is null) && now + failedToolsCacheLifetime < fullRefreshAt ? now + failedToolsCacheLifetime : fullRefreshAt
            };

            return toolSet;
        }
        finally
        {
            toolSetSync.Release();
        }
    }

    /// <returns>The tools of <paramref name="upstream"/> it is configured to expose, or null if they could not be listed.</returns>
    private async Task<IReadOnlyList<Tool>?> ListTools(Upstream upstream, CancellationToken cancellationToken)
    {
        // Nothing to run the index's tools over yet, so they are listed as their stand-ins (See codeIndexTools).
        if (upstream.IsCodeIndex && versions.Served is null)
            return [.. codeIndexTools.Select(exposedTool => ReadOnly(new Tool { Name = exposedTool.ExposedName, Description = exposedTool.Description, InputSchema = exposedTool.StandInSchema!.Value }))];

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(listTimeout);

            var client = await GetClient(upstream, timeout.Token);

            List<Tool> upstreamTools = [];
            string? cursor = null;

            do
            {
                var page = await client.ListToolsAsync(new ListToolsRequestParams { Cursor = cursor }, timeout.Token);
                upstreamTools.AddRange(page.Tools);
                cursor = page.NextCursor;
            } while (cursor is not null);

            if (upstream.ExposedTools is null) return [.. upstreamTools.Select(ReadOnly)];

            // A tool that disappears upstream silently drops off this endpoint, so the mismatch is reported: it means the
            // configuration above no longer matches what the server provides.
            var missingToolNames = upstream.ExposedTools.Keys.Except(upstreamTools.Select(tool => tool.Name)).ToArray();
            if (missingToolNames.Length > 0)
            {
                logger.LogError("{McpServerName} does not provide the {ToolNames} tool(s) /docs-mcp is configured to expose.", upstream.Name, string.Join(", ", missingToolNames));
            }

            List<Tool> exposedTools = [];

            foreach (var tool in upstreamTools)
            {
                if (upstream.ExposedTools.TryGetValue(tool.Name, out var exposedTool) is false) continue;

                // Freshly deserialized per call, so the tool of the upstream's own list result is never the one changed here.
                tool.Name = exposedTool.ExposedName;
                tool.Title = null; // It spells out the name the tool is renamed from.
                tool.Description = exposedTool.Description;
                tool.InputSchema = JsonSerializer.Deserialize<JsonElement>(RenameTools(upstream, tool.InputSchema.GetRawText()));

                if (upstream.IsCodeIndex)
                {
                    tool.InputSchema = WithoutProject(tool.InputSchema);
                }

                exposedTools.Add(ReadOnly(tool));
            }

            return exposedTools;
        }
        catch (Exception exp) when (cancellationToken.IsCancellationRequested is false)
        {
            // One unreachable server must not empty out the whole endpoint: the tools of every other server stay available
            // and this one is picked up again by the next refresh.
            logger.LogError(exp, "Listing the tools of {McpServerName} failed.", upstream.Name);

            await Disconnect(upstream);

            return null;
        }
    }

    /// <summary>
    /// Every tool served here only looks things up, but some servers leave that unsaid and codebase-memory marks its read
    /// tools destructive, so clients would ask to approve each call.
    /// </summary>
    private static Tool ReadOnly(Tool tool)
    {
        tool.Annotations = new()
        {
            Title = tool.Annotations?.Title,
            IdempotentHint = tool.Annotations?.IdempotentHint,
            OpenWorldHint = tool.Annotations?.OpenWorldHint,
            ReadOnlyHint = true,
            DestructiveHint = false
        };

        return tool;
    }

    /// <summary>
    /// The project argument is filled in on the way through, so callers must not see it: a required parameter the schema
    /// advertises but the server fills in would only make every model waste effort inventing a value for it.
    /// </summary>
    private static JsonElement WithoutProject(JsonElement inputSchema)
    {
        var schema = JsonNode.Parse(inputSchema.GetRawText())!.AsObject();

        schema["properties"]?.AsObject().Remove("project");

        if (schema["required"] is JsonArray required && required.FirstOrDefault(entry => entry?.GetValue<string>() is "project") is { } projectEntry)
        {
            required.Remove(projectEntry);
        }

        return JsonSerializer.SerializeToElement(schema);
    }

    private static JsonElement StandIn(params (string Name, string Description)[] arguments) => JsonSerializer.SerializeToElement(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(arguments.Select(argument => KeyValuePair.Create(argument.Name, (JsonNode?)new JsonObject
        {
            ["type"] = "string",
            ["description"] = argument.Description
        })))
    });

    private async ValueTask<McpClient> GetClient(Upstream upstream, CancellationToken cancellationToken)
    {
        if (upstream.Client is { } connected) return connected;

        await upstream.ClientSync.WaitAsync(cancellationToken);

        try
        {
            if (upstream.Client is { } connectedMeanwhile) return connectedMeanwhile;

            IClientTransport transport = upstream.Url is not null
                ? new HttpClientTransport(new()
                {
                    Name = upstream.Name,
                    Endpoint = upstream.Url,
                    TransportMode = HttpTransportMode.StreamableHttp
                }, httpClientFactory.CreateClient(DocsMcpHttp.ClientName), loggerFactory, ownsHttpClient: true)
                : new StdioClientTransport(new()
                {
                    Name = upstream.Name,
                    // Installed before the first version was indexed, which is what lists the index's tools at all.
                    Command = codebaseMemory.ExecutablePath!,
                    // The data directory the indexes were built in, which no other client on this machine holds.
                    EnvironmentVariables = codebaseMemory.Environment,
                    // Nothing to watch: the version served sits on a tag and never moves.
                    WorkingDirectory = settings.DocsMcp.CodeIndex.DataDirectoryPath
                }, loggerFactory);

            return upstream.Client = await McpClient.CreateAsync(transport, new()
            {
                ClientInfo = new() { Name = "bitfabric-docs-mcp", Version = typeof(DocsMcpProxy).Assembly.GetName().Version!.ToString() }
            }, loggerFactory, cancellationToken);
        }
        finally
        {
            upstream.ClientSync.Release();
        }
    }

    private async Task Disconnect(Upstream upstream)
    {
        await upstream.ClientSync.WaitAsync();

        var client = upstream.Client;
        upstream.Client = null;

        try
        {
            if (client is not null)
            {
                await client.DisposeAsync();
            }
        }
        catch (Exception exp)
        {
            logger.LogWarning(exp, "Disconnecting from {McpServerName} failed.", upstream.Name);
        }
        finally
        {
            upstream.ClientSync.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var upstream in upstreams)
        {
            await Disconnect(upstream);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>The merged tool list, and which upstream each of its tools came from.</summary>
    private sealed class ToolSet
    {
        public Tool[] Tools { get; init; } = [];

        public Tool[] DefaultTools { get; init; } = [];

        public Dictionary<string, Upstream> UpstreamPerToolName { get; init; } = new(StringComparer.Ordinal);

        /// <summary>The tools of every upstream that answered, so a retry only lists the ones that did not.</summary>
        public Dictionary<Upstream, IReadOnlyList<Tool>> ToolsPerUpstream { get; init; } = [];

        /// <summary>When every upstream was last listed, which bounds how old the tools of <see cref="ToolsPerUpstream"/> get.</summary>
        public DateTimeOffset RefreshedAt { get; init; }

        public DateTimeOffset ExpiresAt { get; init; }

        /// <summary>The <see cref="DocsMcpVersions.Generation"/> this was merged against.</summary>
        public int Generation { get; init; } = -1;
    }

    /// <param name="Name">The tool, out of the ones its server provides, that this server exposes.</param>
    /// <param name="ExposedName">The name it is exposed under, apart from the names other MCP servers give their tools.</param>
    /// <param name="Description">The description it is exposed with, rather than the one its own server provides.</param>
    /// <param name="StandInSchema">What it is listed with while its server has nothing to answer from (See codeIndexTools).</param>
    private sealed record ExposedTool(string Name, string ExposedName, string Description, JsonElement? StandInSchema = null);

    private sealed class Upstream(string name, Uri? url, BitLibrary? library = null, ExposedTool[]? exposedTools = null)
    {
        /// <summary>For logs and errors, worded to start a sentence: "the bit Bmage documentation server", "DeepWiki".</summary>
        public string Name { get; } = name;

        /// <summary>The endpoint of an http upstream, or null for the stdio one, the source code index.</summary>
        public Uri? Url { get; } = url;

        public bool IsCodeIndex => Url is null;

        /// <summary>The library whose documentation server this is, or null for any other upstream.</summary>
        public BitLibrary? Library { get; } = library;

        /// <summary>The tools of this server to expose, keyed by their own name, or null to expose all of them as they are.</summary>
        public Dictionary<string, ExposedTool>? ExposedTools { get; } = exposedTools?.ToDictionary(exposedTool => exposedTool.Name, StringComparer.Ordinal);

        /// <summary>The name this server gives the tool exposed as <paramref name="exposedName"/>.</summary>
        public string GetToolName(string exposedName)
            => ExposedTools?.Values.FirstOrDefault(exposedTool => exposedTool.ExposedName == exposedName)?.Name ?? exposedName;

        public McpClient? Client { get; set; }

        public SemaphoreSlim ClientSync { get; } = new(1, 1);
    }
}
