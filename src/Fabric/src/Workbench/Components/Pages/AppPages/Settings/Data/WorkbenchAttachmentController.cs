using Bit.Fabric.Shared.Features.Attachments;

namespace Bit.Fabric.Workbench.Components.Pages.AppPages.Settings.Data;

/// <summary>
/// The stand-in for the app's <see cref="IAttachmentController"/>: removing the profile image. Uploading one goes to the
/// address the Workbench serves (See WorkbenchProfileImages), as the app's page posts the file itself.
/// </summary>
public sealed class WorkbenchAttachmentController(WorkbenchScenarioState scenarioState, SettingsSampleAccount account)
    : WorkbenchStandIn(scenarioState, "settings"), IAttachmentController
{
    public Task DeleteUserProfileImage(CancellationToken cancellationToken) => Save(account.RemovePicture, cancellationToken);
}
