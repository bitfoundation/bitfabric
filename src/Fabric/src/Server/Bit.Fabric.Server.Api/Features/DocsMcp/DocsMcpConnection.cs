namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// What the address an agent connected with asks of /docs-mcp: <c>docs-mcp?libraries=all&amp;project=...</c>. Read once
/// per session (See DocsMcpServer.ConfigureSession), since every request of a session carries the same address.
/// </summary>
/// <param name="EveryLibrary">Whether every bit library's tools are listed (libraries=all), or BlazorUI's and Butil's alone.</param>
/// <param name="ProjectId">The project the agent works on (project), which <see cref="IDocsMcpAccess"/> decides on.</param>
/// <param name="CallerAddress">Where the connection came from, which the feedback tool's limit is counted by.</param>
public sealed record DocsMcpConnection(bool EveryLibrary, string? ProjectId, string CallerAddress)
{
    public static DocsMcpConnection Of(HttpContext httpContext) => new(
        string.Equals(Query(httpContext, "libraries"), "all", StringComparison.OrdinalIgnoreCase),
        Query(httpContext, "project"),
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    private static string? Query(HttpContext httpContext, string name)
        => httpContext.Request.Query[name].FirstOrDefault()?.Trim() is { Length: > 0 } value ? value : null;
}
