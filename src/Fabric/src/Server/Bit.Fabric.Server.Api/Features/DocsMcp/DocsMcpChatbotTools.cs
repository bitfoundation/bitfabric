using System.ComponentModel;
using Bit.Fabric.Shared.Features.Chatbot;
using ModelContextProtocol.Protocol;

namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// The bit platform libraries' documentation for the in-app assistant: the tools of every library's documentation server
/// that /docs-mcp proxies, called on the same <see cref="DocsMcpProxy"/> in this process. Two functions stand for all of
/// them rather than one each: the libraries' servers have a hundred or so tools between them, whose descriptions would
/// otherwise go with every question the assistant is asked, while a question about a library needs one library's.
/// </summary>
public static class DocsMcpChatbotTools
{
    private static readonly string librariesList = string.Join("; ", BitLibraries.All.Select(library => $"{library.Name}: {library.Topic}"));

    public static IEnumerable<AIFunction> AIFunctions(IServiceProvider services)
    {
        var proxy = services.GetRequiredService<DocsMcpProxy>();

        yield return AIFunctionFactory.Create(async (
            [Description("The library, such as BlazorUI or Bmage")] string library,
            CancellationToken cancellationToken) =>
        {
            if (BitLibraries.Find(library) is not { } found)
                return $"There is no bit platform library named {library}. The libraries are: {librariesList}.";

            var tools = await proxy.ListTools(found, cancellationToken);

            if (tools.Count is 0)
                return $"The documentation of {found.Name} can't be reached right now. Say so, and don't answer about it from memory.";

            return JsonSerializer.Serialize(tools.Select(tool => new DocsTool(tool.Name, tool.Description, tool.InputSchema)), DocsMcpChatbotJsonContext.Default.IEnumerableDocsTool);
        }, new AIFunctionFactoryOptions
        {
            Name = AiChatProtocol.Tools.GetBitLibraryTools,
            Description = $"""
                Lists the documentation tools of one bit platform library - each with what it does and the arguments it takes -
                for CallBitLibraryTool to call. Every app made with bit Fabric is built on these libraries: {librariesList}.
                Call it whenever the user asks whether bit Fabric or their app has, can do or how to use something one of them
                covers, then answer from what its tools return rather than from memory.
                """
        });

        yield return AIFunctionFactory.Create(async (
            [Description("A tool GetBitLibraryTools listed")] string toolName,
            [Description("The arguments, as the tool's input schema describes them")] JsonElement? arguments,
            CancellationToken cancellationToken) =>
        {
            // The libraries' tools only: the feedback tool reaches people, and the rest of /docs-mcp is for coding agents.
            if (await proxy.LibraryOf(toolName, cancellationToken) is null)
                return $"{toolName} is not a tool GetBitLibraryTools lists. Call GetBitLibraryTools for the library first.";

            Dictionary<string, JsonElement>? toolArguments = arguments is { ValueKind: JsonValueKind.Object } json
                ? json.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone())
                : null;

            var result = await proxy.CallTool(toolName, toolArguments, version: null, cancellationToken);

            return string.Join("\n\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        }, new AIFunctionFactoryOptions
        {
            Name = AiChatProtocol.Tools.CallBitLibraryTool,
            Description = "Calls one documentation tool GetBitLibraryTools listed, with the arguments its input schema asks for, and returns its answer. Show code from it in fenced code blocks with their language, such as ```razor or ```csharp."
        });
    }

    /// <summary>One library tool, as the assistant reads it.</summary>
    public sealed record DocsTool(string Name, string? Description, JsonElement InputSchema);
}

[JsonSerializable(typeof(IEnumerable<DocsMcpChatbotTools.DocsTool>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class DocsMcpChatbotJsonContext : JsonSerializerContext;
