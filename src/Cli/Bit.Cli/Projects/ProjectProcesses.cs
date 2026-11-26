using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Bit.Cli.Infrastructure;

namespace Bit.Cli.Projects;

public sealed record FreedFiles(IReadOnlyList<string> Stopped, IReadOnlyList<string> Holders)
{
    public static FreedFiles None { get; } = new([], []);

    public string? InUseBy => Holders.Count == 0 ? null : $"In use by {string.Join(", ", Holders)}";
}

public static class ProjectProcesses
{
    private static readonly HashSet<string> buildProcesses = new(["dotnet", "MSBuild", "VBCSCompiler", "XamlCompiler"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> projectHosts = new([.. buildProcesses, "aspire"], StringComparer.OrdinalIgnoreCase);

    // Windows' own shell, which bit never stops: the taskbar and the desktop would go with it
    private static readonly HashSet<string> neverStopped = new(["explorer"], StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan exitWait = TimeSpan.FromSeconds(5);

    public static Task<IReadOnlySet<int>> StopAsync(CliServices cli, StepRunner steps, string folder, CancellationToken cancellationToken)
    {
        return StopAsync(cli, steps, folder, "the project's files", cancellationToken);
    }

    /// <summary>
    /// Stops the apps that run from the folder, on Windows, where they keep its files in use. With a deadline, the search for them
    /// and the wait for them to exit end with it; an app the search didn't get to is found later by the files it holds.
    /// </summary>
    public static async Task<IReadOnlySet<int>> StopAsync(CliServices cli, StepRunner steps, string folder, string files, CancellationToken cancellationToken, Deadline? deadline = null)
    {
        var stoppedBuilds = new HashSet<int>();

        if (cli.Environment.IsWindows is false)
            return stoppedBuilds;

        var root = Path.GetFullPath(folder);
        var findings = new Findings();
        var search = Task.Run(() => Find(root, deadline, findings), cancellationToken);

        // A process whose modules take long to read can't be cut short, so bit stops waiting for the search at the deadline
        await cli.Console.RunWithStatusAsync($"Looking for apps that use {files}", async _ => await Task.WhenAny(search, Task.Delay(deadline?.Left ?? Timeout.InfiniteTimeSpan, cancellationToken)));

        cancellationToken.ThrowIfCancellationRequested();
        var found = findings.Close();
        var stopping = new List<(Process Process, StepResult Result)>();

        foreach (var (process, name, id) in found)
        {
            var result = Kill(process, name, id);
            cli.Log.Write($"stop {name} ({id}): {result.Status} {result.Detail}");
            steps.Add("stop-process", result);
            stopping.Add((process, result));

            if (result.Status is StepStatus.Succeeded && buildProcesses.Contains(name))
            {
                stoppedBuilds.Add(id);
            }
        }

        WaitForExit(stopping.Where(s => s.Result.Status is StepStatus.Succeeded).Select(s => s.Process), deadline);

        foreach (var (process, _) in stopping)
        {
            process.Dispose();
        }

        return stoppedBuilds;
    }

    public static FreedFiles Free(CliServices cli, IEnumerable<string> paths, bool stopBuildProcesses = true, IReadOnlySet<int>? stoppedBuilds = null, Deadline? deadline = null)
    {
        if (cli.Environment.IsWindows is false)
            return FreedFiles.None;

        return Free(cli, OperatingSystem.IsWindows() ? FileHolders.Find(paths) : [], stopBuildProcesses ? Builds(stoppedBuilds) : None, "it held the project's files", deadline);
    }

    public static FreedFiles FreeFolder(CliServices cli, string folder, IReadOnlySet<int>? stoppedBuilds = null, Deadline? deadline = null)
    {
        if (cli.Environment.IsWindows is false)
            return FreedFiles.None;

        return Free(cli, OperatingSystem.IsWindows() ? FolderHolders.Find(folder) : [], Builds(stoppedBuilds), "it was in the project's folder", deadline);
    }

    /// <summary>
    /// Stops every app that still holds one of the files or is in the folder, build process or not, for bit remove once its time
    /// to wait is up. It never stops bit itself, the terminal it runs in or Windows' own shell.
    /// </summary>
    public static FreedFiles ForceFree(CliServices cli, string folder, IEnumerable<string> paths, Deadline? deadline = null)
    {
        if (cli.Environment.IsWindows is false)
            return FreedFiles.None;

        List<int> ids = OperatingSystem.IsWindows() ? [.. FileHolders.Find(paths).Concat(FolderHolders.Find(folder)).Distinct()] : [];

        return Free(cli, ids, (name, _, _) => neverStopped.Contains(name) is false, "it still used the folder when bit was done waiting", deadline);
    }

    private delegate bool StopRule(string name, int id, HashSet<int> ancestors);

    private static StopRule None => (_, _, _) => false;

    private static StopRule Builds(IReadOnlySet<int>? stoppedBuilds) => (name, id, ancestors) => buildProcesses.Contains(name) || IsStartedByABuild(id, stoppedBuilds, ancestors);

    private static FreedFiles Free(CliServices cli, IReadOnlyList<int> ids, StopRule stops, string reason, Deadline? deadline)
    {
        var stopped = new List<string>();
        var holders = new List<string>();
        var found = new List<(Process Process, string Name, int Id, bool Stops)>();
        var ancestors = Ancestors();

        foreach (var id in ids)
        {
            if (id == Environment.ProcessId || ancestors.Contains(id) || TryGetProcess(id) is not { } process)
                continue;

            if (TryGetName(process) is { } name)
            {
                found.Add((process, name, id, stops(name, id, ancestors)));
            }
            else
            {
                process.Dispose();
            }
        }

        var killed = new List<Process>();

        foreach (var (process, name, id, stop) in found)
        {
            if (stop && Kill(process, name, id) is { Status: StepStatus.Succeeded })
            {
                cli.Log.Write($"stop {name} ({id}): {reason}");
                stopped.Add($"{name} ({id})");
                killed.Add(process);
            }
            else
            {
                holders.Add($"{name} ({id})");
            }
        }

        WaitForExit(killed, deadline);

        foreach (var (process, _, _, _) in found)
        {
            process.Dispose();
        }

        return new(stopped, holders);
    }

    private static bool IsStartedByABuild(int id, IReadOnlySet<int>? stoppedBuilds, HashSet<int> ancestors)
    {
        var seen = new HashSet<int>();

        while (OperatingSystem.IsWindows() && FolderHolders.ParentOf(id) is { } parentId && parentId != Environment.ProcessId && ancestors.Contains(parentId) is false && seen.Add(parentId))
        {
            if (stoppedBuilds?.Contains(parentId) is true)
                return true;

            if (TryGetProcess(parentId) is not { } parent)
                return false;

            using (parent)
            {
                if (TryGetName(parent) is { } name && buildProcesses.Contains(name))
                    return true;
            }

            id = parentId;
        }

        return false;
    }

    private static HashSet<int> Ancestors()
    {
        var ids = new HashSet<int>();
        var id = Environment.ProcessId;

        while (OperatingSystem.IsWindows() && FolderHolders.ParentOf(id) is { } parent && ids.Add(parent))
        {
            id = parent;
        }

        return ids;
    }

    public static bool IsUnder(string path, string folder)
    {
        var root = folder.TrimEnd('\\', '/');

        return root.Length > 0
            && path.Length > root.Length + 1
            && path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            && path[root.Length] is '\\' or '/';
    }

    public static bool Mentions(string commandLine, string folder, string? workingDirectory = null)
    {
        var root = folder.TrimEnd('\\', '/');

        return root.Length > 0 && Arguments(commandLine).Any(argument => Names(argument, root, workingDirectory)
            || (argument.IndexOf('=') is var equals and >= 0 && Names(argument[(equals + 1)..], root, workingDirectory)));
    }

    private static bool Names(string argument, string root, string? workingDirectory)
    {
        if (argument.Length == 0)
            return false;

        string path;

        try
        {
            path = Path.IsPathFullyQualified(argument) ? Path.GetFullPath(argument) : workingDirectory is null ? argument : Path.GetFullPath(argument, workingDirectory);
        }
        catch (Exception exp) when (exp is ArgumentException or NotSupportedException)
        {
            return false;
        }

        return string.Equals(path.TrimEnd('\\', '/'), root, StringComparison.OrdinalIgnoreCase) || IsUnder(path, root);
    }

    private static IEnumerable<string> Arguments(string commandLine)
    {
        var argument = new StringBuilder();
        var quoted = false;

        foreach (var c in commandLine.Append(' '))
        {
            if (c is '"')
            {
                quoted = !quoted;
            }
            else if (char.IsWhiteSpace(c) && quoted is false)
            {
                if (argument.Length > 0)
                {
                    yield return argument.ToString();
                    argument.Clear();
                }
            }
            else
            {
                argument.Append(c);
            }
        }
    }

    /// <summary>
    /// The processes that run from the folder. Reading every process's modules takes seconds, so they're read in parallel, and
    /// no more of them once the deadline is over.
    /// </summary>
    private static void Find(string folder, Deadline? deadline, Findings findings)
    {
        var ancestors = Ancestors();

        Parallel.ForEach(Process.GetProcesses(), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (process, state) =>
        {
            if (deadline?.IsOver is true || findings.IsClosed)
            {
                state.Stop();
                process.Dispose();
                return;
            }

            if (process.Id != Environment.ProcessId && ancestors.Contains(process.Id) is false && TryGetName(process) is { } name && (UsesFolder(process, folder) || RunsTheProject(process.Id, name, folder)))
            {
                findings.Add((process, name, process.Id));
            }
            else
            {
                process.Dispose();
            }
        });
    }

    /// <summary>What the search for the processes has found, until bit stops waiting for it: a process it finds after that is let go.</summary>
    private sealed class Findings
    {
        private readonly List<(Process Process, string Name, int Id)> found = [];

        public bool IsClosed { get; private set; }

        public void Add((Process Process, string Name, int Id) process)
        {
            lock (found)
            {
                if (IsClosed)
                {
                    process.Process.Dispose();
                    return;
                }

                found.Add(process);
            }
        }

        public List<(Process Process, string Name, int Id)> Close()
        {
            lock (found)
            {
                IsClosed = true;
                return [.. found.OrderByDescending(f => projectHosts.Contains(f.Name)).ThenBy(f => f.Id)];
            }
        }
    }

    private static bool RunsTheProject(int id, string name, string folder)
    {
        return OperatingSystem.IsWindows() && projectHosts.Contains(name) && FolderHolders.Runs(id, folder);
    }

    private static bool UsesFolder(Process process, string folder)
    {
        try
        {
            if (process.MainModule?.FileName is { } main && IsUnder(main, folder))
                return true;

            return process.Modules.Cast<ProcessModule>().Any(module => module.FileName is { } file && IsUnder(file, folder));
        }
        catch (Exception exp) when (exp is Win32Exception or InvalidOperationException or NotSupportedException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private static Process? TryGetProcess(int id)
    {
        try
        {
            return Process.GetProcessById(id);
        }
        catch (Exception exp) when (exp is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string? TryGetName(Process process)
    {
        try
        {
            return process.ProcessName;
        }
        catch (Exception exp) when (exp is InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    private static StepResult Kill(Process process, string name, int id)
    {
        try
        {
            process.Kill();
            return StepResult.Succeeded($"Stopped {name}", $"process {id}");
        }
        catch (Exception exp) when (exp is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return StepResult.Warning($"Couldn't stop {name}", $"process {id}: {exp.Message}");
        }
    }

    /// <summary>
    /// Waits for the stopped processes to exit, so that they let go of their files: up to 5 seconds for all of them together, and
    /// never past the deadline.
    /// </summary>
    private static void WaitForExit(IEnumerable<Process> processes, Deadline? deadline)
    {
        var waiting = Stopwatch.StartNew();
        var most = deadline?.AtMost(exitWait) ?? exitWait;

        foreach (var process in processes)
        {
            var left = most - waiting.Elapsed;

            try
            {
                process.WaitForExit(left > TimeSpan.Zero ? left : TimeSpan.Zero);
            }
            catch (Exception exp) when (exp is Win32Exception or InvalidOperationException)
            {
            }
        }
    }
}
