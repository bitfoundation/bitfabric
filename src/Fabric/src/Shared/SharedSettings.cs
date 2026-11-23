using Microsoft.Extensions.Options;

namespace Bit.Fabric.Shared;

public partial class SharedSettings : IValidatableObject
{
    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var validationResults = new List<ValidationResult>();

        return validationResults;
    }
}

[OptionsValidator]
public partial class SharedSettingsValidator : IValidateOptions<SharedSettings>
{
}
