using ModelContextProtocol.Protocol;

namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// The bit platform's documentation MCP server, at /docs-mcp (https://api.bitfabric.co/docs-mcp): every bit library's
/// tools, the source code index of bitfoundation/bitplatform, the releases and feedback tools, and Microsoft Learn's and
/// DeepWiki's, behind one address and no sign-in. It shares the api's one MCP server registration with /mcp and /dev-mcp,
/// each session taking the tools and instructions of the path it was opened on (See DevMcpServiceCollectionExtensions).
/// </summary>
public class DocsMcpServer(DocsMcpProxy proxy, DocsMcpVersions versions, DocsMcpFeedbackTool feedbackTool, DocsMcpReleasesTool releasesTool)
{
    public const string Path = "/docs-mcp";

    /// <summary>
    /// Gives a session opened on <see cref="Path"/> this server's name, instructions and tools in place of the chatbot's,
    /// for the address it was opened with (See <see cref="DocsMcpConnection"/>).
    /// </summary>
    public async Task ConfigureSession(HttpContext httpContext, McpServerOptions options, CancellationToken cancellationToken)
    {
        var connection = DocsMcpConnection.Of(httpContext);
        var access = await httpContext.RequestServices.GetRequiredService<IDocsMcpAccess>().Decide(connection.ProjectId, cancellationToken);

        options.ServerInfo = new()
        {
            Name = "bitplatform",
            Title = "bit platform",
            Version = typeof(DocsMcpServer).Assembly.GetName().Version?.ToString(3) ?? "0.0.1"
        };
        options.ServerInstructions = access.IsAllowed ? Instructions(connection) : $"{Instructions(connection)} {access.Refusal}";
        options.ToolCollection = null; // The chatbot's tools are /mcp's.
        options.Handlers.ListToolsHandler = async (request, cancellationToken) => new() { Tools = [.. await proxy.ListTools(connection.EveryLibrary, cancellationToken)] };
        options.Handlers.CallToolHandler = (request, cancellationToken) => CallTool(connection, request.Services!, request.Params!, cancellationToken);
    }

    private string Instructions(DocsMcpConnection connection)
        => $"Provides the bit {BitLibraries.Names(BitLibraries.Listed)} documentation tools{(connection.EveryLibrary ? $", those of every other bit library ({BitLibraries.Names(BitLibraries.OnRequest)})" : "")}, a source code index of the bitfoundation/bitplatform repository and the tools of the other MCP servers the bit platform team develops against. "
           + (connection.EveryLibrary ? "" : $"Every other bit library - {BitLibraries.Names(BitLibraries.OnRequest)} - has an MCP server of its own at https://<name in lowercase>.bitplatform.dev/mcp, such as https://bmotion.bitplatform.dev/mcp: plain HTTP with no sign-in, so when one is not in your MCP configuration, send its JSON-RPC requests (tools/list, then tools/call) to that address directly, or connect to this endpoint with libraries=all to get their tools here. ")
           + $"Each library's tools answer from its newest documentation. {versions.Describe()}";

    /// <param name="connection">The address the agent connected with.</param>
    /// <param name="requestServices">The request's own scope, which the access check and the feedback tool's email are resolved from.</param>
    /// <param name="request">The call.</param>
    /// <param name="cancellationToken">Ends the call.</param>
    public async ValueTask<CallToolResult> CallTool(DocsMcpConnection connection, IServiceProvider requestServices, CallToolRequestParams request, CancellationToken cancellationToken)
    {
        var access = await requestServices.GetRequiredService<IDocsMcpAccess>().Decide(connection.ProjectId, cancellationToken);

        if (access.IsAllowed is false)
            return DocsMcpToolResults.Failed(access.Refusal!);

        return request.Name switch
        {
            DocsMcpFeedbackTool.ToolName => await feedbackTool.Call(request, connection, requestServices, cancellationToken),
            DocsMcpReleasesTool.ToolName => await releasesTool.Call(request, connection.RequestedVersion, cancellationToken),
            _ => await proxy.CallTool(request.Name, request.Arguments, versions.Served, cancellationToken)
        };
    }
}
