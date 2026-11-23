namespace Bit.Cli.Licensing;

/// <summary>
/// The public halves of the keys bit Fabric signs licenses with: base64 of their SubjectPublicKeyInfo DER. Only these are
/// accepted, and nothing outside the code adds one, since a key that could be added could sign anyone's license.
/// </summary>
public static class LicenseKeys
{
    /// <summary>bit Fabric's own, at bitfabric.co.</summary>
    public const string Production = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE52X5Tem4wniBLV9mPAuuj5vttCgR2qShHmKl0sWtI+7GTJhUxdqf7Klx92tuIpMy0UwpwemSSTxBIhwu/PW1Hg==";

    /// <summary>bit Fabric's Test environment, which a Debug build of bit accepts as well.</summary>
    public const string Test = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE4+rtNUw5ewIGdfvMule5cdRWbPplh6QapBnGyE2pgA4ix51CspovacBFn5uDcBN0tBXf0efFs8T4LqGtlaP9hA==";

#if DEBUG
    public static IReadOnlyList<string> Accepted { get; } = [Production, Test];
#else
    public static IReadOnlyList<string> Accepted { get; } = [Production];
#endif
}
