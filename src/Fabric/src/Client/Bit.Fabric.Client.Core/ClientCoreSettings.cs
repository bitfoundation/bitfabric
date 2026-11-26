using Microsoft.Extensions.Options;

namespace Bit.Fabric.Client.Core;

public partial class ClientCoreSettings : SharedSettings
{
    /// <summary>
    /// If you're running Bit.Fabric.Server.Web project, then you can also use relative urls such as / for Blazor Server and WebAssembly
    /// </summary>
    [Required]
    public string ServerAddress { get; set; } = default!;



    /// <summary>
    /// When the Blazor Hybrid app sends a request to the API server, and the API server and web app are hosted on different URLs,
    /// the origin of the generated links (e.g., email confirmation links) will depend on `WebAppUrl` value.
    /// The config is relevant for Blazor Hybrid apps only.
    /// </summary>
    public Uri? WebAppUrl { get; set; }

    /// <summary>
    /// The installer of this environment's Windows app, which the CD uploads with its updates (See cd-template.yml), for the
    /// home page's download link: Production's in appsettings.Production.json, and Test's in appsettings.Test.json and in
    /// Development.
    /// </summary>
    public string? WindowsDownloadUrl { get; set; }

    /// <summary>
    /// The <c>summary.json</c> of the app's status page, such as bit Fabric's
    /// https://api.bitfabric.co/api/v1/PublicStatus/Summary/{page} or any Statuspage-compatible one. A few seconds after
    /// the app opens, it asks once and tells its users about an incident or a planned maintenance (See StatusPageNotice).
    /// Empty, as it ships, asks nothing.
    /// </summary>
    public string? StatusPageSummaryUrl { get; set; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var validationResults = base.Validate(validationContext).ToList();


        return validationResults;
    }
}

[OptionsValidator]
public partial class ClientCoreSettingsValidator : IValidateOptions<ClientCoreSettings>
{
}
