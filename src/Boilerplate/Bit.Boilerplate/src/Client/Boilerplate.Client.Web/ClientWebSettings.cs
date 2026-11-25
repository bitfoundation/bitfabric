//+:cnd:noEmit
using Boilerplate.Client.Core;
using Microsoft.Extensions.Options;

namespace Boilerplate.Client.Web;

public class ClientWebSettings : ClientCoreSettings
{
    //#if (notification == true)
    [ValidateObjectMembers]
    public AdsPushVapidOptions? AdsPushVapid { get; set; }
    //#endif

    /// <summary>
    /// The script of the app's status page on bit Fabric (https://bitfabric.co), which shows a bar at the top of the web
    /// app only while something is wrong. Server.Web's App.razor loads it, and the Content-Security-Policy allows its
    /// origin (See ContentSecurityPolicy.BuildCspString). Empty loads nothing.
    /// </summary>
    public string? StatusPageScriptUrl { get; set; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var validationResults = base.Validate(validationContext).ToList();

        //#if (notification == true)
        if (AdsPushVapid is not null)
        {
            if (AppEnvironment.IsDevelopment() is false && AdsPushVapid.PublicKey is "BDSNUvuIISD8NQVByQANEtZ2foKaENIcIGUxsiQs9kDz11fQik8c9WeiMwUHs3iTgNNH4nvXioNQIEsn4OAjTKc")
            {
                validationResults.Add(new ValidationResult("Please set your own AdsPushVapid.PublicKey in Client.Web's appsettings.json"));
            }
        }
        //#endif

        if (string.IsNullOrWhiteSpace(StatusPageScriptUrl) is false
            && (Uri.TryCreate(StatusPageScriptUrl, UriKind.Absolute, out var statusPageScript) is false || statusPageScript.Scheme is not ("https" or "http")))
        {
            validationResults.Add(new ValidationResult($"{nameof(StatusPageScriptUrl)} must be the absolute address of the status page's script, such as https://api.bitfabric.co/api/v1/PublicStatus/Embed/<page>.", [nameof(StatusPageScriptUrl)]));
        }

        return validationResults;
    }
}

[OptionsValidator]
public partial class ClientWebSettingsValidator : IValidateOptions<ClientWebSettings>
{
}
