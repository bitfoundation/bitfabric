using System.CommandLine;
using Bit.Cli.Infrastructure;
using Bit.Cli.Projects;
using Bit.Cli.Templates;
using Bit.Cli.Tools;
using Spectre.Console;

namespace Bit.Cli.Commands;

/// <summary>
/// bit codespace runs the project on GitHub Codespaces, in the codespace of its repository and branch, which bit reuses, starts
/// when it's stopped and creates only when there's none, with the app's ports forwarded to this machine.
/// </summary>
public static class CodespaceCommand
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(3);

    public static Command Create(Func<CliServices> services)
    {
        var branchOption = new Option<string?>("--branch", "-b") { Description = "The branch whose codespace bit uses. Default: the one checked out here." };
        var codespaceOption = new Option<string?>("--codespace", "-c") { Description = "The codespace to use, by its name, instead of the branch's." };
        var machineOption = new Option<string?>("--machine", "-m") { Description = "The machine type of a new codespace, e.g. premiumLinux. Default: the smallest with a prebuild ready, or else the smallest." };
        var noOpenOption = new Option<bool>("--no-open") { Description = "Don't open the app in the browser once it answers." };
        var yesOption = new Option<bool>("--yes", "-y") { Description = "Push the branch's commits that GitHub doesn't have without asking." };
        var webOption = new Option<bool>("--web", "-w") { Description = "Open it in VS Code for the Web, in the browser, instead of VS Code on this machine." };
        var deleteYesOption = new Option<bool>("--yes", "-y") { Description = "Delete it without asking." };

        var projectOptions = new ProjectOptions().SearchedAbove();

        var command = new Command("codespace", "Run the project on GitHub Codespaces: in its branch's codespace, which bit reuses, starts when it's stopped and creates only when there's none, with the app's ports forwarded to this machine.")
        {
            branchOption,
            codespaceOption,
            machineOption,
            noOpenOption,
            yesOption
        };

        projectOptions.AddTo(command, projectOptions.Project, projectOptions.ServerApi, projectOptions.ServerWeb, projectOptions.AppHost);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var cli = services();
            var found = await FindAsync(cli, parseResult, projectOptions, branchOption, codespaceOption, parseResult.GetValue(yesOption), pushes: true, cancellationToken);

            if (found.Session is not { } session)
                return found.ExitCode;

            var name = found.Codespace?.Name ?? await CreateAsync(cli, session, parseResult.GetValue(machineOption), cancellationToken);

            if (name is null)
                return CliApp.ExitFailed;

            return await RunAsync(cli, session, name, parseResult.GetValue(noOpenOption) is false && cli.HasTerminal, cancellationToken);
        });

        var code = new Command("code", "Open the branch's codespace in VS Code, creating it first when there's none.")
        {
            branchOption,
            codespaceOption,
            machineOption,
            yesOption,
            webOption,
            projectOptions.Project
        };

        code.SetAction(async (parseResult, cancellationToken) =>
        {
            var cli = services();
            var found = await FindAsync(cli, parseResult, projectOptions, branchOption, codespaceOption, parseResult.GetValue(yesOption), pushes: true, cancellationToken);

            if (found.Session is not { } session)
                return found.ExitCode;

            var name = found.Codespace?.Name ?? await CreateAsync(cli, session, parseResult.GetValue(machineOption), cancellationToken);

            if (name is null)
                return CliApp.ExitFailed;

            var opened = await session.Codespaces.OpenAsync(name, parseResult.GetValue(webOption), cancellationToken);

            if (opened.Succeeded)
                return CliApp.ExitOk;

            cli.Console.Fail($"Couldn't open the codespace {name}: {Codespaces.LastLine(opened)}");
            return CliApp.ExitFailed;
        });

        var stop = new Command("stop", "Stop the branch's codespace, which then costs only its storage until bit codespace starts it again.")
        {
            branchOption,
            codespaceOption,
            projectOptions.Project
        };

        stop.SetAction(async (parseResult, cancellationToken) =>
        {
            var cli = services();
            var found = await FindAsync(cli, parseResult, projectOptions, branchOption, codespaceOption, yes: false, pushes: false, cancellationToken);

            if (found is not { Session: { } session, Codespace: { } codespace })
                return found.ExitCode;

            if (codespace.State is "Shutdown")
            {
                cli.Console.Dim($"The codespace {codespace.Name} is stopped already.");
                return CliApp.ExitOk;
            }

            var stopped = await cli.Console.RunWithStatusAsync($"Stopping the codespace {codespace.Name}", _ => session.Codespaces.StopAsync(codespace.Name, cancellationToken));

            if (stopped.Succeeded is false)
            {
                cli.Console.Fail($"Couldn't stop the codespace {codespace.Name}: {Codespaces.LastLine(stopped)}");
                return CliApp.ExitFailed;
            }

            cli.Console.Step(StepStatus.Succeeded, "Stopped the codespace", codespace.Name);
            return CliApp.ExitOk;
        });

        var delete = new Command("delete", "Delete the branch's codespace, with whatever in it isn't pushed.")
        {
            branchOption,
            codespaceOption,
            deleteYesOption,
            projectOptions.Project
        };

        delete.SetAction(async (parseResult, cancellationToken) =>
        {
            var cli = services();
            var found = await FindAsync(cli, parseResult, projectOptions, branchOption, codespaceOption, yes: false, pushes: false, cancellationToken);

            if (found is not { Session: { } session, Codespace: { } codespace })
                return found.ExitCode;

            List<string> losses = [];

            if (codespace.HasUncommittedChanges)
            {
                losses.Add("It has changes that aren't committed.");
            }

            if (codespace.HasUnpushedChanges)
            {
                losses.Add("It has commits that aren't pushed.");
            }

            if (parseResult.GetValue(deleteYesOption))
            {
                foreach (var loss in losses)
                {
                    cli.Console.Warn(loss);
                }
            }
            else if (cli.IsInteractive is false || cli.Prompter.CanPrompt is false)
            {
                foreach (var loss in losses)
                {
                    cli.Console.Warn(loss);
                }

                return cli.UsageError("needs-yes", $"Deleting the codespace {codespace.Name} can't be undone. Pass --yes to delete it.");
            }
            else if (cli.Prompter.Confirm(string.Join('\n', losses.Append($"Delete the codespace {codespace.Name}?")), false) is false)
            {
                cli.Console.Dim("Nothing was changed.");
                return CliApp.ExitOk;
            }

            var deleted = await cli.Console.RunWithStatusAsync($"Deleting the codespace {codespace.Name}", _ => session.Codespaces.DeleteAsync(codespace.Name, cancellationToken));

            if (deleted.Succeeded is false)
            {
                cli.Console.Fail($"Couldn't delete the codespace {codespace.Name}: {Codespaces.LastLine(deleted)}");
                return CliApp.ExitFailed;
            }

            cli.Console.Step(StepStatus.Succeeded, "Deleted the codespace", codespace.Name);
            return CliApp.ExitOk;
        });

        command.Subcommands.Add(code);
        command.Subcommands.Add(stop);
        command.Subcommands.Add(delete);

        return command;
    }

    private sealed record Session(Codespaces Codespaces, ProjectContext Project, string RelativeFolder, string Branch);

    /// <summary>The session and the codespace it found, if any, or the exit code to stop with when it has no session.</summary>
    private sealed record Found(int ExitCode, Session? Session = null, Codespace? Codespace = null);

    /// <summary>
    /// What every subcommand needs first: the project, its repository on GitHub, the branch, the GitHub CLI signed in with the
    /// codespace scope, and, for the ones that run it, the branch's commits on GitHub, which a codespace checks out. Then the
    /// codespace: the one named with --codespace, or else the branch's.
    /// </summary>
    private static async Task<Found> FindAsync(CliServices cli, ParseResult parseResult, ProjectOptions projectOptions, Option<string?> branchOption, Option<string?> codespaceOption, bool yes, bool pushes, CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(cli, parseResult, projectOptions, branchOption, yes, pushes, cancellationToken);

        if (prepared.Session is not { } session)
            return prepared;

        var (list, error) = await cli.Console.RunWithStatusAsync($"Looking for the codespace of {session.Branch}", _ => session.Codespaces.ListAsync(cancellationToken));

        if (list is null)
        {
            cli.Console.Fail(error!);
            return new(CliApp.ExitFailed);
        }

        var given = parseResult.GetValue(codespaceOption);
        var codespace = given is null ? Codespaces.Pick(list, session.Branch) : list.FirstOrDefault(c => c.Name == given);

        if (given is not null && codespace is null)
            return new(cli.UsageError("no-codespace", $"{session.Codespaces.Repository} has no codespace named {given}."));

        if (codespace is null && pushes is false)
        {
            cli.Console.Dim($"{session.Codespaces.Repository} has no codespace of {session.Branch}.");
            return new(CliApp.ExitOk);
        }

        if (codespace is not null)
        {
            cli.Console.Step(StepStatus.Succeeded, $"Found the codespace of {codespace.Branch ?? session.Branch}", $"{codespace.Name}, {(codespace.IsAvailable ? "running" : "stopped")}");
        }

        return new(CliApp.ExitOk, session, codespace);
    }

    private static async Task<Found> PrepareAsync(CliServices cli, ParseResult parseResult, ProjectOptions projectOptions, Option<string?> branchOption, bool yes, bool pushes, CancellationToken cancellationToken)
    {
        if (cli.Environment.GetVariable("CODESPACES") is "true")
            return new(cli.UsageError("in-a-codespace", "This is a codespace already: run the project here with bit run."));

        var directory = projectOptions.ProjectDirectory(parseResult, cli, out var folder);

        if (directory is null || ProjectContext.FindProjectName(directory) is not { } name)
            return new(cli.UsageError("not-a-project", ProjectOptions.NotInAProject(folder)));

        var (parts, partsError) = projectOptions.Read(parseResult, cli, directory);

        if (partsError is not null)
            return new(cli.UsageError("project-part-missing", partsError));

        var top = await Git(cli, directory, ["rev-parse", "--show-toplevel"], cancellationToken);

        if (top.Succeeded is false || top.OutputLines.FirstOrDefault() is not { Length: > 0 } topLevel)
            return new(cli.UsageError("not-a-repository", $"{directory} isn't in a git repository, and a codespace runs a repository on GitHub. bit new --github-repo creates one, and gh repo create pushes an existing one."));

        var root = Path.GetFullPath(topLevel.Trim());
        var origin = await Git(cli, root, ["remote", "get-url", "origin"], cancellationToken);

        if (RepositoryAddress.Parse(origin.Output.Trim()) is not { } address || address.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) is false)
            return new(cli.UsageError("not-on-github", origin.Succeeded
                ? $"The repository's origin, {origin.Output.Trim()}, isn't on github.com, and GitHub Codespaces runs repositories on GitHub."
                : "The repository has no origin. Push it to GitHub first, e.g. with gh repo create."));

        var current = await Git(cli, root, ["symbolic-ref", "--quiet", "--short", "HEAD"], cancellationToken);
        var currentBranch = current.Succeeded ? current.Output.Trim() : null;
        var branch = parseResult.GetValue(branchOption) ?? currentBranch;

        if (string.IsNullOrEmpty(branch))
            return new(cli.UsageError("no-branch", "HEAD is detached here. Check out a branch, or name one with --branch."));

        if (await GitHubCliAsync(cli, root, cancellationToken) is false)
            return new(CliApp.ExitFailed);

        if (pushes && branch == currentBranch && await PushAsync(cli, root, branch, yes, cancellationToken) is false)
            return new(CliApp.ExitFailed);

        var project = new ProjectContext { Name = name, Directory = directory, Platforms = new HashSet<Platform> { Platform.Web }, Parts = parts };

        return new(CliApp.ExitOk, new Session(new Codespaces(cli, root, address.Path), project, Path.GetRelativePath(root, directory).Replace('\\', '/'), branch));
    }

    /// <summary>The GitHub CLI, installed when it's missing, as bit setup installs it, and signed in to github.com, in the browser when there's a terminal.</summary>
    private static async Task<bool> GitHubCliAsync(CliServices cli, string root, CancellationToken cancellationToken)
    {
        if (cli.Runner.FindExecutable("gh") is null)
        {
            var context = NewWorkflow.CreateToolContext(cli, new ToolNeeds { Codespaces = true, Ide = IdeLocator.None });
            var checks = await cli.Console.RunWithStatusAsync("Checking this machine for the GitHub CLI", _ => ToolCatalog.CheckAsync(context, cancellationToken, ["gh", "homebrew"]));
            var missing = checks.Where(c => c.Status.IsSatisfied is false).ToList();

            if (missing.Any(c => c.Tool.Id is "gh"))
            {
                await new ToolInstaller(cli, new StepRunner(cli)).InstallAsync(missing, context, cancellationToken);
            }

            if (cli.Runner.FindExecutable("gh") is null)
            {
                cli.Console.Fail("bit codespace works through the GitHub CLI, which isn't installed. Install it from https://cli.github.com, then run this again.");
                return false;
            }
        }

        if ((await Gh(cli, root, ["auth", "status", "--hostname", "github.com"], cancellationToken)).Succeeded)
            return true;

        if (cli.HasTerminal)
        {
            cli.Console.Out.MarkupLine("  [grey]Sign in to GitHub in your browser, so that bit can run the project in a codespace.[/]");
            await cli.Runner.RunAsync(new ProcessSpec { FileName = "gh", Arguments = ["auth", "login", "--web", "--git-protocol", "https", "--hostname", "github.com", "--scopes", "codespace"], WorkingDirectory = root, Interactive = true, Timeout = TimeSpan.FromMinutes(15) }, cancellationToken);

            if ((await Gh(cli, root, ["auth", "status", "--hostname", "github.com"], cancellationToken)).Succeeded)
                return true;
        }

        cli.Console.Fail("You aren't signed in to GitHub. Run gh auth login --web --scopes codespace, then this again.");
        return false;
    }

    /// <summary>
    /// A codespace checks the branch out from GitHub, so commits that are only on this machine aren't in it: bit pushes them,
    /// after asking, or at once with --yes. A branch GitHub doesn't have yet can't be run at all without that. Changes that
    /// aren't committed stay here, and bit says so.
    /// </summary>
    private static async Task<bool> PushAsync(CliServices cli, string root, string branch, bool yes, CancellationToken cancellationToken)
    {
        var status = await Git(cli, root, ["status", "--porcelain"], cancellationToken);

        if (status.Succeeded && string.IsNullOrWhiteSpace(status.Output) is false)
        {
            cli.Console.Warn("Changes that aren't committed stay on this machine: the codespace runs the commits GitHub has.");
        }

        var upstream = await Git(cli, root, ["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], cancellationToken);
        var ahead = upstream.Succeeded ? await Git(cli, root, ["rev-list", "--count", "@{u}..HEAD"], cancellationToken) : null;
        var count = ahead is { Succeeded: true } && int.TryParse(ahead.Output.Trim(), out var commits) ? commits : 0;

        if (upstream.Succeeded && count == 0)
            return true;

        var them = count == 1 ? "it" : "them";
        var question = upstream.Succeeded
            ? $"{branch} has {(count == 1 ? "1 commit" : $"{count} commits")} that GitHub doesn't have, so the codespace wouldn't run {them}. Push {them}?"
            : $"GitHub doesn't have {branch} yet, so a codespace can't check it out. Push it?";

        if (yes is false && (cli.IsInteractive is false || cli.Prompter.CanPrompt is false || cli.Prompter.Confirm(question, true) is false))
        {
            if (upstream.Succeeded)
            {
                cli.Console.Warn($"The codespace runs {branch} as GitHub has it, without the {(count == 1 ? "commit" : $"{count} commits")} that aren't pushed.");
                return true;
            }

            cli.Console.Fail($"GitHub doesn't have {branch} yet, so a codespace can't check it out. Push it with git push -u origin {branch}, or pass --yes to let bit push it.");
            return false;
        }

        var pushed = await cli.Console.RunWithStatusAsync($"Pushing {branch}", _ => cli.Runner.RunAsync(new ProcessSpec
        {
            FileName = "git",
            Arguments = upstream.Succeeded ? ["push"] : ["push", "--set-upstream", "origin", branch],
            WorkingDirectory = root,
            Timeout = TimeSpan.FromMinutes(5)
        }, cancellationToken));

        if (pushed.Succeeded)
        {
            cli.Console.Step(StepStatus.Succeeded, $"Pushed {branch}", "so the codespace has its commits");
            return true;
        }

        cli.Console.Fail($"Couldn't push {branch}: {Codespaces.LastLine(pushed)}", pushed.Spec?.Attempt);
        return false;
    }

    private static async Task<string?> CreateAsync(CliServices cli, Session session, string? machine, CancellationToken cancellationToken)
    {
        string? name = null;

        await new StepRunner(cli).RunAsync("codespace-create", $"Creating a codespace of {session.Branch}", async (progress, token) =>
        {
            var picked = machine is null ? await session.Codespaces.MachineAsync(session.Branch, token) : null;
            var (created, result) = await session.Codespaces.CreateAsync(session.Branch, machine ?? picked?.Name, Codespaces.DisplayName(session.Project.Name, session.Branch), progress, token);
            name = created;

            if (created is null)
                return StepResult.FromProcess(result, "", "Couldn't create a codespace", null) with { Hint = Codespaces.LastLine(result) };

            return StepResult.Succeeded("Created a codespace", string.Join(", ", new[] { created, picked?.DisplayName ?? machine, picked?.PrebuildReady is true ? "from a prebuild" : null }.OfType<string>()));
        }, cancellationToken);

        return name;
    }

    /// <summary>
    /// Runs the project in the codespace, in this terminal, with the app's ports forwarded to the same ones here, and opens the
    /// app once it answers through them. Once bit run there stops, with Ctrl+C or this terminal closing, the forwarding stops
    /// too; the codespace keeps running until GitHub stops it for being idle.
    /// </summary>
    private static async Task<int> RunAsync(CliServices cli, Session session, string codespace, bool open, CancellationToken cancellationToken)
    {
        var ports = Codespaces.Ports(session.Project);
        var appUrl = ProjectSteps.ReadLaunchUrl(session.Project.ServerWebDirectory);

        cli.Console.Out.WriteLine();
        cli.Console.Out.MarkupLine($"  Running [bold]{Markup.Escape(session.Project.Name)}[/] in the codespace {Markup.Escape(codespace)}. [grey]Ctrl+C stops it.[/]");

        if (ports.Count > 0)
        {
            cli.Console.Out.MarkupLine($"  [grey]Its ports {string.Join(", ", ports)} are forwarded to this machine, so the addresses it shows work here{(appUrl is null ? "" : $": the app is at {Markup.Escape(appUrl)}")}.[/]");
        }

        cli.Console.Out.WriteLine();

        using var forwarding = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var forward = ForwardAsync();
        var opening = OpenAsync();

        var run = await session.Codespaces.RunAsync(codespace, Codespaces.RemoteScript(session.Codespaces.RepositoryName, session.RelativeFolder), cancellationToken);

        await forwarding.CancelAsync();
        var forwarded = await forward;
        await opening;

        cli.Console.Out.WriteLine();

        if (forwarded is { Succeeded: false, TimedOut: false } && forwarded.OutputLines.Any())
        {
            cli.Console.Warn($"Couldn't forward the codespace's ports: {Codespaces.LastLine(forwarded)}");
        }

        if (run.NotFound)
        {
            cli.Console.Fail("bit codespace works through the GitHub CLI, which isn't installed.");
            return CliApp.ExitFailed;
        }

        cli.Console.Dim($"The codespace {codespace} keeps running until it's idle for its timeout, 30 minutes unless you changed it. bit codespace stop stops it now, and bit codespace delete deletes it.");

        // 130 is Ctrl+C, which is how bit run is stopped
        return run.ExitCode is 0 or 130 ? CliApp.ExitOk : CliApp.ExitFailed;

        async Task<ProcessResult?> ForwardAsync()
        {
            try
            {
                return ports.Count > 0 ? await session.Codespaces.ForwardAsync(codespace, ports, forwarding.Token) : null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        async Task OpenAsync()
        {
            try
            {
                if (open && appUrl is not null)
                {
                    await OpenWhenItAnswersAsync(cli, new Uri(appUrl), forwarding.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task OpenWhenItAnswersAsync(CliServices cli, Uri url, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        while (cancellationToken.IsCancellationRequested is false)
        {
            try
            {
                using var response = await client.GetAsync(url, cancellationToken);

                if ((int)response.StatusCode < 500)
                {
                    ProjectRun.OpenInBrowser(cli, url.ToString());
                    return;
                }
            }
            catch (Exception exp) when (exp is HttpRequestException || (exp is TaskCanceledException && cancellationToken.IsCancellationRequested is false))
            {
            }

            await Task.Delay(ProbeInterval, cancellationToken);
        }
    }

    private static Task<ProcessResult> Git(CliServices cli, string folder, string[] arguments, CancellationToken cancellationToken)
    {
        return cli.Runner.RunAsync(new ProcessSpec { FileName = "git", Arguments = arguments, WorkingDirectory = folder, Timeout = TimeSpan.FromSeconds(30) }, cancellationToken);
    }

    private static Task<ProcessResult> Gh(CliServices cli, string folder, string[] arguments, CancellationToken cancellationToken)
    {
        return cli.Runner.RunAsync(new ProcessSpec { FileName = "gh", Arguments = arguments, WorkingDirectory = folder, Timeout = TimeSpan.FromMinutes(1) }, cancellationToken);
    }
}
