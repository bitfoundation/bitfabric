namespace Bit.Cli.Licensing;

/// <summary>
/// What the license check takes from outside: the keys it accepts, the clock, and bit Fabric's addresses. Nothing here is
/// read from a setting or an environment variable; only the tests change it, through InternalsVisibleTo.
/// </summary>
public sealed class LicenseOptions
{
    public const string ApiAddress = "https://api.bitfabric.co/";

    public const string SiteAddress = "https://bitfabric.co/";

    internal IReadOnlyList<string> PublicKeys { get; set; } = LicenseKeys.Accepted;

    internal TimeProvider Clock { get; set; } = TimeProvider.System;

    /// <summary>How bit reaches bit Fabric; null for the network.</summary>
    internal HttpMessageHandler? Http { get; set; }

    /// <summary>How often a waiting bit asks whether the license was issued.</summary>
    internal TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>How long a waiting bit waits for the license.</summary>
    internal TimeSpan WaitLimit { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>False leaves the check out before commands, for the tests of everything else.</summary>
    internal bool ChecksCommands { get; set; } = true;
}
