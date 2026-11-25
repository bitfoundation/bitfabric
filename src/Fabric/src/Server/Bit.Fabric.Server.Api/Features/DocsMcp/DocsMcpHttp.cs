using System.Text.Json.Nodes;
using Bit.Fabric.Server.Api.Features.DocsMcp.CodeIndex;

namespace Bit.Fabric.Server.Api.Features.DocsMcp;

public static class DocsMcpHttp
{
    /// <summary>
    /// The http client of everything /docs-mcp reaches: the libraries' and the other upstream MCP servers, GitHub's releases,
    /// and the downloads of the source code index. The tests stand the far side of it in.
    /// </summary>
    public const string ClientName = "DocsMcp";

    public static IServiceCollection AddDocsMcp(this IServiceCollection services)
    {
        // Singletons: the proxy keeps one session per upstream for every caller, and the index is prepared once a process.
        services.AddSingleton<DocsMcpServer>();
        services.AddSingleton<DocsMcpProxy>();
        services.AddSingleton<DocsMcpVersions>();
        services.AddSingleton<DocsMcpFeedbackTool>();
        services.AddSingleton<DocsMcpReleasesTool>();
        services.AddSingleton<BitPlatformReleases>();
        services.AddSingleton<BitPlatformTags>();
        services.AddSingleton<BitPlatformSources>();
        services.AddSingleton<CodebaseMemoryCli>();
        services.AddHostedService<DocsMcpCodeIndexer>();

        // Scoped, so the license check that replaces it can read the database.
        services.AddScoped<IDocsMcpAccess, DocsMcpLicenseAccess>();

        // No timeout and no resilience handler of the client's own: an MCP session holds a response stream open for as long
        // as it lives, and a tool call is retried by the proxy itself, once, on a new session. Each use bounds its own wait.
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is still marked experimental.
        services.AddHttpClient(ClientName, httpClient =>
        {
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
            // GitHub's api refuses a request without one.
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("bitFabric-DocsMcp/1.0 (+https://bitfabric.co)");
        }).RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        return services;
    }

    public static void MapDocsMcp(this WebApplication app)
    {
        // Public: the documentation needs no sign-in. Whether a project's license covers it is IDocsMcpAccess's to say.
        app.MapMcp(DocsMcpServer.Path);

        // The version the source code tools read, whatever ?v= names: the newest bit platform tag, once it is indexed, and
        // so one entry at most.
        app.MapGet($"{DocsMcpServer.Path}/versions", (DocsMcpVersions versions) => Results.Text(new JsonObject
        {
            ["latest"] = versions.Served?.Name,
            ["versions"] = versions.Served is { } served ? new JsonArray(new JsonObject { ["name"] = served.Name, ["tag"] = served.Tag.Tag }) : new JsonArray()
        }.ToJsonString(), "application/json")).ExcludeFromDescription();
    }
}
