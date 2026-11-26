using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bit.Cli.Tests.Infrastructure;

/// <summary>
/// bit Fabric as bit sees it: the trial and claim endpoints, and a key pair of its own the licenses are signed with, which
/// a test makes bit accept in place of bit Fabric's (See LicenseOptions.PublicKeys). Offline until <see cref="Online"/>.
/// </summary>
public sealed class FakeBitFabric : HttpMessageHandler
{
    private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public FakeBitFabric()
    {
        PublicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
    }

    public string PublicKey { get; }

    public bool Online { get; set; }

    /// <summary>When the trials it gives start; now when null.</summary>
    public DateTimeOffset? TrialsIssued { get; set; }

    /// <summary>The licenses the license page issued, by the one-time code bit waits with.</summary>
    public ConcurrentDictionary<string, string> Issued { get; } = new();

    private string? nextClaim;

    /// <summary>What the next claim gets, whatever its code: the license page issuing it while bit waits.</summary>
    public string? NextClaim { get => nextClaim; set => nextClaim = value; }

    /// <summary>The bodies of the trial requests, and the codes asked for, in order.</summary>
    public ConcurrentQueue<string> Requests { get; } = new();

    /// <summary>
    /// The trials it gave, by project id. As bit Fabric does, a project that has one gets the same one again, whatever
    /// first commit it is asked for with; a test puts one here for a project someone else made.
    /// </summary>
    public ConcurrentDictionary<Guid, string> Trials { get; } = new();

    public string Sign(Guid project, string commit, string plan, DateTimeOffset issued, DateTimeOffset expires, string name = "Contoso", string holder = "")
    {
        var payload = new ArrayBufferWriter<byte>();

        using (var json = new Utf8JsonWriter(payload))
        {
            json.WriteStartObject();
            json.WriteNumber("v", 1);
            json.WriteString("project", project.ToString("D"));
            json.WriteString("name", name);
            json.WriteString("commit", commit);
            json.WriteString("plan", plan);
            json.WriteString("holder", holder);
            json.WriteString("issued", issued.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            json.WriteString("expires", expires.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            json.WriteEndObject();
        }

        var signed = $"bfl1.{Base64Url.EncodeToString(payload.WrittenSpan)}";
        var signature = key.SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{signed}.{Base64Url.EncodeToString(signature)}";
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Online is false)
            throw new HttpRequestException("No such host is known. (api.bitfabric.co:443)");

        var path = request.RequestUri!.AbsolutePath;

        if (request.Method == HttpMethod.Post && path is "/api/v1/License/Trial")
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Requests.Enqueue(body);

            using var json = JsonDocument.Parse(body);
            var issued = TrialsIssued ?? DateTimeOffset.UtcNow;
            var project = Guid.Parse(json.RootElement.GetProperty("project").GetString()!);
            var license = Trials.GetOrAdd(project, _ => Sign(project, json.RootElement.GetProperty("commit").GetString()!, "trial", issued, issued.AddDays(30), json.RootElement.GetProperty("name").GetString()!));

            return Answer(license);
        }

        if (request.Method == HttpMethod.Get && path is "/api/v1/License/Claim")
        {
            var code = Uri.UnescapeDataString(request.RequestUri.Query.TrimStart('?').Split('&').Single(value => value.StartsWith("code=", StringComparison.Ordinal))["code=".Length..]);
            Requests.Enqueue($"claim {code}");

            if (Interlocked.Exchange(ref nextClaim, null) is { } next)
                return Answer(next);

            return Answer(Issued.TryRemove(code, out var license) ? license : null);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Answer(string? license) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new Dictionary<string, string?> { ["license"] = license }), Encoding.UTF8, "application/json")
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            key.Dispose();
        }

        base.Dispose(disposing);
    }
}
