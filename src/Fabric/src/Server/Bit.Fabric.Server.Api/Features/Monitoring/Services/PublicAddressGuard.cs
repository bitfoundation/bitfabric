// [mirror] which addresses count as the public internet - keep in sync with:
// - src/Server/Bit.Fabric.Server.Lighthouse/src/guard.ts (isPublic)

using System.Net;
using System.Net.Sockets;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// Keeps bit Fabric's requests to customers' apps on the public internet (bitfabric-internal/specs/monitoring.md, the SSRF
/// risk of the plan). The check runs where the connection is made, on the addresses the name resolves to at that moment,
/// so a name that resolves to a public address once and to the server's own network the next time can't slip past it,
/// and neither can a redirect, which the client doesn't follow anyway.
/// </summary>
public static class PublicAddressGuard
{
    /// <summary>A unicast address anyone on the internet could reach: none of the private, shared or reserved ranges.</summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            return IsPublic(address.MapToIPv4());

        if (address.AddressFamily is AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6None)
                || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal || address.IsIPv6Multicast)
                return false;

            var bytes = address.GetAddressBytes();

            // 64:ff9b::/96 (NAT64) carries an IPv4 address in its last four bytes; judge that one.
            if (bytes[0] is 0x00 && bytes[1] is 0x64 && bytes[2] is 0xff && bytes[3] is 0x9b && bytes[4..12].All(b => b is 0))
                return IsPublic(new IPAddress(bytes[12..]));

            // 2001:db8::/32 is for documentation only.
            return (bytes[0] is 0x20 && bytes[1] is 0x01 && bytes[2] is 0x0d && bytes[3] is 0xb8) is false;
        }

        if (address.AddressFamily is not AddressFamily.InterNetwork)
            return false;

        var o = address.GetAddressBytes();

        return (o[0] is 0 or 10 or 127                                // "this network", private, loopback
                || (o[0] is 100 && o[1] >= 64 && o[1] <= 127)         // shared address space (carrier-grade NAT)
                || (o[0] is 169 && o[1] is 254)                       // link-local, where cloud metadata services live
                || (o[0] is 172 && o[1] >= 16 && o[1] <= 31)          // private
                || (o[0] is 192 && o[1] is 0 && o[2] is 0 or 2)       // IETF protocol assignments, documentation
                || (o[0] is 192 && o[1] is 168)                       // private
                || (o[0] is 198 && o[1] is 18 or 19)                  // benchmarking
                || (o[0] is 198 && o[1] is 51 && o[2] is 100)         // documentation
                || (o[0] is 203 && o[1] is 0 && o[2] is 113)          // documentation
                || o[0] >= 224) is false;                             // multicast, reserved, broadcast
    }

    /// <summary>
    /// Whether <paramref name="host"/> resolves to at least one public address, for what connects to a customer's app
    /// elsewhere: the browser scans, whose Chrome runs in bit Fabric's Lighthouse service behind a guard of its own.
    /// </summary>
    public static async Task<bool> IsPublicHost(string host, CancellationToken cancellationToken)
    {
        try
        {
            var addresses = IPAddress.TryParse(host.Trim('[', ']'), out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(host, cancellationToken);

            return addresses.Any(IsPublic);
        }
        catch (SocketException)
        {
            return false;
        }
    }

    /// <summary>
    /// The connection step of the monitoring http client: resolves the name, keeps the public addresses (and loopback
    /// and private ones only when <paramref name="allowLocalAddresses"/>), and connects to the first that answers.
    /// </summary>
    public static ValueTask<Stream> Connect(SocketsHttpConnectionContext context, bool allowLocalAddresses, CancellationToken cancellationToken)
        => Connect(context.DnsEndPoint.Host, context.DnsEndPoint.Port, allowLocalAddresses, cancellationToken);

    /// <summary>
    /// The same connection outside an http client, for what reads a customer's host below http: the TLS handshake the
    /// domain and certificate scan reads the certificate from (See TlsCertificateInspector).
    /// </summary>
    public static async ValueTask<Stream> Connect(string host, int port, bool allowLocalAddresses, CancellationToken cancellationToken)
    {

        var addresses = IPAddress.TryParse(host.Trim('[', ']'), out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        var allowed = addresses.Where(address => allowLocalAddresses || IsPublic(address)).ToArray();

        if (allowed.Length is 0)
            throw new HttpRequestException(HttpRequestError.ConnectionError, $"{host} doesn't resolve to a public address.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        try
        {
            await socket.ConnectAsync(allowed, port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
