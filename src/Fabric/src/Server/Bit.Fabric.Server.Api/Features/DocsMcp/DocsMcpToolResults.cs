using ModelContextProtocol.Protocol;

namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>What the tools /docs-mcp answers itself return, and how they read their arguments.</summary>
internal static class DocsMcpToolResults
{
    public static CallToolResult Succeeded(string text)
        => new() { Content = [new TextContentBlock { Text = text }] };

    /// <summary>
    /// A tool that cannot answer says why in its result rather than by throwing: the agent reads the reason and can act on
    /// it, where an exception reaches it as a protocol error with nothing to do about it.
    /// </summary>
    public static CallToolResult Failed(string message)
        => new() { IsError = true, Content = [new TextContentBlock { Text = message }] };

    /// <summary>A string argument, trimmed, or null when it is missing, not a string or blank.</summary>
    public static string? Argument(CallToolRequestParams request, string name)
    {
        if (request.Arguments is null ||
            request.Arguments.TryGetValue(name, out var value) is false ||
            value.ValueKind is not JsonValueKind.String)
            return null;

        return value.GetString()?.Trim() is { Length: > 0 } text ? text : null;
    }
}
