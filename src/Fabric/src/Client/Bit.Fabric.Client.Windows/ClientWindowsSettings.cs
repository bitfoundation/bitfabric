using Bit.Fabric.Client.Core;
using Microsoft.Extensions.Options;

namespace Bit.Fabric.Client.Windows;

public class ClientWindowsSettings : ClientCoreSettings
{
    public WindowsUpdateOptions? WindowsUpdate { get; set; }
}

[OptionsValidator]
public partial class ClientWindowsSettingsValidator : IValidateOptions<ClientWindowsSettings>
{
}

public partial class WindowsUpdateOptions
{
    public bool AutoReload { get; set; }

    public string? FilesUrl { get; set; }
}
