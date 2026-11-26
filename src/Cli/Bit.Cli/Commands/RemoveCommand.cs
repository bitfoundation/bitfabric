using System.CommandLine;
using System.Globalization;
using System.Runtime.InteropServices;
using Bit.Cli.Infrastructure;
using Bit.Cli.Projects;
using Spectre.Console;

namespace Bit.Cli.Commands;

public static class RemoveCommand
{
    private const int ShownFailures = 3;

    /// <summary>
    /// The longest bit remove waits, in all: for git, for the apps it stops and for the files they let go of. The time a person
    /// takes to answer its question doesn't count.
    /// </summary>
    public static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    /// <summary>Makes bit remove wait less than <see cref="WaitLimit"/>, never more, in seconds. The tests use it.</summary>
    public const string WaitLimitVariable = "BIT_CLI_REMOVE_WAIT_SECONDS";

    /// <summary>The checks of what deleting the folder would lose get this share of the time, so stopping the apps and deleting have the rest.</summary>
    private const double ChecksShare = 0.3;

    /// <summary>The last second, or half of a shorter time, kept for stopping what still holds the folder and deleting what's left.</summary>
    private static readonly TimeSpan ForcingTime = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(250);

    public static Command Create(Func<CliServices> services)
    {
        var pathArgument = new Argument<string?>("path") { Description = "The folder to delete, any folder. Default: the current folder.", Arity = ArgumentArity.ZeroOrOne };
        var yesOption = new Option<bool>("--yes", "-y") { Description = "Delete it without asking." };

        var command = new Command("remove", "Delete any folder for good, in 10 seconds at most: it stops the apps that keep its files in use, and once it's done waiting, whatever still holds them. A git worktree is taken off its repository's worktrees too.")
        {
            pathArgument,
            yesOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var cli = services();
            var limit = WaitLimitOf(cli);
            var wait = Deadline.After(limit);
            var checks = new GitChecks(cli, wait.Earlier(limit * (1 - ChecksShare)));
            var polite = wait.Earlier(limit / 2 < ForcingTime ? limit / 2 : ForcingTime);
            var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parseResult.GetValue(pathArgument) ?? ".", cli.Environment.CurrentDirectory));

            if (Directory.Exists(folder) is false)
            {
                return cli.UsageError("no-folder", File.Exists(folder) ? $"{folder} is a file, not a folder." : $"{folder} doesn't exist.");
            }

            var realFolder = RealPath(folder);

            if (KeptFolder(cli, folder, realFolder) is { } kept)
            {
                return cli.UsageError("unsafe-folder", $"{folder} {kept}, so bit remove won't delete it.");
            }

            if (await RepositoryOfTheCurrentFolderAsync(checks, realFolder, cancellationToken) is { } current)
            {
                return cli.UsageError("unsafe-folder", $"{folder} holds the git repository of {current}, the folder you're in, so bit remove won't delete it from there. Run it from outside that repository's worktrees.");
            }

            // Deleting a symbolic link or a junction deletes the link alone, which loses nothing of the folder it leads to.
            var git = new DirectoryInfo(folder).LinkTarget is null ? await FindGitAsync(checks, folder, realFolder, cancellationToken) : null;
            var losses = await FindLossesAsync(checks, folder, git, cancellationToken);

            if (checks.TimedOut)
            {
                losses.Add("git didn't answer in time, so bit couldn't check everything deleting it would lose.");
            }

            if (parseResult.GetValue(yesOption))
            {
                WriteLosses(cli, losses);
            }
            else if (cli.IsInteractive is false || cli.Prompter.CanPrompt is false)
            {
                WriteLosses(cli, losses);
                return cli.UsageError("needs-yes", $"Deleting {folder} can't be undone. Pass --yes to delete it.");
            }
            else
            {
                bool confirmed;

                // The time someone takes to answer isn't bit's own waiting
                using (wait.Pause())
                {
                    confirmed = cli.Prompter.Confirm(string.Join('\n', losses.Append(git is { IsLinkedWorktree: true, IsRoot: true } ? $"Delete {folder} and everything in it, and remove it from its repository's worktrees?" : $"Delete {folder} and everything in it?")), false);
                }

                if (confirmed is false)
                {
                    cli.Console.Dim("Nothing was changed.");
                    return CliApp.ExitOk;
                }
            }

            var name = Path.GetFileName(folder);
            cli.Console.Out.MarkupLine($"Removing [bold]{Markup.Escape(name)}[/] [grey]in {Markup.Escape(folder)}[/]");

            var steps = new StepRunner(cli);
            Removal? removal = null;

            var stoppedBuilds = await ProjectProcesses.StopAsync(cli, steps, folder, "the folder's files", cancellationToken, polite);
            LeaveFolder(folder);

            var result = await steps.RunAsync("remove", "Deleting the folder", (progress, token) => Task.Run(() =>
            {
                removal = Delete(cli, folder, stoppedBuilds, polite, wait, progress, token);
                return removal.Result;
            }, token), cancellationToken);

            if (git is { IsLinkedWorktree: true, IsRoot: true } && File.Exists(Path.Combine(folder, ".git")) is false && File.Exists(Path.Combine(git.TopLevel, ".git")) is false)
            {
                await steps.RunAsync("worktree", "Removing it from its repository's worktrees", (_, token) => RemoveWorktreeAsync(cli, git, wait, token), cancellationToken);
            }

            cli.Console.Out.WriteLine();

            if (result.Status is StepStatus.Failed)
            {
                cli.Console.Out.MarkupLine($"  [yellow bold]Some of {Markup.Escape(name)} is left[/]");
                cli.Console.Out.MarkupLine("    Close the apps that use these files, then run bit remove again.");

                if (cli.Log.FilePath is { } logPath)
                {
                    cli.Console.Out.MarkupLine($"    [grey]Every file that's left is in the log:[/] {Markup.Escape(logPath)}");
                }

                return CliApp.ExitFailed;
            }

            cli.Console.Out.MarkupLine($"  [green bold]Removed {Markup.Escape(name)}[/]");

            if (removal is { OnlyEmptyFoldersLeft: true, InFolder: var inFolder })
            {
                var leave = removal.TerminalInFolder && inFolder.Count == 1 ? $"Run cd {ProcessSpec.ShellQuote(Path.GetDirectoryName(folder)!, cli.Environment.Os)}" : $"Close {(inFolder.Count > 1 ? "them" : "it")}";
                cli.Console.Out.MarkupLine($"    [grey]{Markup.Escape(folder)} stays, empty, while {Markup.Escape(Describe(inFolder))} {(inFolder.Count > 1 ? "are" : "is")} in it. {leave}, then delete the folder.[/]");
            }

            return CliApp.ExitOk;
        });

        return command;
    }

    private static TimeSpan WaitLimitOf(CliServices cli)
    {
        return double.TryParse(cli.Environment.GetVariable(WaitLimitVariable), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0 && TimeSpan.FromSeconds(seconds) < WaitLimit
            ? TimeSpan.FromSeconds(seconds)
            : WaitLimit;
    }

    /// <param name="InFolder">The apps that keep the empty folder, this terminal among them.</param>
    private sealed record Removal(StepResult Result, bool OnlyEmptyFoldersLeft, bool TerminalInFolder, IReadOnlyList<string> InFolder);

    /// <summary>
    /// Deletes the folder. Until <paramref name="polite"/> is over, it stops only the build processes that hold its files, as
    /// bit clean does, and while another app holds them it waits, trying again, for the app to let go. Then it stops whatever
    /// still holds them, but bit itself, the terminal it runs in and Windows' own shell, and deletes what's left in the time
    /// <paramref name="wait"/> leaves. When even that isn't enough, the folder is moved aside at once and the rest is deleted
    /// in the background, so bit never waits for a big folder longer than it waits for anything else.
    /// </summary>
    private static Removal Delete(CliServices cli, string folder, IReadOnlySet<int> stoppedBuilds, Deadline polite, Deadline wait, Action<string> progress, CancellationToken cancellationToken)
    {
        var stopped = new List<string>();
        IReadOnlyList<PathFailure> held = [];
        FolderDeletion deletion;

        while (true)
        {
            deletion = FolderRemover.Delete(folder, polite);

            if (deletion.Failures.Count > 0)
            {
                held = deletion.Failures;
            }

            if (deletion.IsGone || deletion.Unfinished)
                break;

            var freed = deletion.Failures.Count > 0 ? ProjectProcesses.Free(cli, deletion.Failures.Select(f => f.Path), stoppedBuilds: stoppedBuilds, deadline: polite) : FreedFiles.None;
            var inIt = deletion.OnlyEmptyFoldersLeft ? ProjectProcesses.FreeFolder(cli, folder, stoppedBuilds, polite) : FreedFiles.None;
            stopped.AddRange([.. freed.Stopped, .. inIt.Stopped]);

            if (polite.IsOver)
                break;

            if (freed.Stopped.Count + inIt.Stopped.Count > 0)
                continue;

            // Waiting helps only while an app holds the folder that bit can stop once it's done waiting
            List<string> holders = [.. freed.Holders, .. inIt.Holders];

            if (holders.Count == 0)
                break;

            var seconds = (int)Math.Ceiling(polite.Left.TotalSeconds);
            progress($"Waiting for {Describe(holders)} to let go of the folder, or bit stops {(holders.Count > 1 ? "them" : "it")} in {seconds}s");
            cancellationToken.WaitHandle.WaitOne(polite.AtMost(RetryInterval));
            cancellationToken.ThrowIfCancellationRequested();
        }

        var forced = FreedFiles.None;

        if (deletion.IsGone is false)
        {
            // The files that were held when bit last got to them, or, when it never did, the folder's own, as many as Windows checks at once
            forced = ProjectProcesses.ForceFree(cli, folder, held.Count > 0 ? held.Select(f => f.Path) : [folder], wait);
            stopped.AddRange(forced.Stopped);
            deletion = FolderRemover.Delete(folder, wait);

            if (deletion.Failures.Count > 0)
            {
                held = deletion.Failures;
            }
        }

        IReadOnlyList<PathFailure> lastFailures = [.. held.Where(f => Path.Exists(f.Path))];

        var after = stopped.Count > 0 ? $"after stopping {string.Join(", ", stopped.Distinct())}" : null;

        if (deletion.Unfinished && deletion.Failures.Count == 0)
        {
            if (FinishInBackground(cli, folder) is { } aside)
            {
                cli.Log.Write($"remove: moved {folder} to {aside}, which is deleted in the background");
                return new(StepResult.Succeeded("Deleted the folder", string.Join("; ", new[] { after, "the last of its files go in the background" }.OfType<string>())), false, false, []);
            }

            lastFailures = lastFailures.Count > 0 ? lastFailures : [new PathFailure(folder, "it took longer than bit waits")];
        }
        else if (deletion.IsGone)
        {
            return new(StepResult.Succeeded("Deleted the folder", after), false, false, []);
        }

        if (deletion.OnlyEmptyFoldersLeft)
        {
            var terminalInFolder = OperatingSystem.IsWindows() && cli.Environment.IsWindows && FolderHolders.AncestorIsIn(folder);
            IReadOnlyList<string> inFolder = terminalInFolder ? [.. forced.Holders, "this terminal"] : forced.Holders;

            return new(StepResult.Warning("Deleted everything in the folder", $"{Describe(inFolder)} {(inFolder.Count > 1 ? "are" : "is")} still in it"), true, terminalInFolder, inFolder);
        }

        foreach (var failure in lastFailures)
        {
            cli.Log.Write($"remove: {failure.Path}: {failure.Reason}");
        }

        var shown = string.Join('\n', lastFailures.Take(ShownFailures).Select(f => f.Path));
        var more = lastFailures.Count > ShownFailures ? $"\nand {lastFailures.Count - ShownFailures} more" : "";
        var inUseBy = forced.InUseBy is { } inUse ? $"\n{inUse}" : "";

        return new(StepResult.Failed("Couldn't delete everything", $"{lastFailures.Count} item{(lastFailures.Count == 1 ? "" : "s")} left", hint: shown + more + inUseBy), false, false, []);
    }

    /// <summary>
    /// Moves the folder aside, to a hidden folder next to it with a name of bit's own, which is instant on the same drive, and
    /// starts deleting that in a process of its own that goes on after bit exits: rd on Windows, rm elsewhere. The name has only
    /// letters, digits, a dot and dashes, so cmd reads it as it is. Null when the folder can't be moved, as while an app still
    /// holds one of its files.
    /// </summary>
    private static string? FinishInBackground(CliServices cli, string folder)
    {
        var parent = Path.GetDirectoryName(folder)!;
        var aside = Path.Combine(parent, $".bit-remove-{Guid.NewGuid().ToString("N")[..8]}");

        try
        {
            Directory.Move(folder, aside);
        }
        catch (Exception exp) when (exp is IOException or UnauthorizedAccessException)
        {
            cli.Log.Write($"remove: couldn't move {folder} aside: {exp.Message}");
            return null;
        }

        try
        {
            File.SetAttributes(aside, File.GetAttributes(aside) | FileAttributes.Hidden);
        }
        catch (Exception exp) when (exp is IOException or UnauthorizedAccessException)
        {
        }

        var name = Path.GetFileName(aside);
        var delete = cli.Environment.IsWindows
            ? new ProcessSpec { FileName = cli.Environment.GetVariable("ComSpec") ?? "cmd.exe", Arguments = ["/d", "/c", "rd", "/s", "/q", name], WorkingDirectory = parent }
            : new ProcessSpec { FileName = "rm", Arguments = ["-rf", "--", name], WorkingDirectory = parent };

        if (cli.Runner.StartDetached(delete) is false)
        {
            cli.Log.Write($"remove: couldn't start deleting {aside} in the background");
        }

        return aside;
    }

    private static string Describe(IReadOnlyList<string> holders) => holders.Count switch
    {
        0 => "an app",
        > ShownFailures => $"{string.Join(", ", holders.Take(ShownFailures))} and {holders.Count - ShownFailures} more",
        _ => string.Join(", ", holders)
    };

    /// <summary>
    /// The folders bit remove never deletes, with the folders that hold one: a drive's root, the home folder and the account's own folders,
    /// the system's folders, and bit itself with the .NET it runs on, whose processes it would stop.
    /// </summary>
    private static string? KeptFolder(CliServices cli, string folder, string realFolder)
    {
        if (Path.GetDirectoryName(folder) is null || Path.GetDirectoryName(realFolder) is null)
            return "is a drive's root";

        // A folder at the top of the file system, like /usr or /home, and on macOS one in /private, where /tmp, /var and /etc lead.
        if (OperatingSystem.IsWindows() is false && Path.GetDirectoryName(realFolder) is "/" or "/private")
            return "is a system folder";

        foreach (var (path, what) in KeptFolders(cli))
        {
            string kept;

            try
            {
                kept = RealPath(path);
            }
            catch (Exception exp) when (exp is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
            {
                continue;
            }

            if (SamePath(kept, realFolder) || SamePath(kept, folder))
                return $"is {what}";

            if (ProjectProcesses.IsUnder(kept, realFolder) || ProjectProcesses.IsUnder(kept, folder))
                return $"holds {what}";
        }

        return null;
    }

    private static IEnumerable<(string Path, string What)> KeptFolders(CliServices cli)
    {
        const string home = "your home folder";
        const string account = "one of your account's own folders";
        const string system = "a system folder";
        const string dotnet = "the .NET that bit runs on";

        yield return (cli.Environment.HomeDirectory, home);
        yield return (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), home);

        foreach (var name in new[] { "Desktop", "Documents", "Downloads", "Music", "Pictures", "Videos" })
        {
            yield return (Path.Combine(cli.Environment.HomeDirectory, name), account);
        }

        foreach (var special in new[] { Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.LocalApplicationData })
        {
            yield return (Environment.GetFolderPath(special), account);
        }

        foreach (var special in new[] { Environment.SpecialFolder.Windows, Environment.SpecialFolder.System, Environment.SpecialFolder.SystemX86, Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.CommonProgramFiles, Environment.SpecialFolder.CommonProgramFilesX86, Environment.SpecialFolder.CommonApplicationData })
        {
            yield return (Environment.GetFolderPath(special), system);
        }

        foreach (var (variable, what) in new[] { ("APPDATA", account), ("LOCALAPPDATA", account), ("SystemRoot", system), ("ProgramFiles", system), ("ProgramFiles(x86)", system), ("ProgramW6432", system), ("ProgramData", system), ("DOTNET_ROOT", dotnet) })
        {
            if (cli.Environment.IsWindows || variable is "DOTNET_ROOT")
            {
                yield return (cli.Environment.GetVariable(variable) ?? "", what);
            }
        }

        yield return (AppContext.BaseDirectory, "bit's own folder");
        yield return (RuntimeEnvironment.GetRuntimeDirectory(), dotnet);
    }

    private static async Task<string?> RepositoryOfTheCurrentFolderAsync(GitChecks git, string realFolder, CancellationToken cancellationToken)
    {
        var current = Path.TrimEndingDirectorySeparator(git.Cli.Environment.CurrentDirectory);

        if (Directory.Exists(current) is false)
            return null;

        var realCurrent = RealPath(current);

        if (SamePath(realCurrent, realFolder) || ProjectProcesses.IsUnder(realCurrent, realFolder))
            return null;

        var common = await git.RunAsync(current, ["rev-parse", "--git-common-dir"], cancellationToken);

        if (common.Succeeded is false || common.OutputLines.FirstOrDefault() is not { Length: > 0 } line)
            return null;

        var repository = RealPath(Path.GetFullPath(line.Trim(), current));

        return SamePath(repository, realFolder) || ProjectProcesses.IsUnder(repository, realFolder) ? current : null;
    }

    private static async Task<GitPlace?> FindGitAsync(GitChecks git, string folder, string realFolder, CancellationToken cancellationToken)
    {
        var place = await git.RunAsync(folder, ["rev-parse", "--show-toplevel", "--absolute-git-dir", "--git-common-dir"], cancellationToken);

        if (place.Succeeded is false || place.OutputLines.ToArray() is not [var topLevel, var gitDirectory, var commonDirectory])
            return null;

        try
        {
            var realTopLevel = RealPath(Path.GetFullPath(topLevel.Trim(), folder));

            return new GitPlace(topLevel.Trim(), SamePath(realTopLevel, realFolder), RealPath(Path.GetFullPath(gitDirectory.Trim(), folder)), RealPath(Path.GetFullPath(commonDirectory.Trim(), folder)), realTopLevel);
        }
        catch (Exception exp) when (exp is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<List<string>> FindLossesAsync(GitChecks checks, string folder, GitPlace? git, CancellationToken cancellationToken)
    {
        var losses = new List<string>();

        if (git is null)
            return losses;

        var status = await checks.RunAsync(folder, ["status", "--porcelain", "--", "."], cancellationToken);

        if (status.Succeeded && string.IsNullOrWhiteSpace(status.Output) is false)
        {
            losses.Add("It has changes that aren't committed.");
        }

        if (git.IsRoot is false || (git.HoldsRepository is false && git.IsLinkedWorktree is false))
            return losses;

        await FindBranchLossesAsync(checks, folder, git, losses, cancellationToken);

        if (git.IsLinkedWorktree)
        {
            if (File.Exists(Path.Combine(git.GitDirectory, "locked")))
            {
                var reason = ReadLockReason(git);
                losses.Add($"It's a locked git worktree{(string.IsNullOrEmpty(reason) ? "" : $": {reason}")}.");
            }

            return losses;
        }

        var others = await checks.RunAsync(folder, ["rev-list", "--count", "--branches", "--not", "--remotes", "HEAD"], cancellationToken);

        if (others.Succeeded && int.TryParse(others.Output.Trim(), out var otherCount) && otherCount > 0)
        {
            losses.Add(otherCount == 1 ? "Other branches have 1 commit that isn't pushed." : $"Other branches have {otherCount} commits that aren't pushed.");
        }

        var stashes = await checks.RunAsync(folder, ["stash", "list"], cancellationToken);

        if (stashes.Succeeded && string.IsNullOrWhiteSpace(stashes.Output) is false)
        {
            losses.Add("Its repository has stashed changes.");
        }

        var worktrees = await checks.RunAsync(folder, ["worktree", "list", "--porcelain"], cancellationToken);
        var linked = worktrees.Succeeded ? LinkedWorktrees(worktrees.Output) : [];

        if (linked.Count > 0)
        {
            var shown = string.Join(", ", linked.Take(ShownFailures)) + (linked.Count > ShownFailures ? $" and {linked.Count - ShownFailures} more" : "");
            losses.Add(linked.Count == 1 ? $"Its repository has another worktree, {shown}, which stops working without it." : $"Its repository has {linked.Count} other worktrees, {shown}, which stop working without it.");
        }

        return losses;
    }

    private static async Task FindBranchLossesAsync(GitChecks checks, string folder, GitPlace git, List<string> losses, CancellationToken cancellationToken)
    {
        var head = await checks.RunAsync(folder, ["symbolic-ref", "--quiet", "--short", "HEAD"], cancellationToken);

        if (head.Succeeded is false)
        {
            var loose = await checks.RunAsync(folder, ["rev-list", "--count", "HEAD", "--not", "--branches", "--tags", "--remotes"], cancellationToken);

            if (loose.Succeeded && int.TryParse(loose.Output.Trim(), out var looseCount) && looseCount > 0)
            {
                losses.Add(looseCount == 1 ? "Its HEAD is detached at 1 commit that no branch or tag has." : $"Its HEAD is detached at {looseCount} commits that no branch or tag has.");
            }

            return;
        }

        var branch = git.IsLinkedWorktree && head.OutputLines.FirstOrDefault() is { Length: > 0 } name ? $"Its branch {name}" : "Its branch";
        var stays = git.IsLinkedWorktree ? $" The branch stays in the repository in {git.Repository}." : "";
        var ahead = await checks.RunAsync(folder, ["rev-list", "--count", "@{u}..HEAD"], cancellationToken);

        if (ahead.Succeeded is false)
        {
            losses.Add(git.IsLinkedWorktree ? $"{branch} has no upstream, so its commits may exist only in the repository in {git.Repository}, where the branch stays." : $"{branch} has no upstream, so its commits may exist only here.");
        }
        else if (int.TryParse(ahead.Output.Trim(), out var count) && count > 0)
        {
            losses.Add((count == 1 ? $"{branch} has 1 commit that isn't pushed." : $"{branch} has {count} commits that aren't pushed.") + stays);
        }
    }

    private static List<string> LinkedWorktrees(string porcelain)
    {
        var linked = new List<string>();
        var blocks = porcelain.Replace("\r", "", StringComparison.Ordinal).Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks.Skip(1))
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.FirstOrDefault(l => l.StartsWith("worktree ", StringComparison.Ordinal)) is { } worktree && lines.Any(l => l == "prunable" || l.StartsWith("prunable ", StringComparison.Ordinal)) is false)
            {
                linked.Add(Path.GetFullPath(worktree["worktree ".Length..]));
            }
        }

        return linked;
    }

    private static string? ReadLockReason(GitPlace git)
    {
        try
        {
            return File.ReadAllText(Path.Combine(git.GitDirectory, "locked")).Trim();
        }
        catch (Exception exp) when (exp is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Takes a linked worktree whose folder is gone, or has no .git file left, off its repository's list of worktrees, so that its branch can be
    /// deleted or checked out elsewhere and its path used again. git worktree remove does it for a folder that's gone, and refuses one that's still
    /// there without a .git file, where git's own administrative folder for the worktree is deleted instead, the same way git worktree remove does it.
    /// git worktree prune isn't used: it would remove every other stale worktree of the repository too.
    /// </summary>
    private static async Task<StepResult> RemoveWorktreeAsync(CliServices cli, GitPlace git, Deadline wait, CancellationToken cancellationToken)
    {
        if (Directory.Exists(git.GitDirectory) is false)
            return StepResult.Succeeded("Removed it from its repository's worktrees", git.Repository);

        // With less than a second left, bit does what git worktree remove would at once
        if (wait.Left >= TimeSpan.FromSeconds(1))
        {
            var removed = await cli.Runner.RunAsync(new ProcessSpec { FileName = "git", Arguments = ["worktree", "remove", "--force", "--force", git.RawTopLevel], WorkingDirectory = git.CommonDirectory, Timeout = wait.Left }, cancellationToken);
            cli.Log.Write($"git worktree remove: {removed.ExitCode} {removed.Output.Trim()}");
        }

        var worktrees = Path.Combine(git.CommonDirectory, "worktrees");

        if (Directory.Exists(git.GitDirectory) && SamePath(Path.GetDirectoryName(git.GitDirectory)!, worktrees))
        {
            // The worktree's own folder points back to the worktree's .git file. When it no longer points to this folder, or a .git file is
            // still where it points, the worktree was moved (git worktree move) while bit asked, to another folder or, where the file system
            // tells cases apart, to another case of this one, and the administrative folder is the moved worktree's, which stays.
            var now = WorktreeOf(git.GitDirectory);

            if (now is null || SamePath(now, git.TopLevel) is false || File.Exists(Path.Combine(now, ".git")))
                return StepResult.Warning("Left its repository's worktrees as they are", now is null ? $"{git.GitDirectory} doesn't say which folder it's for" : $"the worktree is in {now} now");

            FolderRemover.Delete(git.GitDirectory);

            try
            {
                if (Directory.Exists(worktrees) && Directory.EnumerateFileSystemEntries(worktrees).Any() is false)
                {
                    Directory.Delete(worktrees);
                }
            }
            catch (Exception exp) when (exp is IOException or UnauthorizedAccessException)
            {
            }
        }

        return Directory.Exists(git.GitDirectory)
            ? StepResult.Warning("Couldn't remove it from its repository's worktrees", git.Repository, hint: $"Run this in {git.Repository}: git worktree remove {ProcessSpec.Quote(git.RawTopLevel)}")
            : StepResult.Succeeded("Removed it from its repository's worktrees", git.Repository);
    }

    /// <summary>The folder a worktree's administrative folder says the worktree is in, from its gitdir file, which git may write relative to it.</summary>
    private static string? WorktreeOf(string administrativeFolder)
    {
        try
        {
            var gitFile = File.ReadAllText(Path.Combine(administrativeFolder, "gitdir")).Trim();

            return gitFile.Length == 0 ? null : RealPath(Path.GetDirectoryName(Path.GetFullPath(gitFile, administrativeFolder))!);
        }
        catch (Exception exp) when (exp is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>The git commands that check the folder before it's deleted, which end with their deadline: once it's over, they aren't run.</summary>
    private sealed class GitChecks(CliServices cli, Deadline deadline)
    {
        public CliServices Cli => cli;

        public bool TimedOut { get; private set; }

        public async Task<ProcessResult> RunAsync(string folder, string[] arguments, CancellationToken cancellationToken)
        {
            var result = deadline.IsOver
                ? new ProcessResult { ExitCode = -1, TimedOut = true }
                : await cli.Runner.RunAsync(new ProcessSpec { FileName = "git", Arguments = arguments, WorkingDirectory = folder, Timeout = deadline.Left }, cancellationToken);

            TimedOut |= result.TimedOut;
            return result;
        }
    }

    private static void WriteLosses(CliServices cli, IEnumerable<string> losses)
    {
        foreach (var loss in losses)
        {
            cli.Console.Warn(loss);
        }
    }

    private static void LeaveFolder(string folder)
    {
        try
        {
            var current = Path.TrimEndingDirectorySeparator(Environment.CurrentDirectory);

            if (string.Equals(current, folder, StringComparison.OrdinalIgnoreCase) || ProjectProcesses.IsUnder(current, folder))
            {
                Environment.CurrentDirectory = Path.GetDirectoryName(folder)!;
            }
        }
        catch (Exception exp) when (exp is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static bool SamePath(string path, string other) => string.Equals(Path.TrimEndingDirectorySeparator(path), Path.TrimEndingDirectorySeparator(other), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The path with every symbolic link and junction in it followed, the way git reports paths, so /tmp on macOS, a link to /private/tmp, is the same folder as git's /private/tmp.
    /// A link's target is followed the same way, since links may be in its folders too, up to a depth that stops a cycle.
    /// </summary>
    private static string RealPath(string path, int depth = 0)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var root = Path.GetPathRoot(full) ?? "";
        var real = root;

        foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            real = Path.Combine(real, part);

            try
            {
                if (new DirectoryInfo(real).ResolveLinkTarget(returnFinalTarget: true) is { } target)
                {
                    var targetPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target.FullName));
                    real = depth < 32 ? RealPath(targetPath, depth + 1) : targetPath;
                }
            }
            catch (Exception exp) when (exp is IOException or UnauthorizedAccessException)
            {
            }
        }

        return Path.TrimEndingDirectorySeparator(real.Length == 0 ? full : real);
    }

    /// <param name="RawTopLevel">The worktree's folder the way git wrote it, for git's own commands.</param>
    /// <param name="IsRoot">The folder is the top level of its working tree, not a folder inside it.</param>
    private sealed record GitPlace(string RawTopLevel, bool IsRoot, string GitDirectory, string CommonDirectory, string TopLevel)
    {
        public bool IsLinkedWorktree => SamePath(GitDirectory, CommonDirectory) is false;

        /// <summary>The repository is in the folder, so deleting it loses every branch and stash, not only what's in the working tree.</summary>
        public bool HoldsRepository => IsRoot && Directory.Exists(CommonDirectory) && (SamePath(CommonDirectory, TopLevel) || ProjectProcesses.IsUnder(CommonDirectory, TopLevel));

        public string Repository => Path.GetFileName(CommonDirectory) is ".git" ? Path.GetDirectoryName(CommonDirectory)! : CommonDirectory;
    }
}
