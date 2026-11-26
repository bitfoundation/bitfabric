using Anthropic.Models.Messages;

namespace Bit.Fabric.Server.Api.Infrastructure.Services;

/// <summary>
/// Claude's prompt cache for the chat, which Anthropic's api keeps only where a request marks it: the first system message -
/// the site's pages and the license, the same for every chat and evaluation - is kept an hour, and the rest of each
/// conversation up to its last message five minutes, so a chat's next answer and the next visitor's first one read the
/// site from the cache rather than paying for it again (bitfabric-internal/specs/home-page.md, "The AI evaluation").
/// </summary>
public class AnthropicPromptCache(IChatClient innerClient, string model) : DelegatingChatClient(innerClient)
{
    /// <summary>What a request may write when it sets no limit: room for Claude to think and then answer.</summary>
    public const int DefaultMaxOutputTokens = 8000;

    /// <summary>Where the SDK reads a content's cache control from (Microsoft.Extensions.AI.AIContentCacheExtensions).</summary>
    private const string CacheControlKey = "anthropic:cache_control";

    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => base.GetResponseAsync(Marked(messages), WithAutomaticCache(options), cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => base.GetStreamingResponseAsync(Marked(messages), WithAutomaticCache(options), cancellationToken);

    /// <summary>The first system message ends with a breakpoint kept an hour: the longer-lived entry comes first, as the api requires.</summary>
    private static List<ChatMessage> Marked(IEnumerable<ChatMessage> messages)
    {
        var list = messages.ToList();

        var index = list.FindIndex(message => message.Role == ChatRole.System);
        if (index < 0 || list[index].Contents.LastOrDefault() is not TextContent last || last.AdditionalProperties?.ContainsKey(CacheControlKey) is true)
            return list;

        var marked = list[index].Clone();
        marked.Contents = [.. list[index].Contents.SkipLast(1), new TextContent(last.Text).WithCacheControl(new CacheControlEphemeral { Ttl = Ttl.Ttl1h })];
        list[index] = marked;

        return list;
    }

    /// <summary>The conversation's tail is cached as it grows, with the request's own model and limit.</summary>
    private ChatOptions WithAutomaticCache(ChatOptions? options)
    {
        options = options?.Clone() ?? new();

        if (options.RawRepresentationFactory is null)
        {
            var maxTokens = options.MaxOutputTokens ?? DefaultMaxOutputTokens;
            var modelId = options.ModelId ?? model;

            options.RawRepresentationFactory = _ => new MessageCreateParams
            {
                Model = modelId,
                MaxTokens = maxTokens,
                Messages = [],
                CacheControl = new CacheControlEphemeral()
            };
        }

        return options;
    }
}
