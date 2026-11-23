using Bit.Fabric.Client.Core;
using Microsoft.Extensions.Options;

namespace Bit.Fabric.Client.Web;

public class ClientWebSettings : ClientCoreSettings
{
    [ValidateObjectMembers]
    public AdsPushVapidOptions? AdsPushVapid { get; set; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var validationResults = base.Validate(validationContext).ToList();

        if (AdsPushVapid is not null)
        {
            if (AppEnvironment.IsDevelopment() is false && AdsPushVapid.PublicKey is "BDSNUvuIISD8NQVByQANEtZ2foKaENIcIGUxsiQs9kDz11fQik8c9WeiMwUHs3iTgNNH4nvXioNQIEsn4OAjTKc")
            {
                validationResults.Add(new ValidationResult("Please set your own AdsPushVapid.PublicKey in Client.Web's appsettings.json"));
            }
        }

        return validationResults;
    }
}

[OptionsValidator]
public partial class ClientWebSettingsValidator : IValidateOptions<ClientWebSettings>
{
}
