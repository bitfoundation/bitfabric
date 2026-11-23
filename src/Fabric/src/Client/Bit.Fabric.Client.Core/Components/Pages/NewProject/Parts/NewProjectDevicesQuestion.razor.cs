using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>
/// Which native apps get set up on this computer now (Q30), each with what it adds to the download and the time, and the
/// computer itself, which decides the install line and the apps it can build.
/// </summary>
public partial class NewProjectDevicesQuestion
{
    [Parameter] public string HeadingElement { get; set; } = "h2";

    /// <summary>
    /// Whether a switched-on app says it can be set up later instead, with a link to the page that does that. The new project
    /// page offers it; that page itself, which shows this question too, doesn't.
    /// </summary>
    [Parameter] public bool OffersLater { get; set; } = true;

    /// <summary>The page that sets up a project that already exists, with this app picked. It opens beside the wizard, which keeps its answers.</summary>
    private static string LaterUrl(NewProjectDevice device) => $"{CultureInfoManager.GetCurrentCultureUrl(PageUrls.SetupProject)}?devices={device.ToString().ToLowerInvariant()}";

    private string DeviceNote(NewProjectDevice device) => (device, Answers.CanBuild(device)) switch
    {
        (NewProjectDevice.Android, _) => Localizer[nameof(AppStrings.NewProjectDeviceAndroidNote)],
        (NewProjectDevice.Ios, true) when Answers.Computer is NewProjectComputer.Windows => Localizer[nameof(AppStrings.NewProjectDeviceIosNoteOnWindows)],
        (NewProjectDevice.Ios, true) => Localizer[nameof(AppStrings.NewProjectDeviceIosNote)],
        (NewProjectDevice.Ios, false) => Localizer[nameof(AppStrings.NewProjectDeviceIosNeedsMac)],
        (NewProjectDevice.Windows, true) => Localizer[nameof(AppStrings.NewProjectDeviceWindowsNote)],
        (NewProjectDevice.Windows, false) => Localizer[nameof(AppStrings.NewProjectDeviceWindowsNeedsPc)],
        (_, true) => Localizer[nameof(AppStrings.NewProjectDeviceMacNote)],
        _ => Localizer[nameof(AppStrings.NewProjectDeviceMacNeedsMac)]
    };
}
