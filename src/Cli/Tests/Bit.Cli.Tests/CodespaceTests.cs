using Bit.Cli.Infrastructure;
using Bit.Cli.Projects;
using Bit.Cli.Tests.Infrastructure;

namespace Bit.Cli.Tests;

[TestClass]
public class CodespaceTests
{
    private const string Repository = "contoso/Contoso";

    [TestMethod]
    public async Task Codespace_Should_RunTheProjectInTheBranchsOwnCodespace()
    {
        using var host = new TestHost(HostOs.Linux);
        var folder = CreateProject(host);
        OnGitHub(host, folder, "main");
        host.Runner.On("gh", "codespace list", 0, List(
            Space("cart-codespace", "cart", "Available", "2026-11-24T10:00:00Z"),
            Space("old-main-codespace", "main", "Shutdown", "2026-11-20T10:00:00Z"),
            Space("main-codespace", "main", "Shutdown", "2026-11-24T09:00:00Z")));

        Assert.AreEqual(0, await host.RunAsync("codespace", "--project", folder), host.Output);

        Assert.IsFalse(host.Runner.Calls.Any(c => IsGh(c, "codespace create")), "A branch that has a codespace gets no other.");
        CollectionAssert.AreEqual(new[] { "codespace", "list", "--repo", Repository, "--json", "name,displayName,gitStatus,state,lastUsedAt", "--limit", "100" }, host.Runner.Calls.Single(c => IsGh(c, "codespace list")).Arguments.ToArray());

        var ssh = host.Runner.Calls.Single(c => IsGh(c, "codespace ssh"));
        CollectionAssert.AreEqual(new[] { "codespace", "ssh", "--codespace", "main-codespace", "--", "-tt" }, ssh.Arguments.Take(6).ToArray(), "The newest codespace of main, on a terminal of its own.");
        Assert.IsTrue(ssh.Interactive && ssh.TakesOver, "bit run in the codespace has this terminal.");
        StringAssert.StartsWith(ssh.Arguments[^1], "cd /workspaces/Contoso || exit 1; ");
        StringAssert.EndsWith(ssh.Arguments[^1], "; exec bit run");

        var forward = host.Runner.Calls.Single(c => IsGh(c, "codespace ports forward"));
        CollectionAssert.AreEqual(new[] { "codespace", "ports", "forward", "5030:5030", "2030:2030", "--codespace", "main-codespace" }, forward.Arguments.ToArray(), "The app's and the dashboard's ports, to the same ones here.");
        StringAssert.Contains(host.FlatOutput, "Found the codespace of main main-codespace, stopped");
        StringAssert.Contains(host.FlatOutput, "the app is at http://localhost:5030");
    }

    [TestMethod]
    public async Task Codespace_Should_CreateOneOnlyWhenTheBranchHasNone()
    {
        using var host = new TestHost(HostOs.Linux);
        var folder = CreateProject(host);
        OnGitHub(host, folder, "main");
        host.Runner.On("gh", "codespace list", 0, List(Space("cart-codespace", "cart", "Available", "2026-11-24T10:00:00Z")));
        host.Runner.On("gh", "api repos/contoso/Contoso/codespaces/machines?ref=main", 0, """
            {"total_count":3,"machines":[
              {"name":"premiumLinux","display_name":"8 cores, 32 GB RAM, 64 GB storage","cpus":8,"memory_in_bytes":34359738368,"prebuild_availability":"ready"},
              {"name":"basicLinux32gb","display_name":"2 cores, 8 GB RAM, 32 GB storage","cpus":2,"memory_in_bytes":8589934592,"prebuild_availability":null},
              {"name":"standardLinux32gb","display_name":"4 cores, 16 GB RAM, 32 GB storage","cpus":4,"memory_in_bytes":17179869184,"prebuild_availability":"ready"}]}
            """);
        host.Runner.On("gh", "codespace create", 0, "  ✓ Codespaces usage for this repository is paid for by contoso\nfuzzy-space-guacamole-x5\n");

        Assert.AreEqual(0, await host.RunAsync("codespace", "--project", folder), host.Output);

        CollectionAssert.AreEqual(
            new[] { "codespace", "create", "--repo", Repository, "--branch", "main", "--display-name", "Contoso main", "--default-permissions", "--machine", "standardLinux32gb" },
            host.Runner.Calls.Single(c => IsGh(c, "codespace create")).Arguments.ToArray(),
            "The smallest machine with a prebuild ready.");
        Assert.AreEqual("fuzzy-space-guacamole-x5", host.Runner.Calls.Single(c => IsGh(c, "codespace ssh")).Arguments[3]);
        StringAssert.Contains(host.FlatOutput, "Created a codespace fuzzy-space-guacamole-x5, 4 cores");
    }

    [TestMethod]
    public async Task Codespace_Should_CreateItOnTheMachineItIsGiven()
    {
        using var host = new TestHost(HostOs.Linux);
        var folder = CreateProject(host);
        OnGitHub(host, folder, "main");
        host.Runner.On("gh", "codespace list", 0, "[]");
        host.Runner.On("gh", "codespace create", 0, "big-space-1\n");

        Assert.AreEqual(0, await host.RunAsync("codespace", "--project", folder, "--machine", "largePremiumLinux"), host.Output);

        Assert.IsFalse(host.Runner.Calls.Any(c => IsGh(c, "api")), "bit doesn't pick a machine when it's given one.");
        CollectionAssert.Contains(host.Runner.Calls.Single(c => IsGh(c, "codespace create")).Arguments.ToArray(), "largePremiumLinux");
    }

    [TestMethod]
    public void Codespace_Should_PickTheSmallestMachineWhenNoneHasAPrebuild()
    {
        var machines = Codespaces.ParseMachines("""
            {"machines":[{"name":"premiumLinux","cpus":8,"memory_in_bytes":34359738368},{"name":"standardLinux32gb","cpus":4,"memory_in_bytes":17179869184,"prebuild_availability":"in_progress"}]}
            """);

        Assert.AreEqual("standardLinux32gb", Codespaces.PickMachine(machines)?.Name);
        Assert.IsNull(Codespaces.PickMachine([]));
    }

    [TestMethod]
    public void Codespace_Should_PreferTheBranchsRunningCodespace()
    {
        var codespaces = Codespaces.ParseList(List(
            Space("stopped-newer", "main", "Shutdown", "2026-11-24T10:00:00Z"),
            Space("running-older", "main", "Available", "2026-11-01T10:00:00Z"),
            Space("other-branch", "cart", "Available", "2026-11-25T10:00:00Z")));

        Assert.AreEqual("running-older", Codespaces.Pick(codespaces, "main")?.Name);
        Assert.IsNull(Codespaces.Pick(codespaces, "checkout"));
        Assert.AreEqual("Contoso feature/a-branch-name-that-is-far-too-lo", Codespaces.DisplayName("Contoso", "feature/a-branch-name-that-is-far-too-long-for-github"));
    }

    [TestMethod]
    public async Task Codespace_Should_PushABranchGitHubDoesntHaveWithYes()
    {
        using var host = new TestHost(HostOs.Linux);
        var folder = CreateProject(host);
        OnGitHub(host, folder, "cart");
        host.Runner.On("git", "rev-parse --abbrev-ref --symbolic-full-name @{u}", 128, "fatal: no upstream configured for branch 'cart'");
        host.Runner.On("gh", "codespace list", 0, List(Space("cart-codespace", "cart", "Available", "2026-11-24T10:00:00Z")));

        Assert.AreEqual(CliApp.ExitFailed, await host.RunAsync("codespace", "--project", folder));

        StringAssert.Contains(host.FlatOutput, "GitHub doesn't have cart yet");
        Assert.IsFalse(host.Runner.Calls.Any(c => IsGh(c, "codespace")), "Without the branch on GitHub there's no codespace to run it in.");

        host.Runner.Calls.Clear();
        Assert.AreEqual(0, await host.RunAsync("codespace", "--project", folder, "--yes"), host.Output);

        CollectionAssert.AreEqual(new[] { "push", "--set-upstream", "origin", "cart" }, host.Runner.Calls.Single(c => c.FileName is "git" && c.Arguments[0] is "push").Arguments.ToArray());
        Assert.IsTrue(host.Runner.Calls.FindIndex(c => c.FileName is "git" && c.Arguments[0] is "push") < host.Runner.Calls.FindIndex(c => IsGh(c, "codespace ssh")), "bit pushes before it runs the codespace.");
    }

    [TestMethod]
    public async Task Codespace_Should_AskToPushCommitsGitHubDoesntHave()
    {
        var prompter = new ScriptedPrompter().On("Push them?", true);
        using var host = new TestHost(HostOs.Linux, prompter: prompter);
        var folder = CreateProject(host);
        OnGitHub(host, folder, "main");
        host.Runner.On("git", "rev-list --count @{u}..HEAD", 0, "2\n");
        host.Runner.On("git", "status --porcelain", 0, " M README.md\n");
        host.Runner.On("gh", "codespace list", 0, List(Space("main-codespace", "main", "Available", "2026-11-24T10:00:00Z")));

        Assert.AreEqual(0, await host.RunAsync("codespace", "--project", folder, "--no-open"), host.Output);

        Assert.AreEqual("main has 2 commits that GitHub doesn't have, so the codespace wouldn't run them. Push them?", prompter.Prompts.Single().Question);
        CollectionAssert.AreEqual(new[] { "push" }, host.Runner.Calls.Single(c => c.FileName is "git" && c.Arguments[0] is "push").Arguments.ToArray());
        StringAssert.Contains(host.FlatOutput, "Changes that aren't committed stay on this machine");
    }

    [TestMethod]
    public async Task Codespace_Should_AskGitHubForTheCodespaceScope()
    {
        using var host = new TestHost(HostOs.Linux, prompter: new ScriptedPrompter());
        var folder = CreateProject(host);
        OnGitHub(host, folder, "main");
        var refreshed = false;
        host.Runner.On("gh", "auth refresh", _ =>
        {
            refreshed = true;
            return new ProcessResult { ExitCode = 0 };
        });
        host.Runner.On("gh", "codespace list", _ => refreshed
            ? new ProcessResult { ExitCode = 0, Output = List(Space("main-codespace", "main", "Available", "2026-11-24T10:00:00Z")) }
            : new ProcessResult { ExitCode = 1, Output = "error getting codespaces: HTTP 403: Must have admin rights to Repository.\nThis API operation needs the \"codespace\" scope. To request it, run:  gh auth refresh -h github.com -s codespace\n" });

        Assert.AreEqual(0, await host.RunAsync("codespace", "--project", folder, "--no-open"), host.Output);

        var refresh = host.Runner.Calls.Single(c => IsGh(c, "auth refresh"));
        CollectionAssert.AreEqual(new[] { "auth", "refresh", "--hostname", "github.com", "--scopes", "codespace" }, refresh.Arguments.ToArray());
        Assert.IsTrue(refresh.Interactive, "GitHub's sign-in shows its code in this terminal.");
        Assert.AreEqual(2, host.Runner.Calls.Count(c => IsGh(c, "codespace list")));
    }

    [TestMethod]
    public async Task Codespace_Should_SayHowToGetTheCodespaceScopeWithoutATerminal()
    {
        using var host = new TestHost(HostOs.Linux);
        var folder = CreateProject(host);
        OnGitHub(host, folder, "main");
        host.Runner.On("gh", "codespace list", 1, "This API operation needs the \"codespace\" scope. To request it, run:  gh auth refresh -h github.com -s codespace\n");

        Assert.AreEqual(CliApp.ExitFailed, await host.RunAsync("codespace", "--project", folder));

        StringAssert.Contains(host.FlatOutput, "Run gh auth refresh -h github.com -s codespace");
        Assert.IsFalse(host.Runner.Calls.Any(c => IsGh(c, "auth refresh")));
    }

    [TestMethod]
    [DataRow("https://gitlab.com/contoso/Contoso.git", "isn't on github.com")]
    [DataRow("", "has no origin")]
    public async Task Codespace_Should_NeedARepositoryOnGitHub(string origin, string says)
    {
        using var host = new TestHost(HostOs.Linux);
        var folder = CreateProject(host);
        OnGitHub(host, folder, "main");
        host.Runner.On("git", "remote get-url origin", origin.Length == 0 ? 2 : 0, origin.Length == 0 ? "error: No such remote 'origin'" : origin);

        Assert.AreEqual(CliApp.ExitUsage, await host.RunAsync("codespace", "--project", folder));

        StringAssert.Contains(host.FlatOutput, says);
        Assert.IsFalse(host.Runner.Calls.Any(c => c.FileName is "gh"));
    }

    [TestMethod]
    public async Task Codespace_Should_SayTheProjectRunsHereInACodespace()
    {
        using var host = new TestHost(HostOs.Linux, variables: new Dictionary<string, string> { ["CODESPACES"] = "true" });
        var folder = CreateProject(host);

        Assert.AreEqual(CliApp.ExitUsage, await host.RunAsync("codespace", "--project", folder));

        StringAssert.Contains(host.FlatOutput, "run the project here with bit run");
        Assert.IsEmpty(host.Runner.Calls);
    }

    [TestMethod]
    public async Task Codespace_Stop_Should_StopTheBranchsCodespace()
    {
        using var host = new TestHost(HostOs.Linux);
        var folder = CreateProject(host);
        OnGitHub(host, folder, "main");
        host.Runner.On("gh", "codespace list", 0, List(Space("main-codespace", "main", "Available", "2026-11-24T10:00:00Z")));

        Assert.AreEqual(0, await host.RunAsync("codespace", "stop", "--project", folder), host.Output);

        CollectionAssert.AreEqual(new[] { "codespace", "stop", "--codespace", "main-codespace" }, host.Runner.Calls.Single(c => IsGh(c, "codespace stop")).Arguments.ToArray());
        Assert.IsFalse(host.Runner.Calls.Any(c => c.FileName is "git" && c.Arguments[0] is "push"), "Stopping a codespace pushes nothing.");
    }

    [TestMethod]
    public async Task Codespace_Stop_Should_CreateNothing()
    {
        using var host = new TestHost(HostOs.Linux);
        var folder = CreateProject(host);
        OnGitHub(host, folder, "main");
        host.Runner.On("gh", "codespace list", 0, "[]");

        Assert.AreEqual(0, await host.RunAsync("codespace", "stop", "--project", folder), host.Output);

        StringAssert.Contains(host.FlatOutput, "contoso/Contoso has no codespace of main.");
        Assert.IsFalse(host.Runner.Calls.Any(c => IsGh(c, "codespace create") || IsGh(c, "codespace stop")));
    }

    [TestMethod]
    public async Task Codespace_Delete_Should_SayWhatWouldBeLostFirst()
    {
        var prompter = new ScriptedPrompter().On("Delete the codespace", true);
        using var host = new TestHost(HostOs.Linux, prompter: prompter);
        var folder = CreateProject(host);
        OnGitHub(host, folder, "main");
        host.Runner.On("gh", "codespace list", 0, List(Space("main-codespace", "main", "Available", "2026-11-24T10:00:00Z", uncommitted: true)));

        Assert.AreEqual(0, await host.RunAsync("codespace", "delete", "--project", folder), host.Output);

        Assert.AreEqual("It has changes that aren't committed.\nDelete the codespace main-codespace?", prompter.Prompts.Single().Question);
        Assert.AreEqual("no", prompter.Prompts.Single().Preselected.Single());
        CollectionAssert.AreEqual(new[] { "codespace", "delete", "--codespace", "main-codespace", "--force" }, host.Runner.Calls.Single(c => IsGh(c, "codespace delete")).Arguments.ToArray());
    }

    [TestMethod]
    public async Task Codespace_Delete_Should_NeedYesWithoutATerminal()
    {
        using var host = new TestHost(HostOs.Linux);
        var folder = CreateProject(host);
        OnGitHub(host, folder, "main");
        host.Runner.On("gh", "codespace list", 0, List(Space("main-codespace", "main", "Available", "2026-11-24T10:00:00Z")));

        Assert.AreEqual(CliApp.ExitUsage, await host.RunAsync("codespace", "delete", "--project", folder));
        Assert.IsFalse(host.Runner.Calls.Any(c => IsGh(c, "codespace delete")));

        Assert.AreEqual(0, await host.RunAsync("codespace", "delete", "--project", folder, "--yes"), host.Output);
        Assert.IsTrue(host.Runner.Calls.Any(c => IsGh(c, "codespace delete")));
    }

    [TestMethod]
    public void Codespace_Should_RunBitRunInTheProjectsFolderOfTheWorkspace()
    {
        var script = Codespaces.RemoteScript("shop", "src/Shop");

        StringAssert.StartsWith(script, "cd /workspaces/shop/src/Shop || exit 1; ");
        StringAssert.Contains(script, "export PATH=$PATH:$HOME/.dotnet/tools:$HOME/.aspire/bin");
        StringAssert.Contains(script, "GIT_TERMINAL_PROMPT=0 git pull --ff-only");
        StringAssert.EndsWith(script, "; exec bit run");
        Assert.DoesNotContain("bit setup", script, "The script looks for bit setup with a pattern that doesn't find the script itself.");
        Assert.DoesNotContain("\"", script, "gh, ssh and the shell each read the line again, so it has no double quotes.");
        Assert.DoesNotContain("\n", script);
        StringAssert.StartsWith(Codespaces.RemoteScript("shop", "."), "cd /workspaces/shop || exit 1; ");
        StringAssert.StartsWith(Codespaces.RemoteScript("shop", "my apps/Shop"), "cd '/workspaces/shop/my apps/Shop' || exit 1; ");
    }

    [TestMethod]
    public void Codespace_Should_ForwardTheStandaloneApisPortToo()
    {
        using var host = new TestHost(HostOs.Linux);
        var folder = CreateProject(host);
        LaunchSettings(Path.Combine(folder, "src", "Server", "Contoso.Server.Api"), "http://localhost:5031;http://*:5031");

        CollectionAssert.AreEqual(new[] { 5030, 5031, 2030 }, Codespaces.Ports(ProjectContext.At(folder, "Contoso")).ToArray());
    }

    private static bool IsGh(ProcessSpec spec, string arguments) => spec.FileName is "gh" && string.Join(' ', spec.Arguments).StartsWith(arguments, StringComparison.Ordinal);

    /// <summary>Answers git and the GitHub CLI as for a clone of contoso/Contoso on GitHub, with the branch checked out and pushed, signed in to GitHub.</summary>
    private static void OnGitHub(TestHost host, string folder, string branch)
    {
        host.Runner.Executables["gh"] = "/usr/bin/gh";
        host.Runner.On("git", "rev-parse --show-toplevel", 0, $"{folder.Replace('\\', '/')}\n");
        host.Runner.On("git", "remote get-url origin", 0, "https://github.com/contoso/Contoso.git\n");
        host.Runner.On("git", "symbolic-ref --quiet --short HEAD", 0, $"{branch}\n");
        host.Runner.On("git", "status --porcelain", 0, "");
        host.Runner.On("git", "rev-parse --abbrev-ref --symbolic-full-name @{u}", 0, $"origin/{branch}\n");
        host.Runner.On("git", "rev-list --count @{u}..HEAD", 0, "0\n");
        host.Runner.On("gh", "auth status", 0, "github.com\n  ✓ Logged in to github.com account contoso");
        host.Runner.On("gh", "api", 1, "HTTP 404");
    }

    private static string List(params string[] codespaces) => $"[{string.Join(',', codespaces)}]";

    private static string Space(string name, string branch, string state, string lastUsedAt, bool uncommitted = false) =>
        $$"""{"displayName":"Contoso {{branch}}","gitStatus":{"ahead":0,"behind":0,"hasUncommittedChanges":{{(uncommitted ? "true" : "false")}},"hasUnpushedChanges":false,"ref":"{{branch}}"},"lastUsedAt":"{{lastUsedAt}}","name":"{{name}}","state":"{{state}}"}""";

    private static string CreateProject(TestHost host)
    {
        var folder = Directory.CreateDirectory(Path.Combine(host.WorkingDirectory, "Contoso")).FullName;

        File.WriteAllText(Path.Combine(folder, "Contoso.slnx"), "<Solution />");
        File.WriteAllText(Path.Combine(folder, "Contoso.Web.slnf"), "{}");
        LaunchSettings(Path.Combine(folder, "src", "Server", "Contoso.Server.Web"), "http://localhost:5030;http://*:5030");
        LaunchSettings(Path.Combine(folder, "src", "Server", "Contoso.Server.AppHost"), "https://localhost:2030");

        return folder;
    }

    private static void LaunchSettings(string project, string urls)
    {
        Directory.CreateDirectory(Path.Combine(project, "Properties"));
        File.WriteAllText(Path.Combine(project, $"{Path.GetFileName(project)}.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        File.WriteAllText(Path.Combine(project, "Properties", "launchSettings.json"), $$"""{ "profiles": { "http": { "commandName": "Project", "applicationUrl": "{{urls}}" } } }""");
    }
}
