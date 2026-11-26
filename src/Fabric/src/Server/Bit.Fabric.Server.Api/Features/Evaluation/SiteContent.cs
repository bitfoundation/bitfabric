namespace Bit.Fabric.Server.Api.Features.Evaluation;

/// <summary>
/// What the assistant and the evaluation agent read of bit Fabric: every public page as Markdown, from the web app's own
/// <c>/llms-full.txt</c> and <c>/{page}.md</c> (See Server.Web's PageMarkdown), and the license, from bitfoundation/bitfabric's
/// LICENSE.md. Each is kept an hour; one that can't be read is asked for again a minute later, and the assistant answers
/// without it meanwhile.
/// </summary>
public partial class SiteContent(IHttpClientFactory httpClientFactory, IFusionCache cache, ILogger<SiteContent> logger)
{
    public const string HttpClientName = nameof(SiteContent);

    public const string LicenseUrl = "https://raw.githubusercontent.com/bitfoundation/bitfabric/main/LICENSE.md";

    /// <summary>Every page of the site the web app at <paramref name="webAppUrl"/> serves, one after another; null when it can't be read.</summary>
    public Task<string?> All(Uri webAppUrl, CancellationToken cancellationToken)
        => Read(new Uri(webAppUrl, "/llms-full.txt"), cancellationToken);

    /// <summary>One page, by its address without a culture, such as <c>/features</c>; null when there is none.</summary>
    public Task<string?> Page(Uri webAppUrl, string page, CancellationToken cancellationToken)
    {
        page = $"/{page.Trim().Trim('/')}";

        // Only a page's own address, never a way out of the site.
        if (PagePath().IsMatch(page) is false)
            return Task.FromResult<string?>(null);

        return Read(new Uri(webAppUrl, page is "/" ? "/index.md" : $"{page}.md"), cancellationToken);
    }

    /// <summary>The license every project made with bit Fabric is under, as the public repository has it.</summary>
    public Task<string?> License(CancellationToken cancellationToken) => Read(new Uri(LicenseUrl), cancellationToken);

    private async Task<string?> Read(Uri url, CancellationToken cancellationToken)
    {
        return await cache.GetOrSetAsync<string?>($"SiteContent:{url}", async (context, ct) =>
        {
            try
            {
                using var client = httpClientFactory.CreateClient(HttpClientName);
                using var response = await client.GetAsync(url, ct);

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync(ct);

                logger.LogWarning("{Url} couldn't be read for the assistant: {StatusCode}.", url, (int)response.StatusCode);
            }
            catch (Exception exp) when (exp is HttpRequestException or TaskCanceledException && ct.IsCancellationRequested is false)
            {
                logger.LogWarning(exp, "{Url} couldn't be read for the assistant.", url);
            }

            context.Options.Duration = TimeSpan.FromMinutes(1);
            return null;
        }, options => options.SetDuration(TimeSpan.FromHours(1)), cancellationToken);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^/[a-z0-9\-/]*$")]
    private static partial System.Text.RegularExpressions.Regex PagePath();
}
