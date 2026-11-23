using System.Net;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;

/// <summary>
/// The domain and certificate scan (bitfabric-internal/specs/scanners.md, Decisions, 2026-11-12 and 2026-11-13): the TLS
/// certificate of the app's host and of its server's, read in a TLS handshake (days left, a chain browsers trust, the
/// name it was issued for), and the registration of their domains, read over RDAP. Under 14 days is urgent and under 30
/// important, since certificates last 64 days from February 2027. A domain whose registry has no RDAP gives no finding,
/// never a false alarm.
/// </summary>
public partial class DomainAndCertificateScanner(ICertificateInspector certificates, IDomainRegistrationLookup registrations, TimeProvider timeProvider,
                                                 ILogger<DomainAndCertificateScanner> logger) : IProjectScanner
{
    public const int UrgentDays = 14;

    public const int ImportantDays = 30;

    public ProjectScanKind Kind => ProjectScanKind.DomainAndCertificate;

    public async Task<ProjectScanResult> Scan(Project project, ProjectScan scan, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        Uri[] addresses = [.. new[] { project.Address, project.ServerAddress }
            .Where(address => address is not null)
            .Select(address => new Uri(address!))
            .DistinctBy(address => (address.Host, address.Port))];

        ProjectScanResult result = new();
        List<DateTimeOffset> expiries = [];
        var webCertificateRead = false;

        foreach (var address in addresses.Where(address => address.Scheme == Uri.UriSchemeHttps))
        {
            var report = await certificates.Inspect(address.IdnHost, address.Port, cancellationToken);

            if (report is null)
                continue; // That host's TLS can't be read: the other scans and its health check say why.

            webCertificateRead |= address == addresses[0];
            expiries.Add(report.NotAfter);

            var host = address.IdnHost;
            var days = DaysLeft(report.NotAfter, now);

            if (report.NameMatches is false)
            {
                result.Findings.Add(new(ScanFindings.CertificateNameMismatch, ScanFindingSeverity.Urgent, [host], [host]));
            }

            if (report.ChainTrusted is false)
            {
                result.Findings.Add(new(ScanFindings.CertificateChainInvalid, ScanFindingSeverity.Urgent, [host], [host]));
            }

            if (days < 0)
            {
                result.Findings.Add(new(ScanFindings.CertificateExpired, ScanFindingSeverity.Urgent, [host], [host]));
            }
            else if (SeverityOf(days) is { } severity)
            {
                result.Findings.Add(new(ScanFindings.CertificateExpiresSoon, severity, [host, $"{days}"], [host]));
            }
        }

        // An app on plain http (Development) has no certificate to read; on https, the home page's host must answer.
        if (addresses[0].Scheme == Uri.UriSchemeHttps && webCertificateRead is false)
            return ProjectScanResult.Unreachable();

        HashSet<string> domainsRead = [];

        foreach (var host in addresses.Select(address => address.IdnHost).Distinct())
        {
            if (IPAddress.TryParse(host, out _) || host.Contains('.', StringComparison.Ordinal) is false)
                continue;

            try
            {
                if (await registrations.Find(host, cancellationToken) is not { } registration || domainsRead.Add(registration.Domain) is false)
                    continue;

                expiries.Add(registration.ExpiresAt);
                var days = DaysLeft(registration.ExpiresAt, now);

                if (days < 0)
                {
                    result.Findings.Add(new(ScanFindings.DomainExpired, ScanFindingSeverity.Urgent, [registration.Domain], [registration.Domain]));
                }
                else if (SeverityOf(days) is { } severity)
                {
                    result.Findings.Add(new(ScanFindings.DomainExpiresSoon, severity, [registration.Domain, $"{days}"], [registration.Domain]));
                }
            }
            catch (Exception exp) when (exp is not OperationCanceledException)
            {
                // A registry that can't be read is no finding, and the certificates still count.
                logger.LogWarning(exp, "Reading the domain registration of {Host} failed.", host);
            }
        }

        result.ExpiresAt = expiries.Count > 0 ? expiries.Min() : null;
        result.Findings.Sort((a, b) => a.Severity.CompareTo(b.Severity));
        result.Judge(goodFrom: 0, poorBelow: 0);

        return result;
    }

    /// <summary>Whole days left, rounded down: 13 days and 23 hours is 13.</summary>
    private static int DaysLeft(DateTimeOffset until, DateTimeOffset now) => (int)Math.Floor((until - now).TotalDays);

    private static ScanFindingSeverity? SeverityOf(int days) => days switch
    {
        < UrgentDays => ScanFindingSeverity.Urgent,
        < ImportantDays => ScanFindingSeverity.Important,
        _ => null
    };
}

/// <summary>
/// What a TLS handshake shows of a host's certificate: when it ends, whether it was issued for the host's name, and whether
/// it chains to an authority the server trusts (an expired one still can; its end tells that).
/// </summary>
public sealed record CertificateReport(DateTimeOffset NotAfter, bool NameMatches, bool ChainTrusted);

/// <summary>Reads a host's certificate; the tests replace it, since their customer apps aren't on the network.</summary>
public interface ICertificateInspector
{
    /// <summary>Null when no TLS handshake could be made.</summary>
    Task<CertificateReport?> Inspect(string host, int port, CancellationToken cancellationToken);
}

/// <summary>The registered name a host falls under, such as <c>example.co.uk</c>, and when its registration ends.</summary>
public sealed record DomainRegistration(string Domain, DateTimeOffset ExpiresAt);

/// <summary>Reads when a host's domain registration ends; the tests replace it, as they do the certificates.</summary>
public interface IDomainRegistrationLookup
{
    /// <summary>Null when the registry has no RDAP, doesn't know the name, or doesn't say when it ends.</summary>
    Task<DomainRegistration?> Find(string host, CancellationToken cancellationToken);
}
