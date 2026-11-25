using Bit.Fabric.Client.Core.Components.Layout.AiChat;

namespace Bit.Fabric.Client.Core.Infrastructure.Services;

/// <summary>
/// Clear app files: signs out and removes what the app keeps on this device, then reloads it. The App tab of BevTools
/// asks for it (AppDevToolsPanel), and so does the chatbot's tool, over <see cref="SharedAppMessages.CLEAR_APP_FILES"/>
/// (See AppDevTools).
/// </summary>
public partial class ClearAppFilesService(Cookie cookie,
                                          AuthManager authManager,
                                          LocalStorage localStorage,
                                          CacheStorage cacheStorage,
                                          IndexedDb indexedDb,
                                          SessionStorage sessionStorage,
                                          IStorageService storageService,
                                          IJSRuntime jsRuntime,
                                          NavigationManager navigationManager,
                                          ILogger<ClearAppFilesService> logger)
{
    public async Task ClearAppFiles(CancellationToken cancellationToken)
    {
        try
        {
            await authManager.SignOutAll(default);
        }
        catch (Exception exp)
        {
            logger.LogWarning(exp, "Failed to sign out during ClearAppStorage.");
        }

        try
        {
            await storageService.Clear(); // Blazor Hybrid stores key/value pairs outside webview's storage.
        }
        catch (Exception exp)
        {
            logger.LogWarning(exp, "Failed to clear the storage service during ClearAppStorage.");
        }

        await ClearWebStorages();


        if (AppPlatform.IsBlazorHybrid is false)
        {
            await jsRuntime.InvokeVoidAsync("BitBswup.forceRefresh", cancellationToken); // Clears cache storages and uninstalls service-worker.
        }
        else
        {
            navigationManager.Refresh(forceReload: true);
        }
    }

    /// <summary>
    /// Clears the browser / web view storages of this origin.
    /// </summary>
    private async Task ClearWebStorages()
    {
        await Attempt(nameof(CacheStorage), async () =>
        {
            if (await cacheStorage.IsSupported() is false) return;

            foreach (var cacheName in await cacheStorage.Keys())
            {
                await cacheStorage.Delete(cacheName);
            }
        });

        await Attempt(nameof(LocalStorage), localStorage.Clear);

        await Attempt(nameof(SessionStorage), sessionStorage.Clear);

        // The conversation the AI chat keeps on this device (See AppAiChat.RestoreHistory). The chat drops its connection
        // on delete, which would otherwise block it. A const, so naming it loads nothing of the lazily loaded chat.
        await Attempt(nameof(IndexedDb), () => indexedDb.DeleteDatabase(AppAiChat.HistoryDatabase).AsTask());

        await Attempt(nameof(Cookie), async () =>
        {
            foreach (var item in await cookie.GetAll())
            {
                await cookie.Remove(new ButilCookie { Name = item.Name, Path = "/" });
            }
        });

        async Task Attempt(string storageName, Func<Task> clear)
        {
            try
            {
                await clear();
            }
            catch (Exception exp)
            {
                logger.LogWarning(exp, "Failed to clear {Storage} during ClearAppStorage.", storageName);
            }
        }
    }
}
