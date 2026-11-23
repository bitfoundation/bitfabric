using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Cli.Infrastructure;
using Bit.Cli.Licensing;
using Bit.Cli.Tests.Infrastructure;

namespace Bit.Cli.Tests;

/// <summary>
/// A project's license, as bit checks it before a command: signed by bit Fabric (here, by <see cref="FakeBitFabric"/>'s
/// key), for the id in the project's bit.json, and naming the first commit of the repository's history, which real git
/// answers for.
/// </summary>
[TestClass]
public class LicenseTests
{
    private static readonly DateTimeOffset issued = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A command that checks the license, then fails on its own, quickly and without starting anything: bit diff from a
    /// version that isn't one. Its exit code tells whether the license let it run.
    /// </summary>
    private static readonly string[] aCommand = ["diff", "not-a-version"];

    private const int CommandRan = CliApp.ExitUsage;

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task AValidLicense_Should_LetTheCommandRun_WithoutAWord()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync();
        project.SaveLicense(project.Sign("free", issued, issued.AddDays(365), holder: "Jane Doe"));

        Assert.AreEqual(CommandRan, await project.Host.RunAsync(aCommand), project.Host.Output);
        Assert.DoesNotContain("license", project.Host.Output);
    }

    [TestMethod]
    public async Task AProjectWithoutALicense_Should_Stop_WithTheLinkThatGetsItOne()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync();

        Assert.AreEqual(CliApp.ExitFailed, await project.Host.RunAsync(aCommand), project.Host.Output);
        StringAssert.Contains(project.Host.Output, $"This project has no license yet. Get one, free: https://bitfabric.co/license?project=Contoso&id={project.Id:D}&commit={project.FirstCommit}&code=");
    }

    [TestMethod]
    public async Task AnEditedLicense_Should_BeTreatedAsNone()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync();
        var license = project.Sign("trial", issued, issued.AddDays(30));
        var parts = license.Split('.');
        // The same license, a year longer.
        var payload = System.Text.Encoding.UTF8.GetString(System.Buffers.Text.Base64Url.DecodeFromChars(parts[1])).Replace("2026-12-01", "2027-12-01", StringComparison.Ordinal);
        project.SaveLicense($"{parts[0]}.{System.Buffers.Text.Base64Url.EncodeToString(System.Text.Encoding.UTF8.GetBytes(payload))}.{parts[2]}");

        Assert.AreEqual(CliApp.ExitFailed, await project.Host.RunAsync(aCommand), project.Host.Output);
        StringAssert.Contains(project.Host.Output, "The license in bit.json doesn't check out: it was changed, or bit Fabric didn't issue it. Get this project a new one, free: https://bitfabric.co/license?");
    }

    [TestMethod]
    public async Task ALicenseSignedWithAnotherKey_Should_BeTreatedAsNone()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync();
        using var someoneElse = new FakeBitFabric();
        project.SaveLicense(someoneElse.Sign(project.Id, project.FirstCommit, "free", issued, issued.AddDays(365)));

        Assert.AreEqual(CliApp.ExitFailed, await project.Host.RunAsync(aCommand), project.Host.Output);
        StringAssert.Contains(project.Host.Output, "doesn't check out");
    }

    [TestMethod]
    public async Task AnotherProjectsLicense_Should_BeTreatedAsNone()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync();
        project.SaveLicense(project.Host.BitFabric.Sign(Guid.NewGuid(), project.FirstCommit, "free", issued, issued.AddDays(365)));

        Assert.AreEqual(CliApp.ExitFailed, await project.Host.RunAsync(aCommand), project.Host.Output);
        StringAssert.Contains(project.Host.Output, "The license in bit.json belongs to another project. Get this project its own, free: https://bitfabric.co/license?");
    }

    /// <summary>bit.json copied into another repository, with the project's id and license: its first commit isn't there.</summary>
    [TestMethod]
    public async Task ACopiedBitJsonInAnotherRepository_Should_BelongToAnotherProject()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var original = await LicensedProject.CreateAsync();
        var license = original.Sign("free", issued, issued.AddDays(365));

        using var copy = await LicensedProject.CreateAsync(id: original.Id);
        copy.Host.Services.License.PublicKeys = [original.Host.BitFabric.PublicKey];
        copy.SaveLicense(license);

        Assert.AreEqual(CliApp.ExitFailed, await copy.Host.RunAsync(aCommand), copy.Host.Output);
        StringAssert.Contains(copy.Host.Output, $"belongs to another project. Get this project its own, free: https://bitfabric.co/license?project=Contoso&id={original.Id:D}&commit={copy.FirstCommit}&code=");
    }

    /// <summary>The original's history fetched into the copy and grafted under its first commit, by a replace ref or the old grafts file.</summary>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task AGraftedHistory_Should_NotPassForTheProjects(bool replaceRef)
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var original = await LicensedProject.CreateAsync();
        var license = original.Sign("free", issued, issued.AddDays(365));

        using var copy = await LicensedProject.CreateAsync(id: original.Id);
        copy.Host.Services.License.PublicKeys = [original.Host.BitFabric.PublicKey];
        copy.SaveLicense(license);
        await copy.GitAsync("fetch", "-q", original.Directory, "main");

        if (replaceRef)
        {
            await copy.GitAsync("replace", "--graft", copy.FirstCommit, original.FirstCommit);
        }
        else
        {
            File.WriteAllText(Path.Combine(copy.Directory, ".git", "info", "grafts"), $"{copy.FirstCommit} {original.FirstCommit}\n");
        }

        Assert.IsTrue((await copy.GitAsync("merge-base", "--is-ancestor", original.FirstCommit, "HEAD")).Succeeded, "git itself takes the graft.");

        Assert.AreEqual(CliApp.ExitFailed, await copy.Host.RunAsync(aCommand), copy.Host.Output);
        StringAssert.Contains(copy.Host.Output, "belongs to another project");
    }

    [TestMethod]
    public async Task AShallowCloneWithoutTheFirstCommit_Should_AskForTheRest_AndGoOn()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var original = await LicensedProject.CreateAsync();
        original.SaveLicense(original.Sign("free", issued, issued.AddDays(365)));
        await original.GitAsync("add", "-A");
        await original.GitAsync("commit", "-q", "-m", "Add the license");

        using var clone = await LicensedProject.CloneAsync(original, depth: 1);
        clone.Host.Services.License.PublicKeys = [original.Host.BitFabric.PublicKey];

        Assert.AreEqual("true", (await clone.GitAsync("rev-parse", "--is-shallow-repository")).Output.Trim());
        Assert.AreEqual(CommandRan, await clone.Host.RunAsync(aCommand), clone.Host.Output);
        StringAssert.Contains(clone.Host.FlatOutput, "This is a shallow clone, without the commit the project's license names. Run git fetch --unshallow to check it.");
    }

    [TestMethod]
    [DataRow(1, CommandRan, null)]
    [DataRow(13, CommandRan, null)]
    [DataRow(20, CommandRan, "The project's trial ends in 11 days, on 1 December 2026. Get its free license: https://bitfabric.co/license?")]
    [DataRow(29, CommandRan, "The project's trial ends in 2 days, on 1 December 2026. Get its free license: https://bitfabric.co/license?")]
    [DataRow(31, CliApp.ExitFailed, "The project's trial has ended. Get its free license: https://bitfabric.co/license?")]
    public async Task ATrial_Should_BeSilentForTwoWeeks_ThenSayWhenItEnds_ThenStop(int day, int exitCode, string? said)
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync(now: issued.AddDays(day - 1).AddHours(1));
        project.SaveLicense(project.Sign("trial", issued, issued.AddDays(30)));

        Assert.AreEqual(exitCode, await project.Host.RunAsync(aCommand), project.Host.Output);

        if (said is null)
        {
            Assert.DoesNotContain("license", project.Host.Output);
        }
        else
        {
            StringAssert.Contains(project.Host.Output, said);
        }
    }

    [TestMethod]
    [DataRow(300, CommandRan, null)]
    [DataRow(340, CommandRan, "The project's license ends in 26 days, on 1 November 2027. Renew it, free: https://bitfabric.co/license?")]
    [DataRow(370, CommandRan, "The project's license ended on 1 November 2027, and bit keeps working until 15 November 2027. Renew it, free: https://bitfabric.co/license?")]
    [DataRow(380, CliApp.ExitFailed, "The project's license ended on 1 November 2027. Renew it, free: https://bitfabric.co/license?")]
    public async Task AFreeLicense_Should_SayWhenItEnds_AndKeepWorkingTwoWeeksAfter(int day, int exitCode, string? said)
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync(now: issued.AddDays(day - 1).AddHours(1));
        project.SaveLicense(project.Sign("free", issued, issued.AddDays(365), holder: "Jane Doe"));

        Assert.AreEqual(exitCode, await project.Host.RunAsync(aCommand), project.Host.Output);

        if (said is null)
        {
            Assert.DoesNotContain("license", project.Host.Output);
        }
        else
        {
            StringAssert.Contains(project.Host.Output, said);
        }
    }

    /// <summary>A clock turned back to before the license ended would keep it going; bit refuses a license from the future.</summary>
    [TestMethod]
    public async Task ALicenseIssuedInTheFuture_Should_BeTreatedAsNone()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync(now: issued.AddDays(-10));
        project.SaveLicense(project.Sign("free", issued, issued.AddDays(365)));

        Assert.AreEqual(CliApp.ExitFailed, await project.Host.RunAsync(aCommand), project.Host.Output);
        StringAssert.Contains(project.Host.Output, "The license in bit.json is dated in the future. Check this computer's date, or get a new license, free: https://bitfabric.co/license?");
    }

    [TestMethod]
    public async Task BitLicense_Should_SaveALicenseThatIsTheProjects()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync();
        var license = project.Sign("free", issued, issued.AddDays(365), holder: "Jane Doe");

        Assert.AreEqual(0, await project.Host.RunAsync("license", license), project.Host.Output);
        Assert.AreEqual(license, project.ReadBitJson()["license"]?.GetValue<string>());
        StringAssert.Contains(project.Host.FlatOutput, "A free license for Jane Doe, until 1 November 2027.");

        Assert.AreEqual(CommandRan, await project.Host.RunAsync(aCommand), project.Host.Output);
    }

    [TestMethod]
    public async Task BitLicense_Should_RefuseOneThatIsntTheProjects_AndLeaveBitJsonAlone()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync();
        var before = File.ReadAllText(project.BitJsonPath);

        Assert.AreEqual(CliApp.ExitFailed, await project.Host.RunAsync("license", project.Host.BitFabric.Sign(Guid.NewGuid(), project.FirstCommit, "free", issued, issued.AddDays(365))), project.Host.Output);
        StringAssert.Contains(project.Host.Output, "belongs to another project");
        Assert.AreEqual(before, File.ReadAllText(project.BitJsonPath));
    }

    [TestMethod]
    public async Task BitLicenseAlone_Should_ShowTheLicenseAndTheLink()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync();
        project.SaveLicense(project.Sign("trial", issued, issued.AddDays(30)));

        Assert.AreEqual(0, await project.Host.RunAsync("license"), project.Host.Output);
        StringAssert.Contains(project.Host.FlatOutput, "A trial, until 1 December 2026.");
        StringAssert.Contains(project.Host.Output, $"https://bitfabric.co/license?project=Contoso&id={project.Id:D}&commit={project.FirstCommit}&code=");
    }

    /// <summary>At a terminal, bit waits for the license the link's page issues, saves it, and the command goes on.</summary>
    [TestMethod]
    public async Task AWaitingBit_Should_SaveTheLicenseIssuedForItsCode_AndGoOn()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync(prompter: new ScriptedPrompter());
        var license = project.Sign("free", issued, issued.AddDays(365));
        project.Host.BitFabric.Online = true;
        project.Host.BitFabric.NextClaim = license;

        Assert.AreEqual(CommandRan, await project.Host.RunAsync(aCommand), project.Host.Output);
        Assert.AreEqual(license, project.ReadBitJson()["license"]?.GetValue<string>());
        StringAssert.Contains(project.Host.Output, "Got the project's license and saved it in bit.json.");

        var code = System.Text.RegularExpressions.Regex.Match(project.Host.Output, "&code=([A-Za-z0-9_-]+)").Groups[1].Value;
        Assert.AreEqual($"claim {code}", project.Host.BitFabric.Requests.Single(), "It asks with the code of the link it printed.");
    }

    [TestMethod]
    public async Task InCI_Bit_Should_OnlyPrintTheLink()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync(prompter: new ScriptedPrompter(), variables: new() { ["GITHUB_ACTIONS"] = "true" });
        project.Host.BitFabric.Online = true;
        project.Host.BitFabric.NextClaim = project.Sign("free", issued, issued.AddDays(365));

        Assert.AreEqual(CliApp.ExitFailed, await project.Host.RunAsync(aCommand), project.Host.Output);
        StringAssert.Contains(project.Host.Output, "Get one, free: https://bitfabric.co/license?");
        Assert.IsEmpty(project.Host.BitFabric.Requests);
        Assert.IsNull(project.ReadBitJson()["license"]);
    }

    [TestMethod]
    [DataRow("telemetry")]
    [DataRow("--version")]
    [DataRow("diff", "--help")]
    [DataRow("license", "--help")]
    public async Task Commands_Should_NeverCheck_WhenTheyWorkOnNoProject(params string[] command)
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync();

        Assert.AreEqual(0, await project.Host.RunAsync(command), project.Host.Output);
        Assert.DoesNotContain("Get one, free", project.Host.Output);
    }

    [TestMethod]
    public async Task AFolderWithoutBitJson_Should_HaveNothingToCheck()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync();
        File.Delete(project.BitJsonPath);

        await project.Host.RunAsync(aCommand);

        Assert.DoesNotContain("license", project.Host.Output);
    }

    /// <summary>Deleting projectId from bit.json doesn't turn the check off.</summary>
    [TestMethod]
    public async Task AProjectOnTheFirstLicensedTemplateWithoutAnId_Should_Stop_UntilBitLicenseGivesItOne()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync();
        const string bitJson = """{ "template": "bit-bp", "version": "11.0.0-pre-24", "name": "Contoso" }""";
        File.WriteAllText(project.BitJsonPath, bitJson);

        Assert.AreEqual(CliApp.ExitFailed, await project.Host.RunAsync(aCommand), project.Host.Output);
        StringAssert.Contains(project.Host.FlatOutput, "This project has no id in bit.json, so it has no license yet. Run bit license to give it its id and the link that gets it a license, free.");
        Assert.AreEqual(bitJson, File.ReadAllText(project.BitJsonPath), "Only bit license writes the id.");

        await project.Host.RunAsync("license");

        var id = Guid.Parse(project.ReadBitJson()["projectId"]!.GetValue<string>());
        StringAssert.Contains(project.Host.Output, $"Get one, free: https://bitfabric.co/license?project=Contoso&id={id:D}&commit=");
    }

    /// <summary>A project made before licenses never had an id.</summary>
    [TestMethod]
    [DataRow("11.0.0-pre-22")]
    [DataRow("10.4.0")]
    public async Task AProjectOnAnOlderTemplate_Should_HaveNothingToCheck(string version)
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync();
        File.WriteAllText(project.BitJsonPath, $$"""{ "template": "bit-bp", "version": "{{version}}", "name": "Contoso" }""");

        Assert.AreEqual(CommandRan, await project.Host.RunAsync(aCommand), project.Host.Output);
        Assert.DoesNotContain("license", project.Host.Output);
    }

    /// <summary>bit Boilerplate's own source, which CI runs bit setup on: the template's bit.json, without an id.</summary>
    [TestMethod]
    public async Task TheTemplatesOwnFolder_Should_HaveNothingToCheck()
    {
        if (await GitIdentity.GitAvailableAsync() is false)
            return;

        using var project = await LicensedProject.CreateAsync();
        File.WriteAllText(project.BitJsonPath, """{ "template": "bit-bp", "version": "11.0.0-pre-24", "name": "Boilerplate" }""");
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(project.Directory, ".template.config")).FullName, "template.json"), "{}");

        Assert.AreEqual(CommandRan, await project.Host.RunAsync(aCommand), project.Host.Output);
        Assert.DoesNotContain("license", project.Host.Output);
    }

    [TestMethod]
    public void ARelease_Should_AcceptBitFabricsProductionKeyAlone()
    {
#if DEBUG
        CollectionAssert.AreEqual(new[] { LicenseKeys.Production, LicenseKeys.Test }, LicenseKeys.Accepted.ToArray());
#else
        CollectionAssert.AreEqual(new[] { LicenseKeys.Production }, LicenseKeys.Accepted.ToArray());
#endif
    }

    [TestMethod]
    public async Task BitNew_Should_NameTheProject_AskForItsTrial_AndPutItsIdOnTheDocsMcpAddress()
    {
        using var host = new TestHost(HostOs.Linux);
        host.BitFabric.Online = true;
        host.Services.License.PublicKeys = [host.BitFabric.PublicKey];
        const string firstCommit = "0123456789abcdef0123456789abcdef01234567";
        string? committedBitJson = null;

        host.Runner.On("dotnet", "new bit-bp", spec =>
        {
            var arguments = spec.Arguments.ToList();
            var directory = Directory.CreateDirectory(arguments[arguments.IndexOf("--output") + 1]).FullName;

            NewWorkflowTests.WriteProject(directory, "Contoso");
            File.WriteAllText(Path.Combine(directory, "bit.json"), """
                {
                    "template": "bit-bp",
                    "version": "11.0.0",
                    "name": "Contoso",
                    "projectId": "11111111-1111-1111-1111-111111111111",
                    "options": {
                        "database": "Sqlite"
                    }
                }
                """);
            File.WriteAllText(Path.Combine(directory, ".mcp.json"), """{ "mcpServers": { "bitplatform": { "type": "http", "url": "https://api.bitfabric.co/docs-mcp?v=11.0.0" }, "dev-mcp-test": { "type": "http", "url": "https://test.example.com/dev-mcp" } } }""");
            File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(directory, ".codex")).FullName, "config.toml"), "[mcp_servers.bitplatform]\nurl = \"https://api.bitfabric.co/docs-mcp?v=11.0.0&project=old\"\n");

            return new ProcessResult { ExitCode = 0 };
        });
        host.Runner.Executables["git"] = "git";
        host.Runner.On("git", "rev-parse", 128, "fatal: not a git repository");
        host.Runner.On("git", "rev-list --max-parents=0 HEAD", 0, firstCommit);
        host.Runner.On("git", "commit -q -m Create", spec =>
        {
            committedBitJson = File.ReadAllText(Path.Combine(spec.WorkingDirectory!, "bit.json"));
            return new ProcessResult { ExitCode = 0 };
        });

        Assert.AreEqual(0, await host.RunAsync("new", "Contoso", "--template-package", NewWorkflowTests.FakeTemplatePackage(host), "--yes", "--no-setup", "--no-trust"), host.Output);

        var directory = Path.Combine(host.WorkingDirectory, "Contoso");
        var bitJson = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "bit.json")))!.AsObject();
        var id = Guid.Parse(bitJson["projectId"]!.GetValue<string>());

        StringAssert.Contains(committedBitJson, $"\"projectId\": \"{id:D}\"", "The first commit has the project's id.");
        Assert.AreNotEqual(Guid.Parse("11111111-1111-1111-1111-111111111111"), id, "Each project gets an id of its own, whatever the template's bit.json says.");
        Assert.AreEqual("11.0.0", bitJson["version"]!.GetValue<string>(), "The rest of bit.json is as it was.");

        using var trial = JsonDocument.Parse(host.BitFabric.Requests.Single());
        Assert.AreEqual(id.ToString("D"), trial.RootElement.GetProperty("project").GetString());
        Assert.AreEqual("Contoso", trial.RootElement.GetProperty("name").GetString());
        Assert.AreEqual(firstCommit, trial.RootElement.GetProperty("commit").GetString());

        var license = ProjectLicense.Verify(bitJson["license"]!.GetValue<string>(), [host.BitFabric.PublicKey]);
        Assert.IsNotNull(license);
        Assert.AreEqual(id, license.Project);
        Assert.AreEqual("trial", license.Plan);
        Assert.IsTrue(host.Runner.Calls.Any(call => call.FileName is "git" && string.Join(' ', call.Arguments).StartsWith("commit -q -m Add the project's trial license", StringComparison.Ordinal)), "The trial is committed.");
        StringAssert.Contains(host.FlatOutput, "Got a trial license");

        StringAssert.Contains(File.ReadAllText(Path.Combine(directory, ".mcp.json")), $"https://api.bitfabric.co/docs-mcp?v=11.0.0&project={id:D}\"");
        StringAssert.Contains(File.ReadAllText(Path.Combine(directory, ".mcp.json")), "https://test.example.com/dev-mcp\"", "Only the docs MCP server's address names the project.");
        StringAssert.Contains(File.ReadAllText(Path.Combine(directory, ".codex", "config.toml")), $"https://api.bitfabric.co/docs-mcp?v=11.0.0&project={id:D}\"");
    }

    [TestMethod]
    public async Task BitNewOffline_Should_FinishWithOneLine_AndLeaveTheProjectUnlicensed()
    {
        using var host = new TestHost(HostOs.Linux);
        host.Runner.On("dotnet", "new bit-bp", spec =>
        {
            var arguments = spec.Arguments.ToList();
            var directory = Directory.CreateDirectory(arguments[arguments.IndexOf("--output") + 1]).FullName;

            NewWorkflowTests.WriteProject(directory, "Contoso");
            File.WriteAllText(Path.Combine(directory, "bit.json"), """{ "name": "Contoso" }""");

            return new ProcessResult { ExitCode = 0 };
        });
        host.Runner.Executables["git"] = "git";
        host.Runner.On("git", "rev-parse", 128, "fatal: not a git repository");
        host.Runner.On("git", "rev-list --max-parents=0 HEAD", 0, "0123456789abcdef0123456789abcdef01234567");

        Assert.AreEqual(0, await host.RunAsync("new", "Contoso", "--template-package", NewWorkflowTests.FakeTemplatePackage(host), "--yes", "--no-setup", "--no-trust"), host.Output);

        var bitJson = JsonNode.Parse(File.ReadAllText(Path.Combine(host.WorkingDirectory, "Contoso", "bit.json")))!.AsObject();
        Assert.IsTrue(Guid.TryParse(bitJson["projectId"]?.GetValue<string>(), out _));
        Assert.IsNull(bitJson["license"]);
        StringAssert.Contains(host.FlatOutput, "No trial license yet bit Fabric didn't answer; run bit license later");
    }

    [TestMethod]
    [DataRow("https://api.bitfabric.co/docs-mcp", "https://api.bitfabric.co/docs-mcp?project={0}")]
    [DataRow("https://api.bitfabric.co/docs-mcp?v=11.0.0", "https://api.bitfabric.co/docs-mcp?v=11.0.0&project={0}")]
    [DataRow("https://api.bitfabric.co/docs-mcp?project=x&v=11.0.0", "https://api.bitfabric.co/docs-mcp?v=11.0.0&project={0}")]
    [DataRow("https://localhost:5031/docs-mcp/?v=1", "https://localhost:5031/docs-mcp/?v=1&project={0}")]
    public void TheDocsMcpAddress_Should_NameTheProjectOnce(string address, string expected)
    {
        var id = Guid.NewGuid();

        Assert.AreEqual(string.Format(System.Globalization.CultureInfo.InvariantCulture, expected, id.ToString("D")), NewProjectIdentity.WithProject(address, id));
    }

    /// <summary>A git repository with a first commit, a bit.json with the project's id, and bit checking its license.</summary>
    private sealed class LicensedProject : IDisposable
    {
        private readonly GitIdentity identity;

        private readonly ProcessRunner runner;

        private LicensedProject(TestHost host, GitIdentity identity)
        {
            Host = host;
            this.identity = identity;
            runner = new ProcessRunner(host.Environment, CliLog.None);
        }

        public TestHost Host { get; }

        public string Directory => Host.WorkingDirectory;

        public string BitJsonPath => Path.Combine(Directory, "bit.json");

        public Guid Id { get; private set; }

        public string FirstCommit { get; private set; } = "";

        public static async Task<LicensedProject> CreateAsync(Guid? id = null, DateTimeOffset? now = null, IPrompter? prompter = null, Dictionary<string, string>? variables = null)
        {
            var project = Start(now, prompter, variables);

            await project.GitAsync("init", "-q", "-b", "main");
            File.WriteAllText(project.BitJsonPath, $$"""
                {
                    "template": "bit-bp",
                    "name": "Contoso",
                    "projectId": "{{id ?? Guid.NewGuid():D}}"
                }
                """);
            // Two projects made the same second from the same files would share their first commit.
            File.WriteAllText(Path.Combine(project.Directory, "README.md"), $"Contoso {Guid.NewGuid():N}");
            await project.GitAsync("add", "-A");
            await project.GitAsync("commit", "-q", "-m", "Create Contoso with bit new");
            await project.ReadAsync();

            return project;
        }

        public static async Task<LicensedProject> CloneAsync(LicensedProject original, int depth)
        {
            var project = Start(null, null, null);

            System.IO.Directory.Delete(project.Directory);
            await project.GitAsync("clone", "-q", "--depth", $"{depth}", new Uri(original.Directory).AbsoluteUri, project.Directory);
            await project.ReadAsync();

            return project;
        }

        private static LicensedProject Start(DateTimeOffset? now, IPrompter? prompter, Dictionary<string, string>? variables)
        {
            var allVariables = GitIdentity.RealPath();

            foreach (var (key, value) in variables ?? [])
            {
                allVariables[key] = value;
            }

            var host = new TestHost(variables: allVariables, prompter: prompter, realProcesses: true, checksLicenses: true);
            host.Services.License.PublicKeys = [host.BitFabric.PublicKey];
            host.Services.License.Clock = new Clock(now ?? issued.AddDays(1));
            host.Services.License.PollInterval = TimeSpan.FromMilliseconds(10);
            host.Services.License.WaitLimit = TimeSpan.FromSeconds(30);

            return new LicensedProject(host, new GitIdentity(host.Root));
        }

        private async Task ReadAsync()
        {
            Id = Guid.Parse(ReadBitJson()["projectId"]!.GetValue<string>());
            FirstCommit = (await GitAsync("rev-list", "--max-parents=0", "HEAD")).OutputLines.Last().Trim();
        }

        public string Sign(string plan, DateTimeOffset from, DateTimeOffset until, string holder = "")
            => Host.BitFabric.Sign(Id, FirstCommit, plan, from, until, holder: holder);

        public void SaveLicense(string license)
        {
            var bitJson = ReadBitJson();
            bitJson["license"] = license;
            File.WriteAllText(BitJsonPath, bitJson.ToJsonString());
        }

        public JsonObject ReadBitJson() => JsonNode.Parse(File.ReadAllText(BitJsonPath))!.AsObject();

        public Task<ProcessResult> GitAsync(params string[] arguments)
            => runner.RunAsync(new ProcessSpec { FileName = "git", Arguments = arguments, WorkingDirectory = System.IO.Directory.Exists(Directory) ? Directory : Host.Root });

        public void Dispose()
        {
            identity.Dispose();
            Host.Dispose();
        }
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
