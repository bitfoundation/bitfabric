using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bit.Fabric.Server.Api.Features.Licensing;
using Bit.Fabric.Server.Api.Features.Licensing.Models;
using Bit.Fabric.Shared.Features.Licensing;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Microsoft.AspNetCore.Identity;

namespace Bit.Fabric.Tests.Features.Licensing;

/// <summary>
/// Projects' licenses as bit CLI gets them: a trial for each new project without an account, and a free license the
/// license page issues to a signed-in account, which bit CLI reads once with its one-time code. Each license is checked the
/// way bit CLI checks it, with the public half of the key the server signs with.
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

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().LicensedProjects.SingleAsync(row => row.Id == project, TestContext.CancellationToken);
        Assert.IsNull(row.UserId, "Nobody signs in for a trial.");
        Assert.AreEqual(commit, row.FirstCommit);
    }

    [TestMethod]
    public async Task TheSameProject_Should_GetTheTrialItAlreadyHas()
    {
        await using var server = await StartServer();
        await using var client = server.CreateAppClient();
        var licenses = client.GetController<ILicenseController>();
        var project = Guid.NewGuid();

        var first = await licenses.Trial(new() { Project = project, Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken);
        var again = await licenses.Trial(new() { Project = project, Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken);

        Assert.AreEqual(first.License, again.License);
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

    [TestMethod]
    public async Task AFreeLicense_Should_GoToTheWaitingBitOnce()
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

        var issued = await licenses.Issue(new() { Project = project, Name = "Contoso", Commit = commit, Code = code }, TestContext.CancellationToken);

        var payload = Verify(server, issued.License);
        Assert.AreEqual("free", payload.GetProperty("plan").GetString());
        Assert.AreEqual("", payload.GetProperty("holder").GetString(), "A new account has no name, and its email is never written into a license.");
        Assert.AreEqual(commit, payload.GetProperty("commit").GetString());
        Assert.AreEqual(TimeSpan.FromDays(365), payload.GetProperty("expires").GetDateTimeOffset() - payload.GetProperty("issued").GetDateTimeOffset());
        Assert.AreEqual("", issued.Holder);

        // bit CLI reads it anonymously, once.
        await using var cli = server.CreateAppClient();
        Assert.AreEqual(issued.License, (await cli.GetController<ILicenseController>().Claim(code, TestContext.CancellationToken)).License);
        Assert.IsNull((await cli.GetController<ILicenseController>().Claim(code, TestContext.CancellationToken)).License, "The code is spent.");

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().LicensedProjects.SingleAsync(row => row.Id == project, TestContext.CancellationToken);
        Assert.AreEqual(await UserIdOf(server, email), row.UserId, "The project is the account's now.");
    }

    [TestMethod]
    public async Task AnOldCode_Should_GiveNothing()
    {
        await using var server = await StartServer(configuration => configuration["Licensing:ClaimCodeLifetime"] = "00:00:00");
        await using var client = await SignIn(server, await TestUsers.SignUp(server, TestContext.CancellationToken));
        var licenses = client.GetController<ILicenseController>();
        var code = NewCode();

        await licenses.Issue(new() { Project = Guid.NewGuid(), Name = "Contoso", Commit = NewCommit(), Code = code }, TestContext.CancellationToken);

        Assert.IsNull((await licenses.Claim(code, TestContext.CancellationToken)).License);
    }

    /// <summary>A fresh repository made from the project's files has a first commit of its own, which the account licenses again.</summary>
    [TestMethod]
    public async Task TheSameAccount_Should_GetALicenseForTheProjectsNewFirstCommit()
    {
        await using var server = await StartServer();
        await using var client = await SignIn(server, await TestUsers.SignUp(server, TestContext.CancellationToken));
        var licenses = client.GetController<ILicenseController>();
        var project = Guid.NewGuid();
        var newCommit = NewCommit();

        await licenses.Issue(new() { Project = project, Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken);
        var again = await licenses.Issue(new() { Project = project, Name = "Contoso", Commit = newCommit }, TestContext.CancellationToken);

        Assert.AreEqual(newCommit, Verify(server, again.License).GetProperty("commit").GetString());

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().LicensedProjects.SingleAsync(row => row.Id == project, TestContext.CancellationToken);
        Assert.AreEqual(newCommit, row.FirstCommit);
    }

    /// <summary>
    /// A public repository shows its project's id and first commit to anyone, so the account that asked first can't keep
    /// the others from a license while every license is free.
    /// </summary>
    [TestMethod]
    public async Task AnotherAccount_Should_GetItsOwnLicense_ForAProjectAnotherHas()
    {
        await using var server = await StartServer();
        var ownerEmail = await TestUsers.SignUp(server, TestContext.CancellationToken);
        await using var owner = await SignIn(server, ownerEmail);
        await using var someoneElse = await SignIn(server, await TestUsers.SignUp(server, TestContext.CancellationToken));
        var project = Guid.NewGuid();
        var commit = NewCommit();

        var first = await owner.GetController<ILicenseController>().Issue(new() { Project = project, Name = "Contoso", Commit = commit }, TestContext.CancellationToken);
        var second = await someoneElse.GetController<ILicenseController>().Issue(new() { Project = project, Name = "Contoso", Commit = commit }, TestContext.CancellationToken);

        Assert.AreNotEqual(first.License, second.License);
        Assert.AreEqual(project.ToString("D"), Verify(server, second.License).GetProperty("project").GetString());
        Assert.AreEqual(commit, Verify(server, second.License).GetProperty("commit").GetString());

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().LicensedProjects.SingleAsync(row => row.Id == project, TestContext.CancellationToken);
        Assert.AreEqual(await UserIdOf(server, ownerEmail), row.UserId, "The first account stays recorded with the project.");
    }

    /// <summary>The license goes into the project's bit.json and its git history, often public: a full name or no one.</summary>
    [TestMethod]
    public async Task TheLicense_Should_NameTheAccountByItsFullName_AndNeverByItsEmail()
    {
        await using var server = await StartServer();
        var email = await TestUsers.SignUp(server, TestContext.CancellationToken);
        await using var client = await SignIn(server, email);
        var licenses = client.GetController<ILicenseController>();

        async Task<string?> HolderAsync() => Verify(server, (await licenses.Issue(new() { Project = Guid.NewGuid(), Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken)).License).GetProperty("holder").GetString();

        Assert.AreEqual("", await HolderAsync(), "A new account has an email and no name.");

        await TestUsers.SetFullName(server, email, "Jane Doe");
        Assert.AreEqual("Jane Doe", await HolderAsync());

        await TestUsers.SetFullName(server, email, email);
        Assert.AreEqual("", await HolderAsync(), "A full name that is an email address is one too.");
    }

    /// <summary>What the docs MCP server asks of a project's id: a trial or a license in force, the weeks of grace included.</summary>
    [TestMethod]
    public async Task TheLicenseStatus_Should_FollowTheTrialAndTheLicenses()
    {
        await using var server = await StartServer();
        var email = await TestUsers.SignUp(server, TestContext.CancellationToken);
        await using var client = await SignIn(server, email);
        var userId = await UserIdOf(server, email);
        var now = DateTimeOffset.UtcNow;

        var trial = Guid.NewGuid();
        await client.GetController<ILicenseController>().Trial(new() { Project = trial, Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken);

        var licensed = Guid.NewGuid();
        await client.GetController<ILicenseController>().Issue(new() { Project = licensed, Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken);

        var trialEnded = await AddProject(server, now.AddDays(-1), userId, licenseEnded: null);
        var inGrace = await AddProject(server, now.AddDays(-40), userId, licenseEnded: now.AddDays(-13));
        var pastGrace = await AddProject(server, now.AddDays(-40), userId, licenseEnded: now.AddDays(-15));

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

    private static async Task<Guid> UserIdOf(AppTestServer server, string email)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<UserManager<User>>().FindByEmailAsync(email))!.Id;
    }

    /// <summary>A project whose trial ended at <paramref name="trialEnded"/>, with a license that ended at <paramref name="licenseEnded"/>.</summary>
    private async Task<Guid> AddProject(AppTestServer server, DateTimeOffset trialEnded, Guid userId, DateTimeOffset? licenseEnded)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var project = new LicensedProject { Id = Guid.NewGuid(), Name = "Contoso", FirstCommit = NewCommit(), CreatedOn = trialEnded.AddDays(-30), TrialIssuedOn = trialEnded.AddDays(-30), TrialExpiresOn = trialEnded, UserId = userId };

        if (licenseEnded is { } ended)
        {
            project.Licenses.Add(new() { UserId = userId, Plan = "free", FirstCommit = project.FirstCommit, IssuedOn = ended.AddDays(-365), ExpiresOn = ended, Document = "bfl1.test.test" });
        }

        await dbContext.LicensedProjects.AddAsync(project, TestContext.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        return project.Id;
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
