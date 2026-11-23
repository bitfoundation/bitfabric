using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;

/// <summary>
/// A TLS handshake with a customer's host, through the same public-address guard as every request bit Fabric makes to
/// one, that accepts whatever certificate it is shown so it can say what is wrong with it rather than fail.
/// </summary>
public partial class TlsCertificateInspector(ServerApiSettings settings, ILogger<TlsCertificateInspector> logger) : ICertificateInspector
{
    public async Task<CertificateReport?> Inspect(string host, int port, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.Monitoring.ScanRequestTimeout);

        try
        {
            var stream = await PublicAddressGuard.Connect(host, port, settings.Monitoring.AllowLocalAddresses, timeout.Token);

            await using var tls = new SslStream(stream, leaveInnerStreamOpen: false);

            DateTimeOffset? notAfter = null;
            var nameMatches = false;
            var chainTrusted = false;

            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
                {
                    if (certificate is not null)
                    {
                        var read = certificate as X509Certificate2 ?? X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
                        notAfter = new DateTimeOffset(read.NotAfter.ToUniversalTime(), TimeSpan.Zero);
                    }

                    nameMatches = errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch) is false;

                    // An expired certificate fails its chain too; its expiry is reported on its own.
                    chainTrusted = errors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors) is false
                                   || chain?.ChainStatus.All(status => status.Status is X509ChainStatusFlags.NoError or X509ChainStatusFlags.NotTimeValid) is true;

                    // Accepted, to be read: nothing is sent over this connection.
                    return true;
                }
            }, timeout.Token);

            return notAfter is { } expires ? new(expires, nameMatches, chainTrusted) : null;
        }
        catch (Exception exp) when (exp is IOException or AuthenticationException or HttpRequestException or System.Net.Sockets.SocketException
                                    || (exp is OperationCanceledException && cancellationToken.IsCancellationRequested is false))
        {
            logger.LogInformation("No TLS handshake with {Host}:{Port}: {Reason}", host, port, exp.Message);
            return null;
        }
    }
}
