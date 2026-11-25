namespace Boilerplate.Client.Core.Infrastructure.Services;

/// <summary>
/// Reads the app's status page (See <see cref="ClientCoreSettings.StatusPageSummaryUrl"/>) for <see cref="StatusPageNotice"/>.
/// The request goes out with a plain <see cref="HttpClient"/>: the status page is someone else's server, so none of the
/// app's handlers - its access token included - go with it. A summary that doesn't come back, or comes back as anything
/// but a summary, is logged as an error and shows nothing.
/// </summary>
public class StatusPageService(ClientCoreSettings settings, TimeProvider timeProvider, ILogger<StatusPageService> logger)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public bool IsEnabled => string.IsNullOrWhiteSpace(settings.StatusPageSummaryUrl) is false;

    /// <summary>When the status page was last asked, so a notice rebuilt with the layout (a new culture, for one) needn't ask again.</summary>
    public DateTimeOffset? LastCheckedAt { get; private set; }

    /// <summary>What that ask found.</summary>
    public StatusNotice? LastNotice { get; private set; }

    public async Task<StatusNotice?> GetNotice(CancellationToken cancellationToken)
    {
        if (IsEnabled is false)
            return null;

        LastCheckedAt = timeProvider.GetUtcNow();

        if (Uri.TryCreate(settings.StatusPageSummaryUrl, UriKind.Absolute, out var url) is false || url.Scheme is not ("https" or "http"))
        {
            logger.LogError("{Setting} isn't an absolute http(s) address: {StatusPageSummaryUrl}.", nameof(ClientCoreSettings.StatusPageSummaryUrl), settings.StatusPageSummaryUrl);
            return LastNotice = null;
        }

        try
        {
            using var httpClient = CreateHttpClient();
            httpClient.Timeout = Timeout;

            var summary = await httpClient.GetFromJsonAsync(url, StatusPageSummaryJsonContext.Default.StatusPageSummary, cancellationToken);

            return LastNotice = StatusNotice.From(summary, timeProvider.GetUtcNow());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exp)
        {
            logger.LogError(exp, "Couldn't read the status page's summary at {StatusPageSummaryUrl}.", url);
            return LastNotice = null;
        }
    }

    protected virtual HttpClient CreateHttpClient() => new();
}
