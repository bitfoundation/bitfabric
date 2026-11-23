namespace Bit.Fabric.Shared.Features.Attachments;

[Route("api/v1/[controller]/[action]/"), AuthorizedApi]
public interface IAttachmentController : IAppController
{
    [HttpDelete]
    Task DeleteUserProfileImage(CancellationToken cancellationToken);

}
