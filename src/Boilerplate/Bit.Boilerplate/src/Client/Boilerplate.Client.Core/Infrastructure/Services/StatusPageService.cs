namespace Boilerplate.Client.Core.Infrastructure.Services;

/// <summary>
/// Reads the app's status page (See <see cref="ClientCoreSettings.StatusPageSummaryUrl"/>) for <see cref="StatusPageNotice"/>
/// and for BevTools' Status tab (See AppStatusPagePanel). The request goes out with a plain <see cref="HttpClient"/>: the
/// status page is someone else's server, so none of the app's handlers - its access token included - go with it. A
/// summary that doesn't come back, or comes back as anything but a summary, is logged as an error and shows nothing.
/// </summary>
public class StatusPageService(ClientCoreSettings settings, TimeProvider timeProvider, ILogger<StatusPageService> logger)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public bool IsEnabled => string.IsNullOrWhiteSpace(settings.StatusPageSummaryUrl) is false;

    /// <summary>The summary's address, as the app's settings give it.</summary>
    public string? SummaryUrl => settings.StatusPageSummaryUrl;

    /// <summary>When the status page was last asked, so a notice rebuilt with the layout (a new culture, for one) needn't ask again.</summary>
    public DateTimeOffset? LastCheckedAt { get; private set; }

    /// <summary>What that ask found.</summary>
    public StatusNotice? LastNotice { get; private set; }

    public async Task<StatusNotice?> GetNotice(CancellationToken cancellationToken)
    {
        if (IsEnabled is false)
            return null;

        LastCheckedAt = timeProvider.GetUtcNow();

        if (await Read(cancellationToken) is not { } read)
            return null;

        return LastNotice = StatusNotice.From(read.Summary, timeProvider.GetUtcNow());
    }

    /// <summary>Reads the summary once: what came back, or why nothing did. Null when <paramref name="cancellationToken"/> stopped it, or no address is set.</summary>
    public async Task<StatusPageRead?> Read(CancellationToken cancellationToken)
    {
        if (IsEnabled is false)
            return null;

        if (Uri.TryCreate(settings.StatusPageSummaryUrl, UriKind.Absolute, out var url) is false || url.Scheme is not ("https" or "http"))
        {
            logger.LogError("{Setting} isn't an absolute http(s) address: {StatusPageSummaryUrl}.", nameof(ClientCoreSettings.StatusPageSummaryUrl), settings.StatusPageSummaryUrl);
            return new(timeProvider.GetUtcNow(), null, $"{nameof(ClientCoreSettings.StatusPageSummaryUrl)} isn't an absolute http(s) address.");
        }

        try
        {
            using var httpClient = CreateHttpClient();
            httpClient.Timeout = Timeout;

            var summary = await httpClient.GetFromJsonAsync(url, StatusPageSummaryJsonContext.Default.StatusPageSummary, cancellationToken);

            return new(timeProvider.GetUtcNow(), summary, summary is null ? "The answer was empty." : null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exp)
        {
            logger.LogError(exp, "Couldn't read the status page's summary at {StatusPageSummaryUrl}.", url);
            return new(timeProvider.GetUtcNow(), null, $"{exp.GetType().Name}: {exp.Message}");
        }
    }

    protected virtual HttpClient CreateHttpClient() => new();
}

/// <param name="At">When the answer came back, or the read gave up.</param>
/// <param name="Summary">The summary, when one came back.</param>
/// <param name="Error">Why none did.</param>
public record StatusPageRead(DateTimeOffset At, StatusPageSummary? Summary, string? Error);
