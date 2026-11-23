namespace Bit.Fabric.Shared.Features.Attachments;

public enum AttachmentKind
{
    /// <summary>
    /// Resized to fit within 256*256px, preserving the aspect ratio.
    /// </summary>
    UserProfileImageSmall = 0,
    UserProfileImageOriginal = 1,
    /// <summary>
    /// An image the user attached to a message in the AI chat panel.
    /// </summary>
    AiChatImage = 4,
    /// <summary>
    /// A connected app's icon, as its web app manifest names it, shrunk to fit within 128*128px
    /// (bitfabric-internal/specs/monitoring.md, The cards show the app's own icon). Stored under an id of its own, never the
    /// project's, as the public status page shows it.
    /// </summary>
    ProjectIcon = 5
}
