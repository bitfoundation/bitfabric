using Bit.Fabric.Client.Core;
using Microsoft.Extensions.Options;

namespace Bit.Fabric.Client.Maui;

public class ClientMauiSettings : ClientCoreSettings
{

}

[OptionsValidator]
public partial class ClientMauiSettingsValidator : IValidateOptions<ClientMauiSettings>
{
}
