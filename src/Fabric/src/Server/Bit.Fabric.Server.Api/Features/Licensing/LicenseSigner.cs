using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Bit.Fabric.Server.Api.Features.Licensing;

/// <summary>
/// Signs projects' licenses: <c>bfl1.&lt;payload&gt;.&lt;signature&gt;</c>, both base64url without padding. The payload is
/// UTF-8 JSON; the signature is ECDSA P-256 with SHA-256 over the ASCII bytes of <c>bfl1.&lt;payload&gt;</c>, in IEEE P1363
/// form (r||s). bit CLI checks it with the key's public half, which it carries.
/// </summary>
public sealed partial class LicenseSigner : IHostedService, IDisposable
{
    public const string Prefix = "bfl1";

    public const string TrialPlan = "trial";

    public const string FreePlan = "free";

    private readonly ECDsa key;

    public LicenseSigner(ServerApiSettings settings, IHostEnvironment environment, ILogger<LicenseSigner> logger)
    {
        if (string.IsNullOrWhiteSpace(settings.Licensing.PrivateKey) is false)
        {
            key = ImportPrivateKey(Convert.FromBase64String(settings.Licensing.PrivateKey));
            PublicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        }
        else if (environment.IsDevelopment())
        {
            key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            PublicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
            LogEphemeralKey(logger, PublicKey);
        }
        else
        {
            throw new InvalidOperationException("Licensing:PrivateKey is not set. Set it from the SERVERAPI__LICENSING__PRIVATEKEY secret.");
        }
    }

    /// <summary>
    /// The private key's DER in either of its two standard encodings: PKCS#8, or SEC1, which <c>openssl pkey -outform DER</c>
    /// writes for an EC key.
    /// </summary>
    public static ECDsa ImportPrivateKey(byte[] der)
    {
        var key = ECDsa.Create();

        try
        {
            key.ImportPkcs8PrivateKey(der, out _);
        }
        catch (CryptographicException)
        {
            key.ImportECPrivateKey(der, out _);
        }

        return key;
    }

    /// <summary>The public half: base64 of its SubjectPublicKeyInfo DER, as bit CLI carries it.</summary>
    public string PublicKey { get; }

    public string Sign(Guid project, string name, string commit, string plan, string holder, DateTimeOffset issued, DateTimeOffset expires)
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
            json.WriteString("issued", Iso(issued));
            json.WriteString("expires", Iso(expires));
            json.WriteEndObject();
        }

        var signed = $"{Prefix}.{Base64Url.EncodeToString(payload.WrittenSpan)}";
        var signature = key.SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{signed}.{Base64Url.EncodeToString(signature)}";
    }

    /// <summary>To the second, the way a license states its dates.</summary>
    public static DateTimeOffset ToSeconds(DateTimeOffset at) => new(at.UtcDateTime.Ticks - at.UtcDateTime.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);

    private static string Iso(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    // Resolving it as a hosted service makes the key, and logs a development one, when the server starts.
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => key.Dispose();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Licensing:PrivateKey is empty, so licenses are signed with a key made for this run. Its public half: {PublicKey}")]
    private static partial void LogEphemeralKey(ILogger logger, string publicKey);
}
