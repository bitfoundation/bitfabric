namespace Bit.Fabric.Server.Api;

/// <summary>How this server signs projects' licenses, which bit CLI checks with the public half of the same key.</summary>
public partial class LicensingOptions
{
    /// <summary>
    /// The ECDSA P-256 private key licenses are signed with: base64 of its PKCS#8 DER. Required outside Development, where
    /// a deployment sets it from the GitHub environment's <c>SERVERAPI__LICENSING__PRIVATEKEY</c> secret. Empty in
    /// Development makes a new key at startup and logs its public half, so licenses issued locally check only against it.
    /// </summary>
    public string? PrivateKey { get; set; }

    /// <summary>How long a new project's trial lasts.</summary>
    [Range(1, 365)]
    public int TrialDays { get; set; } = 30;

    /// <summary>How long a free license lasts from the day it is issued.</summary>
    [Range(1, 3650)]
    public int FreeDays { get; set; } = 365;

    /// <summary>How long bit CLI can read a license with the one-time code it waits with.</summary>
    public TimeSpan ClaimCodeLifetime { get; set; } = TimeSpan.FromMinutes(15);
}
