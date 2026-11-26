namespace Boilerplate.Server.Shared.Infrastructure.HealthChecks;

/// <summary>
/// What a health check talks to: the outside service behind it, such as <c>Twilio</c> behind "SMS", the model for an AI
/// check, and the host it reaches. Names only: never a credential, key, connection string, user name, path or query.
/// </summary>
/// <param name="Provider">A short name of the service, such as "Twilio", "OpenAI" or "Amazon S3".</param>
/// <param name="Host">The host name alone, such as "smtp.example.com" (See <see cref="HostOf(string?)"/>); null when it says nothing more than <paramref name="Provider"/>.</param>
/// <param name="Model">The model an AI check uses, such as "gpt-4.1-mini".</param>
public record HealthCheckDependency(string Provider, string? Host = null, string? Model = null)
{
    /// <summary>
    /// The host of <paramref name="address"/>, an absolute url or a bare host name, and nothing else of it: a url's user
    /// name, password, port, path and query are left out. Null for nothing, or for something that isn't one.
    /// </summary>
    public static string? HostOf(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        if (Uri.TryCreate(address, UriKind.Absolute, out var url) && string.IsNullOrEmpty(url.Host) is false)
            return url.Host;

        return Uri.CheckHostName(address.Trim()) is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6 ? address.Trim() : null;
    }

    public static string? HostOf(Uri? address) => address?.IsAbsoluteUri is true && string.IsNullOrEmpty(address.Host) is false ? address.Host : null;
}

/// <summary>
/// What /healthz reports each check talks to (See HealthReportEntryDto.Provider), by registration name, so a monitor shows
/// what lies behind a part, such as the email provider behind "Email". The template's checks name theirs where they are
/// registered; name yours next to its registration, e.g.
/// <c>.TalksTo("paymentGateway", "Stripe", "api.stripe.com")</c>. A check named nowhere, and the stand-in of one whose
/// settings are missing, report nothing.
/// </summary>
public class HealthCheckDependencies : Dictionary<string, HealthCheckDependency>;
