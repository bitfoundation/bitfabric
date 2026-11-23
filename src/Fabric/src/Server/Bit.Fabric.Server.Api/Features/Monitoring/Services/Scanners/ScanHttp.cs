using System.Net;
using System.Text;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;

/// <summary>
/// The scanners' requests to a customer's app: anonymous GETs through the monitoring http client, so every one goes through
/// its public-address guard and none follows a redirect the guard didn't see, each with a short timeout of its own
/// (<see cref="MonitoringOptions.ScanRequestTimeout"/>). Nothing it reads is more than any visitor sees.
/// </summary>
public partial class ScanHttp(IHttpClientFactory httpClientFactory, ServerApiSettings settings, ILogger<ScanHttp> logger)
{
    public const string UserAgent = "bitFabric-Scanner/1.0 (+https://bitfabric.co)";

    /// <summary>More than any page or sitemap a scanner reads; the rest of a bigger body is left unread.</summary>
    private const int MaxBodyLength = 1024 * 1024;

    /// <summary>
    /// What <paramref name="url"/> answers, or null when nothing did. With <paramref name="followRedirects"/>, redirects
    /// within the same origin are followed as a browser would (three at most), and the cookies every step set are kept.
    /// <paramref name="readBody"/> leaves a body unread, such as an endpoint that might stream for as long as it's open.
    /// </summary>
    public async Task<ScanResponse?> Get(string url, CancellationToken cancellationToken, bool followRedirects = false, bool readBody = true)
    {
        var uri = new Uri(url);
        List<string> cookies = [];

        try
        {
            var http = httpClientFactory.CreateClient(MonitoringHttp.ClientName);

            for (var redirects = 0; ; redirects++)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(settings.Monitoring.ScanRequestTimeout);

                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                // A name of its own, so the customer tells a scan from the minute's health check in their logs.
                request.Headers.UserAgent.ParseAdd(UserAgent);

                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

                if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
                {
                    cookies.AddRange(setCookies);
                }

                var status = (int)response.StatusCode;

                if (followRedirects && status is >= 300 and < 400 && response.Headers.Location is { } location && redirects < 3)
                {
                    var next = new Uri(uri, location);

                    if (string.Equals(next.GetLeftPart(UriPartial.Authority), uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
                    {
                        uri = next;
                        continue;
                    }
                }

                Dictionary<string, string[]> headers = new(StringComparer.OrdinalIgnoreCase);
                foreach (var header in response.Headers.Concat(response.Content.Headers))
                {
                    headers[header.Key] = [.. header.Value];
                }

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                var body = readBody && IsText(mediaType) ? await ReadText(response, timeout.Token) : null;

                return new(status, uri, headers, mediaType, body, cookies);
            }
        }
        catch (Exception exp) when (exp is HttpRequestException or TaskCanceledException or IOException && cancellationToken.IsCancellationRequested is false)
        {
            logger.LogInformation("{Url} didn't answer the scan: {Reason}", uri, exp.Message);
            return null;
        }
    }

    private static bool IsText(string? mediaType) => mediaType is not null
        && mediaType is not "text/event-stream"
        && (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || mediaType.Contains("json", StringComparison.OrdinalIgnoreCase) || mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase));

    /// <summary>The body as text, up to <see cref="MaxBodyLength"/> characters' worth of bytes, in the charset it names.</summary>
    private static async Task<string> ReadText(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var buffer = new byte[MaxBodyLength];
        var length = 0;

        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken);
            if (read is 0)
                break;
            length += read;
        }

        var encoding = Encoding.UTF8;
        if (response.Content.Headers.ContentType?.CharSet is { Length: > 0 } charSet)
        {
            try
            {
                encoding = Encoding.GetEncoding(charSet.Trim('"'));
            }
            catch (ArgumentException)
            {
                // An unknown charset: UTF-8, as a browser would most likely guess.
            }
        }

        return encoding.GetString(buffer, 0, length);
    }
}

/// <summary>
/// An answer of the app, read in full before its response is disposed: its status, the address that answered after the
/// redirects followed, the response's and its content's headers by name in any case, its media type and text body, and the
/// Set-Cookie headers of every step, redirects included.
/// </summary>
public sealed record ScanResponse(int Status, Uri Url, IReadOnlyDictionary<string, string[]> Headers, string? MediaType, string? Body, IReadOnlyList<string> Cookies)
{
    public string? Header(string name) => Headers.TryGetValue(name, out var values) && values.Length > 0 ? string.Join(", ", values) : null;

    public bool IsOk => Status is (int)HttpStatusCode.OK;

    public bool IsHtml => MediaType is "text/html" or "application/xhtml+xml";

    public bool IsJson => MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) is true;
}
