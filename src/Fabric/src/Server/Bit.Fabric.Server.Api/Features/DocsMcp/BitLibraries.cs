namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// The bit platform libraries, each of which answers for itself through an MCP server of its own at
/// https://{name in lowercase}.bitplatform.dev/mcp, which /docs-mcp proxies. Every list of them an agent reads - the server
/// instructions, the descriptions of the tools that point at them, the chatbot's library tools - is written from here, so
/// adding or renaming a library is one edit.
/// </summary>
public static class BitLibraries
{
    /// <summary>Listed to every caller of /docs-mcp.</summary>
    public static readonly BitLibrary[] Listed =
    [
        new("BlazorUI", "a component, an icon or a theme"),
        new("Butil", "a browser API")
    ];

    /// <summary>Listed to the callers that ask for every library (libraries=all), in this order.</summary>
    public static readonly BitLibrary[] OnRequest =
    [
        new("Bmotion", "an animation"),
        new("Bswup", "the service worker"),
        new("Brouter", "routing"),
        new("Bmage", "an image editor or image processing"),
        new("Bink", "a drawing canvas"),
        new("Balette", "a command palette"),
        new("BbScanner", "a barcode scanner"),
        new("Bdnd", "drag and drop"),
        new("BlazeDb", "a browser database"),
        new("Blow", "a node-based editor"),
        new("Borm", "a schema-driven form"),
        new("Bcrdt", "real-time collaboration"),
        new("Bonboarding", "a product tour"),
        new("Bortable", "a sortable list"),
        new("Blex", "state management"),
        new("Bsqlite", "an EF Core database in the browser"),
        new("Belemetry", "telemetry"),
        new("Bheadless", "accessible unstyled UI primitives")
    ];

    public static readonly BitLibrary[] All = [.. Listed, .. OnRequest];

    /// <summary>The library named <paramref name="name"/>, with or without its bit prefix and in any case.</summary>
    public static BitLibrary? Find(string? name)
    {
        var trimmed = name?.Trim();

        if (trimmed?.StartsWith("bit ", StringComparison.OrdinalIgnoreCase) is true)
        {
            trimmed = trimmed[4..].Trim();
        }

        return All.FirstOrDefault(library => string.Equals(library.Name, trimmed, StringComparison.OrdinalIgnoreCase)
                                             || string.Equals($"Bit{library.Name}", trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The names of <paramref name="libraries"/> the way a sentence lists them: "Bmotion, Bswup and Brouter".</summary>
    public static string Names(IEnumerable<BitLibrary> libraries) => List(libraries.Select(library => library.Name), "and");

    /// <summary>Joins <paramref name="items"/> the way a sentence lists them: "a, b and c", or "a, b or c".</summary>
    public static string List(IEnumerable<string> items, string conjunction)
    {
        var array = items.ToArray();

        return array.Length < 2 ? string.Concat(array) : $"{string.Join(", ", array[..^1])} {conjunction} {array[^1]}";
    }
}

/// <param name="Name">The name its package and its MCP tools carry, without the bit prefix.</param>
/// <param name="Topic">What a question has to be about for this library to answer it, worded to follow "a question about".</param>
public sealed record BitLibrary(string Name, string Topic)
{
    /// <summary>Its own MCP server, which /docs-mcp proxies.</summary>
    public Uri Address { get; } = new($"https://{Name.ToLowerInvariant()}.bitplatform.dev/mcp");
}
