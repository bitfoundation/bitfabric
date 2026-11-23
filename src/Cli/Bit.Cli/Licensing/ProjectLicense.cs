using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bit.Cli.Licensing;

/// <summary>
/// A project's license as bit Fabric issues it and bit.json keeps it: <c>bfl1.&lt;payload&gt;.&lt;signature&gt;</c>, both
/// base64url without padding. The payload is UTF-8 JSON. The signature is ECDSA P-256 with SHA-256 over the ASCII bytes of
/// <c>bfl1.&lt;payload&gt;</c>, in IEEE P1363 form (r||s), checked with the public keys bit carries (See LicenseKeys). That
/// is all there is to it: nothing about the computer is read, and nothing is sent anywhere to check it.
/// </summary>
public sealed partial record ProjectLicense
{
    public const string Prefix = "bfl1";

    public const string TrialPlan = "trial";

    public const string FreePlan = "free";

    /// <summary>The project's id, which bit.json keeps as <c>projectId</c>.</summary>
    public required Guid Project { get; init; }

    /// <summary>The project's name when it was issued, only shown.</summary>
    public string Name { get; init; } = "";

    /// <summary>The full sha of the project's first git commit.</summary>
    public required string Commit { get; init; }

    /// <summary><see cref="TrialPlan"/> or <see cref="FreePlan"/>.</summary>
    public required string Plan { get; init; }

    /// <summary>Empty for a trial; the account's name or email for a free license.</summary>
    public string Holder { get; init; } = "";

    public required DateTimeOffset Issued { get; init; }

    public required DateTimeOffset Expires { get; init; }

    public bool IsTrial => Plan is TrialPlan;

    /// <summary>The license in <paramref name="document"/>, or null when it isn't one or none of <paramref name="publicKeys"/> signed it.</summary>
    public static ProjectLicense? Verify(string? document, IEnumerable<string> publicKeys)
    {
        if (document?.Trim().Split('.') is not [Prefix, var payload, var signature])
            return null;

        byte[] payloadBytes, signatureBytes;

        try
        {
            payloadBytes = Base64Url.DecodeFromChars(payload);
            signatureBytes = Base64Url.DecodeFromChars(signature);
        }
        catch (FormatException)
        {
            return null;
        }

        var signed = Encoding.ASCII.GetBytes($"{Prefix}.{payload}");

        return publicKeys.Any(key => IsSignedWith(key, signed, signatureBytes)) ? Read(payloadBytes) : null;
    }

    private static bool IsSignedWith(string publicKey, byte[] signed, byte[] signature)
    {
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);

        return key.VerifyData(signed, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static ProjectLicense? Read(byte[] payload)
    {
        try
        {
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;

            string? Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String ? value.GetString() : null;
            DateTimeOffset? Date(string name) => root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String && value.TryGetDateTimeOffset(out var date) ? date : null;

            if (root.TryGetProperty("v", out var version) is false || version.ValueKind is not JsonValueKind.Number || version.GetInt32() != 1
                || Guid.TryParse(Text("project"), out var project) is false
                || Text("commit") is not { } commit || IsCommit(commit) is false
                || Text("plan") is not ({ } plan and (TrialPlan or FreePlan))
                || Date("issued") is not { } issued || Date("expires") is not { } expires)
                return null;

            return new() { Project = project, Name = Text("name") ?? "", Commit = commit, Plan = plan, Holder = Text("holder") ?? "", Issued = issued, Expires = expires };
        }
        catch (Exception exp) when (exp is JsonException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>A full sha of git's, SHA-1 or SHA-256.</summary>
    public static bool IsCommit(string? value) => value is not null && CommitRegex().IsMatch(value);

    [GeneratedRegex("^([0-9a-f]{40}|[0-9a-f]{64})$")]
    private static partial Regex CommitRegex();
}
