using System.Text.Json;
using System.Text.RegularExpressions;
using Bit.Cli.Infrastructure;
using Spectre.Console;

namespace Bit.Cli.Projects;

/// <param name="Branch">The branch checked out in it, as GitHub last saw it.</param>
public sealed record Codespace(string Name, string? DisplayName, string State, string? Branch, bool HasUncommittedChanges, bool HasUnpushedChanges, DateTimeOffset? LastUsedAt)
{
    public bool IsAvailable => State is "Available";
}

public sealed record CodespaceMachine(string Name, string? DisplayName, int Cpus, long Memory, bool PrebuildReady);

/// <summary>
/// The GitHub codespaces of a repository, through the GitHub CLI. bit codespace keeps one codespace per branch: it reuses the
/// codespace that has the branch checked out, starting it when it's stopped, and creates one only when there's none, so
/// running the project again doesn't pay for another machine and another first setup.
/// </summary>
public sealed partial class Codespaces(CliServices cli, string root, string repository)
{
    /// <summary>The longest display name GitHub takes for a codespace.</summary>
    public const int DisplayNameLength = 48;

    public string Repository => repository;

    public string RepositoryName => repository[(repository.IndexOf('/') + 1)..];

    /// <summary>
    /// The codespaces of the repository. When the GitHub CLI's sign-in lacks the codespace scope, which gh auth login doesn't
    /// ask for, bit asks GitHub for it in the browser and lists them again; without a terminal it can't, and says how.
    /// </summary>
    public async Task<(IReadOnlyList<Codespace>? Codespaces, string? Error)> ListAsync(CancellationToken cancellationToken)
    {
        string[] list = ["codespace", "list", "--repo", repository, "--json", "name,displayName,gitStatus,state,lastUsedAt", "--limit", "100"];
        var result = await Gh(list, cancellationToken);

        if (result.Succeeded is false && NeedsCodespaceScopeRegex().IsMatch(result.Output))
        {
            if (cli.HasTerminal is false)
                return (null, "The GitHub CLI's sign-in can't manage codespaces yet. Run gh auth refresh -h github.com -s codespace, then this again.");

            cli.Console.Out.MarkupLine("  [grey]The GitHub CLI's sign-in can't manage codespaces yet, so GitHub asks you to allow it in your browser.[/]");
            await cli.Runner.RunAsync(new ProcessSpec { FileName = "gh", Arguments = ["auth", "refresh", "--hostname", "github.com", "--scopes", "codespace"], WorkingDirectory = root, Interactive = true, Timeout = TimeSpan.FromMinutes(15) }, cancellationToken);
            result = await Gh(list, cancellationToken);
        }

        if (result.Succeeded is false)
            return (null, $"Couldn't list the codespaces of {repository}: {LastLine(result)}");

        try
        {
            return (ParseList(result.Output), null);
        }
        catch (JsonException exp)
        {
            return (null, $"Couldn't read the codespaces of {repository}: {exp.Message}");
        }
    }

    public static IReadOnlyList<Codespace> ParseList(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind is not JsonValueKind.Array)
            return [];

        return [.. document.RootElement.EnumerateArray().Where(c => c.ValueKind is JsonValueKind.Object && Text(c, "name") is { Length: > 0 }).Select(c =>
        {
            var status = c.TryGetProperty("gitStatus", out var git) && git.ValueKind is JsonValueKind.Object ? git : default;

            return new Codespace(
                Text(c, "name")!,
                Text(c, "displayName"),
                Text(c, "state") ?? "",
                status.ValueKind is JsonValueKind.Object ? Text(status, "ref") : null,
                status.ValueKind is JsonValueKind.Object && Flag(status, "hasUncommittedChanges"),
                status.ValueKind is JsonValueKind.Object && Flag(status, "hasUnpushedChanges"),
                DateTimeOffset.TryParse(Text(c, "lastUsedAt"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var used) ? used : null);
        })];
    }

    /// <summary>The codespace of the branch: one that runs before a stopped one, then the one used last.</summary>
    public static Codespace? Pick(IReadOnlyList<Codespace> codespaces, string branch) => codespaces
        .Where(c => string.Equals(c.Branch, branch, StringComparison.Ordinal))
        .OrderByDescending(c => c.IsAvailable)
        .ThenByDescending(c => c.LastUsedAt ?? DateTimeOffset.MinValue)
        .FirstOrDefault();

    /// <summary>
    /// The machine for a new codespace, among those GitHub offers for the branch, which already meet the dev container's
    /// hostRequirements: the smallest with a prebuild ready, which starts in a minute instead of setting the project up first,
    /// or else the smallest, which costs least.
    /// </summary>
    public async Task<CodespaceMachine?> MachineAsync(string branch, CancellationToken cancellationToken)
    {
        var result = await Gh(["api", $"repos/{repository}/codespaces/machines?ref={Uri.EscapeDataString(branch)}"], cancellationToken);

        if (result.Succeeded is false)
            return null;

        try
        {
            return PickMachine(ParseMachines(result.Output));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static IReadOnlyList<CodespaceMachine> ParseMachines(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.TryGetProperty("machines", out var machines) is false || machines.ValueKind is not JsonValueKind.Array)
            return [];

        return [.. machines.EnumerateArray().Where(m => m.ValueKind is JsonValueKind.Object && Text(m, "name") is { Length: > 0 }).Select(m => new CodespaceMachine(
            Text(m, "name")!,
            Text(m, "display_name"),
            m.TryGetProperty("cpus", out var cpus) && cpus.TryGetInt32(out var count) ? count : int.MaxValue,
            m.TryGetProperty("memory_in_bytes", out var memory) && memory.TryGetInt64(out var bytes) ? bytes : long.MaxValue,
            Text(m, "prebuild_availability") is "ready"))];
    }

    public static CodespaceMachine? PickMachine(IReadOnlyList<CodespaceMachine> machines) => machines
        .OrderByDescending(m => m.PrebuildReady)
        .ThenBy(m => m.Cpus)
        .ThenBy(m => m.Memory)
        .FirstOrDefault();

    public static string DisplayName(string project, string branch)
    {
        var name = $"{project} {branch}";
        return name.Length <= DisplayNameLength ? name : name[..DisplayNameLength];
    }

    /// <summary>Creates a codespace of the branch; its name, or null with gh's result.</summary>
    public async Task<(string? Name, ProcessResult Result)> CreateAsync(string branch, string? machine, string displayName, Action<string> progress, CancellationToken cancellationToken)
    {
        string[] arguments = ["codespace", "create", "--repo", repository, "--branch", branch, "--display-name", displayName, "--default-permissions", .. machine is null ? Array.Empty<string>() : ["--machine", machine]];
        var result = await cli.Runner.RunAsync(new ProcessSpec { FileName = "gh", Arguments = arguments, WorkingDirectory = root, OnOutputLine = progress, Timeout = TimeSpan.FromMinutes(20) }, cancellationToken);
        var name = result.Succeeded ? result.OutputLines.Select(l => l.Trim()).LastOrDefault(l => CodespaceNameRegex().IsMatch(l)) : null;

        return (name, result);
    }

    /// <summary>
    /// Runs the project in the codespace through gh codespace ssh, which starts a stopped codespace first, on a terminal of its
    /// own (-tt), so Ctrl+C reaches bit run there and closing this terminal stops it.
    /// </summary>
    public Task<ProcessResult> RunAsync(string codespace, string script, CancellationToken cancellationToken)
    {
        return cli.Runner.RunAsync(new ProcessSpec { FileName = "gh", Arguments = ["codespace", "ssh", "--codespace", codespace, "--", "-tt", script], WorkingDirectory = root, Interactive = true, TakesOver = true }, cancellationToken);
    }

    /// <summary>Forwards the ports to the same ones on this machine, until it's canceled.</summary>
    public Task<ProcessResult> ForwardAsync(string codespace, IReadOnlyList<int> ports, CancellationToken cancellationToken)
    {
        return cli.Runner.RunAsync(new ProcessSpec { FileName = "gh", Arguments = ["codespace", "ports", "forward", .. ports.Select(port => $"{port}:{port}"), "--codespace", codespace], WorkingDirectory = root }, cancellationToken);
    }

    public Task<ProcessResult> OpenAsync(string codespace, bool web, CancellationToken cancellationToken)
    {
        return cli.Runner.RunAsync(new ProcessSpec { FileName = "gh", Arguments = ["codespace", "code", "--codespace", codespace, .. web ? new[] { "--web" } : []], WorkingDirectory = root, Interactive = true, Timeout = TimeSpan.FromMinutes(10) }, cancellationToken);
    }

    public Task<ProcessResult> StopAsync(string codespace, CancellationToken cancellationToken) => Gh(["codespace", "stop", "--codespace", codespace], cancellationToken);

    public Task<ProcessResult> DeleteAsync(string codespace, CancellationToken cancellationToken) => Gh(["codespace", "delete", "--codespace", codespace, "--force"], cancellationToken);

    /// <summary>
    /// What runs in the codespace, in its shell: the project's folder in the workspace Codespaces clones the repository into,
    /// with bit's own folders on the PATH and the development certificate's trust for .NET, as the dev container's environment,
    /// which an SSH session doesn't get, has them. It first waits for the codespace to finish setting the project up, while bit
    /// setup, the dev container's onCreateCommand, runs; a fresh codespace is available before that ends. Then it brings the
    /// branch up to date, when that's a fast-forward, and runs bit run. The lines have no double quotes or apostrophes, which
    /// gh, ssh and the shell would each read again.
    /// </summary>
    public static string RemoteScript(string repositoryName, string projectFolder)
    {
        var workspace = $"/workspaces/{repositoryName}";
        var folder = projectFolder is "" or "." ? workspace : $"{workspace}/{projectFolder.Replace('\\', '/')}";

        return string.Join("; ",
            $"cd {ProcessSpec.ShellQuote(folder, HostOs.Linux)} || exit 1",
            "export PATH=$PATH:$HOME/.dotnet/tools:$HOME/.aspire/bin",
            ": ${SSL_CERT_DIR:=$HOME/.aspnet/dev-certs/trust:/usr/lib/ssl/certs:/etc/ssl/certs}",
            "export SSL_CERT_DIR",
            // [b]it matches bit, but not this line itself, which is on the shell's own command line
            @"busy() { for f in /proc/[0-9]*/cmdline; do tr '\0' ' ' < $f 2>/dev/null | grep -q '[b]it setup' && return 0; done; return 1; }",
            "w=0",
            "while busy || ! command -v bit >/dev/null 2>&1; do " +
                "if [ $w -eq 0 ]; then echo Waiting for the codespace to finish getting the project ready...; fi; " +
                "if [ $w -ge 120 ] && ! busy; then echo The codespace has no bit command. Its dev container installs bit CLI when the codespace is created.; exit 127; fi; " +
                "if [ $w -ge 3600 ]; then break; fi; " +
                "sleep 5; w=$((w+5)); " +
            "done",
            "GIT_TERMINAL_PROMPT=0 git pull --ff-only --quiet 2>/dev/null || echo The codespace runs the commits it has, since git could not fast-forward it to the branch on GitHub.",
            "exec bit run");
    }

    /// <summary>
    /// The ports of the project's web servers, the standalone api's and the Aspire dashboard's, from their launchSettings.json:
    /// forwarded to the same ports here, the app's localhost addresses, such as its ServerAddress, work as they do on this machine.
    /// </summary>
    public static IReadOnlyList<int> Ports(ProjectContext project)
    {
        List<string?> urls = [.. project.ServerWebProjects.Select(web => ProjectSteps.ReadLaunchUrl(Path.GetDirectoryName(web)!))];

        if (project.IsApiStandalone)
        {
            urls.Add(ProjectSteps.ReadLaunchUrl(project.ServerApiDirectory));
        }

        if (System.IO.Directory.Exists(project.AppHostDirectory))
        {
            urls.Add(ProjectSteps.ReadLaunchUrl(project.AppHostDirectory));
        }

        return [.. urls.Select(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Port : -1).Where(port => port > 0).Distinct()];
    }

    private Task<ProcessResult> Gh(string[] arguments, CancellationToken cancellationToken)
    {
        return cli.Runner.RunAsync(new ProcessSpec { FileName = "gh", Arguments = arguments, WorkingDirectory = root, Timeout = TimeSpan.FromMinutes(2) }, cancellationToken);
    }

    public static string LastLine(ProcessResult result) => result.NotFound ? "the GitHub CLI isn't installed" : result.TimedOut ? "it took too long" : result.OutputLines.LastOrDefault(l => l.Trim().Length > 0)?.Trim() ?? $"exit code {result.ExitCode}";

    private static string? Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String ? value.GetString() : null;

    private static bool Flag(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True;

    [GeneratedRegex(@"needs the ""codespace"" scope|-s codespace")]
    private static partial Regex NeedsCodespaceScopeRegex();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{2,}$")]
    private static partial Regex CodespaceNameRegex();
}
