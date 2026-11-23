using System.Text;
using Bit.BevTools;

namespace Bit.Fabric.Client.Core.Components.Layout.Diagnostic;

public partial class AppDiagnosticModal
{
    [AutoInject] private Cookie cookie = default!;
    [AutoInject] private AuthManager authManager = default!;
    [AutoInject] private LocalStorage localStorage = default!;
    [AutoInject] private CacheStorage cacheStorage = default!;
    [AutoInject] private IndexedDb indexedDb = default!;
    [AutoInject] private SessionStorage sessionStorage = default!;
    [AutoInject] private IStorageService storageService = default!;
    [AutoInject] private IAppUpdateService appUpdateService = default!;
    [AutoInject] private ILogger<AppDiagnosticModal> logger = default!;
    [AutoInject] private BevToolsLoader bevToolsLoader = default!;

    private static async Task ThrowTestException()
    {
        await Task.Delay(250);

        showKnownException = !showKnownException;

        throw showKnownException
            ? new InvalidOperationException("Something critical happened.").WithData("TestData", 1)
            : new DomainLogicException("Something bad happened.").WithData("TestData", 2);
    }

    private async Task OpenDevTools()
    {
        if (await bevToolsLoader.OpenAsync())
        {
            isOpen = false;
        }
        else
        {
            SnackBarService.Error("Open dev tools", bevToolsLoader.Problem ?? string.Empty);
        }
    }

    private async Task CallGC()
    {
        SnackBarService.Show("Memory Before GC", GetMemoryUsage());

        await Task.Run(() =>
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        });

        SnackBarService.Show("Memory After GC", GetMemoryUsage());
    }

    private string GetMemoryUsage()
    {
        long memory = Environment.WorkingSet;
        return $"{memory / (1024.0 * 1024.0):F2} MB";
    }

    private async Task ClearAppFiles()
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
            await JSRuntime.InvokeVoidAsync("BitBswup.forceRefresh"); // Clears cache storages and uninstalls service-worker.
        }
        else
        {
            NavigationManager.Refresh(forceReload: true);
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
        await Attempt(nameof(IndexedDb), () => indexedDb.DeleteDatabase(AiChat.AppAiChat.HistoryDatabase).AsTask());

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

    private async Task UpdateApp()
    {
        await appUpdateService.ForceUpdate();
    }
}
