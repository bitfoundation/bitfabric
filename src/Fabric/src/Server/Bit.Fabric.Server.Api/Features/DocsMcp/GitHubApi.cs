using System.Text.Json.Nodes;

namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>The reads of GitHub's api /docs-mcp makes, through the DocsMcp http client and with no token.</summary>
internal static class GitHubApi
{
    /// <summary>Bounds the pages followed, so a Link header that never ends can't hold a read.</summary>
    private const int MaxPages = 20;

    public static async Task<JsonNode> Read(IHttpClientFactory httpClientFactory, string url, CancellationToken cancellationToken)
        => (await ReadPage(httpClientFactory.CreateClient(DocsMcpHttp.ClientName), url, cancellationToken)).Content;

    /// <summary>Every item of a list, page after page as the Link headers GitHub pages its lists with name them.</summary>
    public static async Task<List<JsonNode>> ReadList(IHttpClientFactory httpClientFactory, string url, CancellationToken cancellationToken)
    {
        var http = httpClientFactory.CreateClient(DocsMcpHttp.ClientName);
        List<JsonNode> items = [];
        string? next = url;

        for (var page = 0; next is not null && page < MaxPages; page++)
        {
            (var content, next) = await ReadPage(http, next, cancellationToken);
            items.AddRange(content.AsArray().OfType<JsonNode>());
        }

        return items;
    }

    private static async Task<(JsonNode Content, string? Next)> ReadPage(HttpClient http, string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))!, NextPage(response));
    }

    /// <summary>The address a Link header names for the next page: &lt;url&gt;; rel="next".</summary>
    private static string? NextPage(HttpResponseMessage response)
        => response.Headers.TryGetValues("Link", out var links)
            ? links.SelectMany(link => link.Split(','))
                   .Select(link => link.Split(';', StringSplitOptions.TrimEntries))
                   .Where(parts => parts.Length > 1 && parts.Skip(1).Contains("rel=\"next\""))
                   .Select(parts => parts[0].TrimStart('<').TrimEnd('>'))
                   .FirstOrDefault()
            : null;
}

/// <summary>
/// One read of GitHub's api, kept for a quarter of an hour. A read that fails keeps what was read before and is tried again a
/// minute later rather than on every call in between: without a token, the api allows a few dozen requests an hour.
/// </summary>
/// <param name="timeProvider">What the quarter of an hour is counted by.</param>
/// <param name="logger">Where a failed read is logged.</param>
/// <param name="what">Names what is read, in the log line of a failed read.</param>
/// <param name="read">The read itself, given half a minute.</param>
internal sealed class GitHubCache<T>(TimeProvider timeProvider, ILogger logger, string what, Func<CancellationToken, Task<T>> read)
    where T : class
{
    private static readonly TimeSpan cacheLifetime = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan failedReadLifetime = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim sync = new(1, 1);

    private T? value;

    private DateTimeOffset expiresAt;

    /// <summary>Null when GitHub could not be read and nothing was read before.</summary>
    public async Task<T?> Get(CancellationToken cancellationToken)
    {
        if (timeProvider.GetUtcNow() < expiresAt) return value;

        await sync.WaitAsync(cancellationToken);

        try
        {
            if (timeProvider.GetUtcNow() < expiresAt) return value;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            value = await read(timeout.Token);

            expiresAt = timeProvider.GetUtcNow() + cacheLifetime;
        }
        catch (Exception exp) when (cancellationToken.IsCancellationRequested is false)
        {
            logger.LogError(exp, "Reading {What} from GitHub failed.", what);

            expiresAt = timeProvider.GetUtcNow() + failedReadLifetime;
        }
        finally
        {
            sync.Release();
        }

        return value;
    }
}
