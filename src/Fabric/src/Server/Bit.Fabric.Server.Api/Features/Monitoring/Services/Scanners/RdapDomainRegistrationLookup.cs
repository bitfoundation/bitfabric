using System.Net;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;

/// <summary>
/// When a domain's registration ends, over RDAP (RFC 9224 and RFC 9083): IANA's bootstrap file names each TLD's RDAP
/// server, and the domain's object there has an <c>expiration</c> event. The registered name is found from the host by
/// asking for the longest name first, since a registry knows only the names registered with it, so no public suffix list
/// is needed. The bootstrap is kept a day and an answer 12 hours: a registration changes once a year.
/// </summary>
public partial class RdapDomainRegistrationLookup(IHttpClientFactory httpClientFactory, IFusionCache cache, ILogger<RdapDomainRegistrationLookup> logger) : IDomainRegistrationLookup
{
    /// <summary>Its own http client: RDAP servers are on the public internet, never a customer's, and some redirect.</summary>
    public const string ClientName = "Rdap";

    public const string BootstrapUrl = "https://data.iana.org/rdap/dns.json";

    private const string NotRegistered = "-";

    public async Task<DomainRegistration?> Find(string host, CancellationToken cancellationToken)
    {
        var labels = host.TrimEnd('.').ToLowerInvariant().Split('.');

        if (labels.Length < 2 || (await Bootstrap(cancellationToken)).TryGetValue(labels[^1], out var server) is false)
            return null; // A TLD without RDAP: nothing to say, rather than a false alarm.

        for (var take = labels.Length; take >= 2; take--)
        {
            var name = string.Join('.', labels[^take..]);

            var answer = await cache.GetOrSetAsync($"rdap-domain:{name}", async token => await Expiration(server, name, token) ?? NotRegistered,
                                                   TimeSpan.FromHours(12), token: cancellationToken);

            if (answer is NotRegistered)
                continue;

            return DateTimeOffset.TryParse(answer, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt) ? new(name, expiresAt) : null;
        }

        return null;
    }

    /// <summary>The <c>expiration</c> event of a domain's object, as text; null when the registry doesn't have the name.</summary>
    private async Task<string?> Expiration(string server, string name, CancellationToken cancellationToken)
    {
        var http = httpClientFactory.CreateClient(ClientName);

        using var response = await http.GetAsync($"{server.TrimEnd('/')}/domain/{name}", cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            return null;

        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        if (document.RootElement.TryGetProperty("events", out var events) && events.ValueKind is JsonValueKind.Array)
        {
            foreach (var item in events.EnumerateArray())
            {
                if (item.TryGetProperty("eventAction", out var action) && action.GetString() is "expiration"
                    && item.TryGetProperty("eventDate", out var date) && date.GetString() is { } eventDate)
                    return eventDate;
            }
        }

        // Registered, but its registry doesn't say until when, as some ccTLDs don't.
        logger.LogInformation("The RDAP object of {Domain} has no expiration event.", name);
        return "";
    }

    /// <summary>Each TLD's RDAP server, from IANA's bootstrap file.</summary>
    private async Task<Dictionary<string, string>> Bootstrap(CancellationToken cancellationToken)
    {
        var json = await cache.GetOrSetAsync("rdap-bootstrap:dns", async token =>
        {
            var http = httpClientFactory.CreateClient(ClientName);
            return await http.GetStringAsync(BootstrapUrl, token);
        }, TimeSpan.FromDays(1), token: cancellationToken);

        Dictionary<string, string> servers = [];

        using var document = JsonDocument.Parse(json);

        // "services": [ [ ["com", "net"], ["https://rdap.verisign.com/com/v1/"] ], ... ]
        foreach (var service in document.RootElement.GetProperty("services").EnumerateArray())
        {
            var urls = service[1].EnumerateArray().Select(url => url.GetString()).Where(url => url is not null).ToArray();
            var url = urls.FirstOrDefault(url => url!.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) ?? urls.FirstOrDefault();

            if (url is null)
                continue;

            foreach (var tld in service[0].EnumerateArray())
            {
                if (tld.GetString() is { } name)
                {
                    servers.TryAdd(name.ToLowerInvariant(), url);
                }
            }
        }

        return servers;
    }
}
