using Maui.AppStores;

namespace Bit.Fabric.Client.Maui.Infrastructure.Services;

public partial class MauiAppUpdateService : IAppUpdateService
{
    public async Task ForceUpdate()
    {
        await AppStoreInfo.Current.OpenApplicationInStoreAsync();
    }
}
