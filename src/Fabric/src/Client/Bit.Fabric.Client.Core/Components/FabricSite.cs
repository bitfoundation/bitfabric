namespace Bit.Fabric.Client.Core.Components;

/// <summary>
/// The company behind bitfabric.co (bitfabric-internal/specs/home-page.md, Decisions): the legal pages, the About
/// and Trust pages' company card and the home page's JSON-LD print it from here. Dutch law (BW 3:15d) asks a business's site to show its name, address, Chamber of
/// Commerce (KvK) number and VAT number where they are easy to find: the legal page, which every page links to. The nav panel
/// no longer shows them on every page. No legal form, such as B.V., is known, so none is
/// written.
/// </summary>
public static class FabricSite
{
    public const string LegalName = "BitPlatform";

    public const string StreetAddress = "Sixmastraat 15";

    public const string PostalCode = "8932 PA";

    public const string Locality = "Leeuwarden";

    public const string Country = "the Netherlands";

    /// <summary>ISO 3166-1 alpha-2, as schema.org's addressCountry takes it.</summary>
    public const string CountryCode = "NL";

    /// <summary>The address on one line.</summary>
    public const string Address = $"{StreetAddress}, {PostalCode} {Locality}, {Country}";

    /// <summary>The company's number in the Dutch Chamber of Commerce's register (Kamer van Koophandel).</summary>
    public const string KvkNumber = "89263723";

    public const string VatNumber = "NL004711376B75";

    /// <summary>The address the site gives for questions, the privacy notice's and security.txt's included.</summary>
    public const string Email = "info@bitplatform.dev";

    /// <summary>The team's organization on GitHub, where bit platform lives.</summary>
    public const string GitHubOrganizationUrl = "https://github.com/bitfoundation";
}
