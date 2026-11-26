using Bit.BevTools;

namespace Boilerplate.Client.Core.Components.Layout.Diagnostic;

/// <summary>
/// The app's part of bit BevTools, for both ways it runs: registered at startup (Development and Blazor Hybrid) and
/// attached on demand everywhere else. IClientCoreServiceCollectionExtensions hands it to the loader through a lambda
/// (BevToolsLoaderOptions.ConfigureBevTools), so the startup code names no Bit.BevTools type and this runs only once
/// Bit.BevTools is loaded.
/// </summary>
/// <remarks>
/// Public, and safe to be: it only names Bit.BevTools types in its members. A public type of this assembly whose base type
/// or interface lives in Bit.BevTools would break the router's start before a lazily loaded Bit.BevTools.wasm is there,
/// which is why <see cref="AppDevToolsPanel"/> is a plain component of the app.
/// </remarks>
public static class AppBevTools
{
    /// <summary>The app's own panel, which BevTools opens on unless asked for another one (See AppDevTools).</summary>
    public const string AppPanelId = "app";

    public static void Configure(BevToolsOptions options)
    {
        // Before BevTools' other extension panels, so it is the first tab after the built-in ones.
        options.AddPanel<AppDevToolsPanel>(AppPanelId, "App", order: 0);
    }
}
