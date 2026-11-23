// [mirror] not implemented push notification service of the windows targets - keep in sync with:
// - src/Client/Bit.Fabric.Client.Maui/Platforms/Windows/Services/WindowsPushNotificationService.cs

using Bit.Fabric.Shared.Features.PushNotification;

namespace Bit.Fabric.Client.Windows.Infrastructure.Services;

public partial class WindowsPushNotificationService : PushNotificationServiceBase
{
    public override Task<PushNotificationSubscriptionDto?> GetSubscription(CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public override Task RequestPermission(CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
