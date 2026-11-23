using Bit.Fabric.Shared.Features.PushNotification;

namespace Bit.Fabric.Server.Api.Features.PushNotification;

/// <summary>
/// More info at src/Server/Bit.Fabric.Server.Api/Features/Mappers.md
/// </summary>
[Mapper]
public static partial class PushNotificationMapper
{
    public static partial void Patch(this PushNotificationSubscriptionDto source, PushNotificationSubscription destination);
}
