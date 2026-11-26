using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bit.Fabric.Server.Api.Features.DocsMcp;
using Bit.Fabric.Server.Api.Features.Licensing;
using Bit.Fabric.Server.Api.Features.Licensing.Models;
using Bit.Fabric.Server.Api.Features.Projects.Models;
using Bit.Fabric.Shared.Features.Licensing;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Bit.Fabric.Shared.Features.Tenants;
using Microsoft.AspNetCore.Identity;

namespace Bit.Fabric.Tests.Features.Licensing;

/// <summary>
/// Projects' licenses as bit CLI gets them (bitfabric-internal/specs/product.md, A License belongs to a Team): a trial for
/// each new project without an account, and a free license the license page issues to one of the signed-in account's
/// teams, which bit CLI reads once with its one-time code. Each license is checked the way bit CLI checks it, with the
/// public half of the key the server signs with.
/// </summary>
[TestClass, TestCategory("IntegrationTest")]
public class LicenseTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task ATrial_Should_BeIssuedWithoutAnAccount_AndCheckOutWithThePublicKey()
    {
        await using var server = await StartServer();
        await using var client = server.CreateAppClient();
        var project = Guid.NewGuid();
        var commit = NewCommit();

        var trial = await client.GetController<ILicenseController>().Trial(new() { Project = project, Name = "Contoso", Commit = commit }, TestContext.CancellationToken);

        var payload = Verify(server, trial.License);
        Assert.AreEqual(project.ToString("D"), payload.GetProperty("project").GetString());
        Assert.AreEqual("Contoso", payload.GetProperty("name").GetString());
        Assert.AreEqual(commit, payload.GetProperty("commit").GetString());
        Assert.AreEqual("trial", payload.GetProperty("plan").GetString());
        Assert.AreEqual("", payload.GetProperty("holder").GetString());
        Assert.AreEqual(TimeSpan.FromDays(30), payload.GetProperty("expires").GetDateTimeOffset() - payload.GetProperty("issued").GetDateTimeOffset());

        var row = await TrialOf(server, project);
        Assert.AreEqual(commit, row.FirstCommit);
        Assert.IsNull(row.Ci, "Asked outside CI.");
    }

    /// <summary>
    /// The founder's condition on counting CI's trials apart (bitfabric-internal/specs/product.md, Neither ci nor
    /// --project-id changes what a license allows): the field is kept, and the trial is the same trial, as long.
    /// </summary>
    [TestMethod]
    public async Task ATrialAskedFromCi_Should_BeTheSameTrial_OnlyCountedApart()
    {
        await using var server = await StartServer();
        await using var client = server.CreateAppClient();
        var licenses = client.GetController<ILicenseController>();
        var fromCi = Guid.NewGuid();
        var outsideCi = Guid.NewGuid();
        var commit = NewCommit();

        var ciTrial = Verify(server, (await licenses.Trial(new() { Project = fromCi, Name = "Contoso", Commit = commit, Ci = "github-actions" }, TestContext.CancellationToken)).License);
        var trial = Verify(server, (await licenses.Trial(new() { Project = outsideCi, Name = "Contoso", Commit = commit }, TestContext.CancellationToken)).License);

        Assert.AreEqual("trial", ciTrial.GetProperty("plan").GetString());
        Assert.AreEqual(trial.GetProperty("expires").GetDateTimeOffset() - trial.GetProperty("issued").GetDateTimeOffset(),
                        ciTrial.GetProperty("expires").GetDateTimeOffset() - ciTrial.GetProperty("issued").GetDateTimeOffset(), "As long as any trial.");
        Assert.AreEqual("github-actions", (await TrialOf(server, fromCi)).Ci);

        // Asking again, from CI or not, gives the same trial back: no second trial and no longer one.
        var again = await licenses.Trial(new() { Project = fromCi, Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken);
        Assert.AreEqual(ciTrial.GetProperty("expires").GetDateTimeOffset(), Verify(server, again.License).GetProperty("expires").GetDateTimeOffset());
        Assert.AreEqual(commit, Verify(server, again.License).GetProperty("commit").GetString(), "Still signed for the first commit it was first asked with.");

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var status = scope.ServiceProvider.GetRequiredService<IProjectLicenseStatus>();
        Assert.IsTrue(await status.IsLicensed(fromCi, TestContext.CancellationToken), "It allows what any trial allows.");
    }

    /// <summary>
    /// Someone else's id, as <c>bit new --project-id</c> could give it, gets that id's trial back, signed for its owner's
    /// first commit, which bit CLI refuses in any other repository.
    /// </summary>
    [TestMethod]
    public async Task SomeoneElsesId_Should_GetTheirTrialBack_SignedForTheirCommit()
    {
        await using var server = await StartServer();
        await using var client = server.CreateAppClient();
        var licenses = client.GetController<ILicenseController>();
        var project = Guid.NewGuid();
        var ownersCommit = NewCommit();

        await licenses.Trial(new() { Project = project, Name = "Contoso", Commit = ownersCommit }, TestContext.CancellationToken);
        var copy = await licenses.Trial(new() { Project = project, Name = "Copy", Commit = NewCommit(), Ci = "other" }, TestContext.CancellationToken);

        Assert.AreEqual(ownersCommit, Verify(server, copy.License).GetProperty("commit").GetString());
        Assert.IsNull((await TrialOf(server, project)).Ci, "The first request's trial stays as it was.");
    }

    [TestMethod]
    public async Task TheDocsServer_Should_AnswerALicensedProject_AndRefuseAnAddressWithoutOne()
    {
        await using var server = await StartServer();
        await using var client = server.CreateAppClient();
        var project = Guid.NewGuid();
        await client.GetController<ILicenseController>().Trial(new() { Project = project, Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken);

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<IDocsMcpAccess>();

        Assert.IsTrue((await access.Decide(project.ToString(), TestContext.CancellationToken)).IsAllowed, "A project in its trial gets the docs.");
        Assert.IsFalse((await access.Decide(null, TestContext.CancellationToken)).IsAllowed, "An address that names no project is refused.");
        Assert.IsFalse((await access.Decide(Guid.NewGuid().ToString(), TestContext.CancellationToken)).IsAllowed, "A project bit Fabric doesn't know is refused.");
        Assert.IsFalse((await access.Decide("not-an-id", TestContext.CancellationToken)).IsAllowed);
    }

    [TestMethod]
    public async Task Trials_Should_BeLimitedPerAddress()
    {
        await using var server = await StartServer();
        using var api = server.CreateRawHttpClient();

        for (var i = 0; i < 10; i++)
        {
            using var allowed = await api.PostAsJsonAsync("api/v1/License/Trial", new TrialLicenseRequestDto { Project = Guid.NewGuid(), Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var refused = await api.PostAsJsonAsync("api/v1/License/Trial", new TrialLicenseRequestDto { Project = Guid.NewGuid(), Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    [TestMethod]
    public async Task ATrialWithoutAFirstCommit_Should_BeRefused()
    {
        await using var server = await StartServer();
        await using var client = server.CreateAppClient();

        await Assert.ThrowsExactlyAsync<BadRequestException>(() => client.GetController<ILicenseController>().Trial(new() { Project = Guid.NewGuid(), Name = "Contoso", Commit = "main" }, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Issue_Should_NeedASignIn()
    {
        await using var server = await StartServer();
        using var api = server.CreateRawHttpClient();

        using var response = await api.PostAsJsonAsync("api/v1/License/Issue", new IssueLicenseRequestDto { Project = Guid.NewGuid(), Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The license page's path for someone in no team: it is made on the spot, with no elevated session, and the license is
    /// the team's, naming it rather than the person, for the team's project of that id.
    /// </summary>
    [TestMethod]
    public async Task AFreeLicense_Should_BeTheTeams_AndGoToTheWaitingBitOnce()
    {
        await using var server = await StartServer();
        var email = await TestUsers.SignUp(server, TestContext.CancellationToken);
        await using var client = await SignIn(server, email);
        var licenses = client.GetController<ILicenseController>();
        var project = Guid.NewGuid();
        var commit = NewCommit();
        var code = NewCode();

        await licenses.Trial(new() { Project = project, Name = "Contoso", Commit = commit }, TestContext.CancellationToken);
        Assert.IsNull((await licenses.Claim(code, TestContext.CancellationToken)).License, "Nothing is issued with the code yet.");
        Assert.IsEmpty(await licenses.GetTeams(TestContext.CancellationToken), "A new account is in no team.");

        var team = await CreateTeam(client, "Contoso Labs");
        var issued = await licenses.Issue(new() { Project = project, Name = "Contoso", Commit = commit, Code = code, TeamId = team }, TestContext.CancellationToken);

        var payload = Verify(server, issued.License);
        Assert.AreEqual("free", payload.GetProperty("plan").GetString());
        Assert.AreEqual("Contoso Labs", payload.GetProperty("holder").GetString(), "The license names the team.");
        Assert.AreEqual(commit, payload.GetProperty("commit").GetString());
        Assert.AreEqual(TimeSpan.FromDays(365), payload.GetProperty("expires").GetDateTimeOffset() - payload.GetProperty("issued").GetDateTimeOffset());
        Assert.AreEqual("Contoso Labs", issued.Holder);

        // bit CLI reads it anonymously, once.
        await using var cli = server.CreateAppClient();
        Assert.AreEqual(issued.License, (await cli.GetController<ILicenseController>().Claim(code, TestContext.CancellationToken)).License);
        Assert.IsNull((await cli.GetController<ILicenseController>().Claim(code, TestContext.CancellationToken)).License, "The code is spent.");

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var teamsProject = await dbContext.Projects.IgnoreQueryFilters().Include(row => row.Members).SingleAsync(row => row.TenantId == team && row.BitProjectId == project, TestContext.CancellationToken);
        Assert.AreEqual(issued.ProjectId, teamsProject.Id);
        Assert.AreEqual(await UserIdOf(server, email), teamsProject.Members.Single().UserId, "Whoever got it works on the project.");

        var listed = (await licenses.GetLicenses(TestContext.CancellationToken)).Single();
        Assert.AreEqual("Contoso Labs", listed.TeamTitle);
        Assert.AreEqual(project, listed.BitProjectId);
        Assert.AreEqual(LicenseState.InForce, listed.State);
        Assert.AreEqual(issued.License, listed.License);
    }

    [TestMethod]
    public async Task TheTeamPicker_Should_OfferTheTeamsJoined_TheFirstJoinedFirst()
    {
        await using var server = await StartServer();
        await using var client = await SignIn(server, await TestUsers.SignUp(server, TestContext.CancellationToken));
        var licenses = client.GetController<ILicenseController>();

        var first = await CreateTeam(client, "First");
        var second = await CreateTeam(client, "Second");

        Assert.AreSequenceEqual(new[] { first, second }, (await licenses.GetTeams(TestContext.CancellationToken)).Select(team => team.Id).ToArray());
    }

    [TestMethod]
    public async Task ALicense_Should_BeRefused_ForATeamTheAccountIsNotIn()
    {
        await using var server = await StartServer();
        await using var owner = await SignIn(server, await TestUsers.SignUp(server, TestContext.CancellationToken));
        await using var stranger = await SignIn(server, await TestUsers.SignUp(server, TestContext.CancellationToken));
        var ownersTeam = await CreateTeam(owner, "Owners");

        await Assert.ThrowsExactlyAsync<BadRequestException>(() => stranger.GetController<ILicenseController>().Issue(new() { Project = Guid.NewGuid(), Name = "Contoso", Commit = NewCommit(), TeamId = ownersTeam }, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<BadRequestException>(() => stranger.GetController<ILicenseController>().Issue(new() { Project = Guid.NewGuid(), Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AnOldCode_Should_GiveNothing()
    {
        await using var server = await StartServer(configuration => configuration["Licensing:ClaimCodeLifetime"] = "00:00:00");
        await using var client = await SignIn(server, await TestUsers.SignUp(server, TestContext.CancellationToken));
        var licenses = client.GetController<ILicenseController>();
        var code = NewCode();

        await licenses.Issue(new() { Project = Guid.NewGuid(), Name = "Contoso", Commit = NewCommit(), Code = code, TeamId = await CreateTeam(client, "Contoso") }, TestContext.CancellationToken);

        Assert.IsNull((await licenses.Claim(code, TestContext.CancellationToken)).License);
    }

    /// <summary>A fresh repository made from the project's files has a first commit of its own, which the team licenses again.</summary>
    [TestMethod]
    public async Task TheSameTeam_Should_GetALicenseForTheProjectsNewFirstCommit_OnTheSameProject()
    {
        await using var server = await StartServer();
        await using var client = await SignIn(server, await TestUsers.SignUp(server, TestContext.CancellationToken));
        var licenses = client.GetController<ILicenseController>();
        var team = await CreateTeam(client, "Contoso");
        var project = Guid.NewGuid();
        var newCommit = NewCommit();

        var first = await licenses.Issue(new() { Project = project, Name = "Contoso", Commit = NewCommit(), TeamId = team }, TestContext.CancellationToken);
        var again = await licenses.Issue(new() { Project = project, Name = "Contoso", Commit = newCommit, TeamId = team }, TestContext.CancellationToken);

        Assert.AreEqual(newCommit, Verify(server, again.License).GetProperty("commit").GetString());
        Assert.AreEqual(first.ProjectId, again.ProjectId, "One project of the team per id.");

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Projects.IgnoreQueryFilters().SingleAsync(row => row.Id == first.ProjectId, TestContext.CancellationToken);
        Assert.AreEqual(newCommit, row.FirstCommit);
    }

    /// <summary>
    /// A public repository shows its project's id and first commit to anyone, so another team licenses the same id for a
    /// project of its own, and neither sees the other's (bitfabric-internal/specs/monitoring.md, A project belongs to one Team).
    /// </summary>
    [TestMethod]
    public async Task AnotherTeam_Should_GetItsOwnProjectAndLicense_ForTheSameId()
    {
        await using var server = await StartServer();
        await using var owner = await SignIn(server, await TestUsers.SignUp(server, TestContext.CancellationToken));
        await using var someoneElse = await SignIn(server, await TestUsers.SignUp(server, TestContext.CancellationToken));
        var project = Guid.NewGuid();
        var commit = NewCommit();

        var first = await owner.GetController<ILicenseController>().Issue(new() { Project = project, Name = "Contoso", Commit = commit, TeamId = await CreateTeam(owner, "Owners") }, TestContext.CancellationToken);
        var second = await someoneElse.GetController<ILicenseController>().Issue(new() { Project = project, Name = "Contoso", Commit = commit, TeamId = await CreateTeam(someoneElse, "Others") }, TestContext.CancellationToken);

        Assert.AreNotEqual(first.License, second.License);
        Assert.AreNotEqual(first.ProjectId, second.ProjectId);
        Assert.AreEqual(project.ToString("D"), Verify(server, second.License).GetProperty("project").GetString());

        Assert.AreEqual(first.ProjectId, (await owner.GetController<ILicenseController>().GetLicenses(TestContext.CancellationToken)).Single().ProjectId, "Each team sees its own license only.");
        Assert.AreEqual(second.ProjectId, (await someoneElse.GetController<ILicenseController>().GetLicenses(TestContext.CancellationToken)).Single().ProjectId);
    }

    /// <summary>What the docs MCP server asks of a project's id: a trial or any team's license in force, the weeks of grace included.</summary>
    [TestMethod]
    public async Task TheLicenseStatus_Should_FollowTheTrialAndTheLicenses()
    {
        await using var server = await StartServer();
        await using var client = await SignIn(server, await TestUsers.SignUp(server, TestContext.CancellationToken));
        var team = await CreateTeam(client, "Contoso");
        var now = DateTimeOffset.UtcNow;

        var trial = Guid.NewGuid();
        await client.GetController<ILicenseController>().Trial(new() { Project = trial, Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken);

        var licensed = Guid.NewGuid();
        await client.GetController<ILicenseController>().Issue(new() { Project = licensed, Name = "Contoso", Commit = NewCommit(), TeamId = team }, TestContext.CancellationToken);

        var trialEnded = await AddProject(server, team, now.AddDays(-1), licenseEnded: null);
        var inGrace = await AddProject(server, team, now.AddDays(-40), licenseEnded: now.AddDays(-13));
        var pastGrace = await AddProject(server, team, now.AddDays(-40), licenseEnded: now.AddDays(-15));

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var status = scope.ServiceProvider.GetRequiredService<IProjectLicenseStatus>();

        Assert.IsTrue(await status.IsLicensed(trial, TestContext.CancellationToken));
        Assert.IsTrue(await status.IsLicensed(licensed.ToString(), TestContext.CancellationToken));
        Assert.IsTrue(await status.IsLicensed(inGrace, TestContext.CancellationToken));
        Assert.IsFalse(await status.IsLicensed(trialEnded, TestContext.CancellationToken));
        Assert.IsFalse(await status.IsLicensed(pastGrace, TestContext.CancellationToken));
        Assert.IsFalse(await status.IsLicensed(Guid.NewGuid(), TestContext.CancellationToken), "A project bit Fabric doesn't know.");
        Assert.IsFalse(await status.IsLicensed("not-an-id", TestContext.CancellationToken));
    }

    private async Task<AppTestServer> StartServer(Action<Microsoft.Extensions.Configuration.ConfigurationManager>? configure = null)
    {
        var server = new AppTestServer();

        await server.Build(configureTestConfiguration: configure).Start(TestContext.CancellationToken);

        return server;
    }

    /// <summary>An account of its own (See TestUsers.SignUp), signed in: tests share one database and run at the same time.</summary>
    private async Task<AppClient> SignIn(AppTestServer server, string email)
    {
        var client = server.CreateAppClient();
        await client.AuthManager.SignIn(new() { Email = email, Password = TestUsers.Password }, TestContext.CancellationToken);

        return client;
    }

    /// <summary>A team of the signed-in account's, made as the license page makes one: with no elevated session.</summary>
    private async Task<Guid> CreateTeam(AppClient client, string title)
        => (await client.GetController<ITenantController>().Create(new() { Name = $"t{Guid.NewGuid():N}"[..20], Title = title }, TestContext.CancellationToken)).Id;

    private static async Task<Guid> UserIdOf(AppTestServer server, string email)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<UserManager<User>>().FindByEmailAsync(email))!.Id;
    }

    private async Task<ProjectTrial> TrialOf(AppTestServer server, Guid project)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProjectTrials.SingleAsync(row => row.Id == project, TestContext.CancellationToken);
    }

    /// <summary>A codebase whose trial ended at <paramref name="trialEnded"/>, with the team's license that ended at <paramref name="licenseEnded"/>.</summary>
    private async Task<Guid> AddProject(AppTestServer server, Guid team, DateTimeOffset trialEnded, DateTimeOffset? licenseEnded)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bitId = Guid.NewGuid();
        var commit = NewCommit();

        await dbContext.ProjectTrials.AddAsync(new() { Id = bitId, Name = "Contoso", FirstCommit = commit, IssuedOn = trialEnded.AddDays(-30), ExpiresOn = trialEnded, Document = "bfl1.test.test" }, TestContext.CancellationToken);

        if (licenseEnded is { } ended)
        {
            var project = new Project { Id = Guid.NewGuid(), TenantId = team, Name = "Contoso", BitProjectId = bitId, FirstCommit = commit, CreatedOn = ended.AddDays(-365) };
            project.Licenses.Add(new() { Id = Guid.NewGuid(), TenantId = team, Plan = "free", FirstCommit = commit, IssuedOn = ended.AddDays(-365), ExpiresOn = ended, Document = "bfl1.test.test" });
            await dbContext.Projects.AddAsync(project, TestContext.CancellationToken);
        }

        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        return bitId;
    }

    /// <summary>
    /// The payload of <paramref name="license"/> once its signature checks out with the server's public key, the way bit CLI
    /// checks it: ECDSA P-256 with SHA-256 over the ASCII bytes of <c>bfl1.&lt;payload&gt;</c>, in IEEE P1363 form.
    /// </summary>
    private static JsonElement Verify(AppTestServer server, string? license)
    {
        var parts = license!.Split('.');
        Assert.HasCount(3, parts);
        Assert.AreEqual("bfl1", parts[0]);

        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(server.ApiApp.Services.GetRequiredService<LicenseSigner>().PublicKey), out _);

        Assert.IsTrue(key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation), "The signature checks out.");

        using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        Assert.AreEqual(1, payload.RootElement.GetProperty("v").GetInt32());

        return payload.RootElement.Clone();
    }

    private static string NewCommit() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(20));

    private static string NewCode() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(18));
}
