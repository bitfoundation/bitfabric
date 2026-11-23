using Bit.Fabric.Shared.Features.Attachments;

namespace Bit.Fabric.Server.Api.Features.Attachments;

public partial class Attachment
{
    public Guid Id { get; set; }

    public AttachmentKind Kind { get; set; }

    public string? Path { get; set; }

    public DateTimeOffset CreatedOn { get; set; }
}
