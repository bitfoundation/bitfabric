using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Bit.Cli.Infrastructure;

namespace Bit.Cli.Tools;

public static class Oasdiff
{
    public const string Version = "1.41.0";

    private static readonly Dictionary<string, string> archiveSha256 = new(StringComparer.Ordinal)
    {
        ["windows_amd64"] = "dcacaa482ba2e5a783c7e5c7682a96e1d0d65180a897fc66f13e66dc814a7a83",
        ["windows_arm64"] = "3f09d256bf3344b0afafef1e7955c235326f6980055d11867702d97f24559a1b",
        ["linux_amd64"] = "96365453163118e6503eb71742643d9ee67f91aedbaadd1a1ce9c6213e3b71c7",
        ["linux_arm64"] = "8d4b8ddd467440885675db918e229ab8db330111c6428eb864e7eb766a7f4071",
        ["darwin_all"] = "0452671a98a290ab7c5e49b274fdc77b34c5cdb595b4305cfdb64ae011f7f1ee"
    };

    public static string? Platform(CliEnvironment environment) => (environment.Os, environment.Architecture) switch
    {
        (HostOs.Windows, Architecture.X64) => "windows_amd64",
        (HostOs.Windows, Architecture.Arm64) => "windows_arm64",
        (HostOs.Linux, Architecture.X64) => "linux_amd64",
        (HostOs.Linux, Architecture.Arm64) => "linux_arm64",
        (HostOs.MacOS, Architecture.X64 or Architecture.Arm64) => "darwin_all",
        _ => null
    };

    public static string ArchiveUrl(string platform) => $"https://github.com/oasdiff/oasdiff/releases/download/v{Version}/oasdiff_{Version}_{platform}.tar.gz";

    public static string ExecutablePath(CliEnvironment environment) => Path.Combine(environment.BitDirectory, "tools", "oasdiff", Version, environment.IsWindows ? "oasdiff.exe" : "oasdiff");

    public static async Task<string> EnsureAsync(CliEnvironment environment, HttpMessageHandler handler, CancellationToken cancellationToken)
    {
        var executable = ExecutablePath(environment);

        if (File.Exists(executable))
            return executable;

        var platform = Platform(environment) ?? throw new PlatformNotSupportedException($"oasdiff has no build for {environment.Os} on {environment.Architecture}.");
        var url = ArchiveUrl(platform);

        using var http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromMinutes(5) };
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("bit-cli", BuildInfo.Version));

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var archive = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        if (Convert.ToHexStringLower(SHA256.HashData(archive)) != archiveSha256[platform])
            throw new InvalidDataException($"{url} isn't the archive oasdiff {Version} released: its SHA-256 differs.");

        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        var download = executable + ".download";

        await using (var gzip = new GZipStream(new MemoryStream(archive), CompressionMode.Decompress))
        await using (var tar = new TarReader(gzip))
        {
            while (await tar.GetNextEntryAsync(cancellationToken: cancellationToken) is { } entry)
            {
                if (entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile && entry.Name == Path.GetFileName(executable))
                {
                    await entry.ExtractToFileAsync(download, overwrite: true, cancellationToken);
                    break;
                }
            }
        }

        if (File.Exists(download) is false)
            throw new InvalidDataException($"{url} has no {Path.GetFileName(executable)}.");

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(download, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        File.Move(download, executable, overwrite: true);
        return executable;
    }
}
