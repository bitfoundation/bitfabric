//+:cnd:noEmit
//#if (offlineDb == true)
using Bit.Bsqlite;
//#endif
//#if (signalR == true)
using Boilerplate.Client.Core.Components.Layout.AiChat;
//#endif

namespace Boilerplate.Client.Core.Infrastructure.Services;

/// <summary>
/// Clear app files: signs out and removes what the app keeps on this device, then reloads it. The App tab of BevTools
/// asks for it (AppDevToolsPanel), and so does the chatbot's tool, over SharedAppMessages.CLEAR_APP_FILES (See AppDevTools).
/// </summary>
public partial class ClearAppFilesService(Cookie cookie,
                                          AuthManager authManager,
                                          LocalStorage localStorage,
                                          CacheStorage cacheStorage,
                                          //#if (signalR == true)
                                          IndexedDb indexedDb,
                                          //#endif
                                          SessionStorage sessionStorage,
                                          IStorageService storageService,
                                          IJSRuntime jsRuntime,
                                          NavigationManager navigationManager,
                                          //#if (offlineDb == true)
                                          SyncService syncService,
                                          IDbContextFactory<AppOfflineDbContext> dbContextFactory,
                                          //#endif
                                          ILogger<ClearAppFilesService> logger)
{
    public async Task ClearAppFiles(CancellationToken cancellationToken)
    {
        //#if (offlineDb == true)
        try
        {
            // Try to push any pending changes before clearing the DB. This deliberately takes the overload that
            // accepts a DbContext rather than SyncService.Push(), which returns without doing anything when the app
            // believes it is offline - the state in which unpushed changes are most likely to exist.
            using var pushCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            pushCts.CancelAfter(TimeSpan.FromSeconds(5));

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(pushCts.Token);
            await syncService.Sync(dbContext, pullRecentChanges: false, pushCts.Token);
        }
        catch (Exception exp)
        {
            logger.LogWarning(exp, "Failed to push Offline Database changes during ClearAppStorage.");
        }
        //#endif

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

        //#if (offlineDb == true)
        try
        {
            if (dbContextFactory is IBsqliteDbContextFactory<AppOfflineDbContext> bsqliteDbContextFactory)
            {
                await bsqliteDbContextFactory.Database.DeleteAsync(cancellationToken); // Blazor Hybrid stores the db outside webview's storage.
            }
            else
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                await dbContext.Database.EnsureDeletedAsync(cancellationToken);
                await dbContext.Database.MigrateAsync(cancellationToken);
            }
        }
        catch (Exception exp)
        {
            logger.LogWarning(exp, "Failed to reset Offline Database during ClearAppStorage.");
        }
        //#endif

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

        //#if (signalR == true)
        // The conversation the AI chat keeps on this device (See AppAiChat.RestoreHistory). The chat drops its connection
        // on delete, which would otherwise block it. A const, so naming it loads nothing of the lazily loaded chat.
        await Attempt(nameof(IndexedDb), () => indexedDb.DeleteDatabase(AppAiChat.HistoryDatabase).AsTask());
        //#endif

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
