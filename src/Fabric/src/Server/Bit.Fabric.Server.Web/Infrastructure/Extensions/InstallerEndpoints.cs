using Bit.Fabric.Server.Web;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// The one-line installers the project wizard gives (bitfabric-internal/specs/getting-started.md): /install.ps1 for
/// Windows, run as
/// <c>powershell -c "irm https://bitfabric.co/install.ps1 | iex; bit new ..."</c> from PowerShell or Command Prompt, and
/// /install.sh for macOS and Linux, run as
/// <c>curl -fsSL https://bitfabric.co/install.sh | bash -s -- new ...</c>. Each installs Microsoft's .NET SDK and bit CLI for
/// the visitor's user, and bit new then does the rest. They are plain text, so the wizard's "Read the installer before you
/// run it" opens them in the browser as they are.
/// </summary>
public static class InstallerEndpoints
{
    extension(WebApplication app)
    {
        public WebApplication MapInstallers()
        {
            var options = app.Services.GetRequiredService<ServerWebSettings>().Install;

            // The settings go into the scripts once, at startup; InstallOptions allows only a version's characters in them
            var powerShell = Read("install.ps1", options);
            // A bash script with a carriage return at its line ends fails on its first line, whatever the checkout did to the file
            var shell = Read("install.sh", options).ReplaceLineEndings("\n");

            // Built once in memory, so only the CDN edge and the browser keep them, for an hour
            app.MapGet("/install.ps1", [AppResponseCache(SharedMaxAge = 3600, SkipOutputCache = true)] () => Results.Text(powerShell, "text/plain; charset=utf-8"))
                .CacheOutput("AppResponseCachePolicy")
                .ExcludeFromDescription();

            app.MapGet("/install.sh", [AppResponseCache(SharedMaxAge = 3600, SkipOutputCache = true)] () => Results.Text(shell, "text/plain; charset=utf-8"))
                .CacheOutput("AppResponseCachePolicy")
                .ExcludeFromDescription();

            return app;
        }
    }

    private static string Read(string name, InstallOptions options)
    {
        using var stream = typeof(InstallerEndpoints).Assembly.GetManifestResourceStream($"Installers/{name}")
            ?? throw new InvalidOperationException($"The installer {name} isn't embedded in Server.Web.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd()
            .Replace("{{SdkChannel}}", options.SdkChannel, StringComparison.Ordinal)
            .Replace("{{BitCliVersion}}", options.BitCliVersion ?? string.Empty, StringComparison.Ordinal);
    }
}
