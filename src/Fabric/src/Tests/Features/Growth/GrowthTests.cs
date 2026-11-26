using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Growth;
using Bit.Fabric.Shared.Features.Growth.Dtos;
using Bit.Fabric.Shared.Features.Licensing;
using Bit.Fabric.Shared.Features.Tenants;
using Bit.Fabric.Tests.Features.Identity;

namespace Bit.Fabric.Tests.Features.Growth;

/// <summary>
/// The growth page's counts (bitfabric-internal/specs/growth.md) as the api gives them to a global admin: projects made
/// by their Trial or a License without one, first Licenses, teams, Trials ending and converting, the days to a License,
/// licensed projects in production, a Trial asked for from CI left out, and the days of the viewer's time zone. The
/// tests share one database, so the rows each test makes are moved to days of a year long past, picked at random, and
/// only those days are read.
/// </summary>
[TestClass, TestCategory("IntegrationTest")]
public class GrowthTests
{
    public TestContext TestContext { get; set; } = default!;

    /// <summary>
    /// Takes away what a run of these tests cut short left on days long past, before any of them starts: rows dated before
    /// 2001 are only ever these tests', and the page's running totals start at the first project.
    /// </summary>
    [ClassInitialize]
    public static async Task RemoveLeftovers(TestContext context)
    {
        await using var server = new AppTestServer();
        await server.Build().Start(context.CancellationToken);

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var longAgo = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await dbContext.Monitorings.IgnoreQueryFilters().Where(row => row.CreatedOn < longAgo).ExecuteDeleteAsync(context.CancellationToken);
        await dbContext.IssuedLicenses.IgnoreQueryFilters().Where(row => row.IssuedOn < longAgo).ExecuteDeleteAsync(context.CancellationToken);
        await dbContext.Projects.IgnoreQueryFilters().Where(row => row.CreatedOn < longAgo).ExecuteDeleteAsync(context.CancellationToken);
        await dbContext.ProjectTrials.Where(row => row.IssuedOn < longAgo).ExecuteDeleteAsync(context.CancellationToken);
    }

    [TestMethod]
    public async Task TheGrowthCounts_Should_CountProjectsTrialsAndLicensesByDay_AndLeaveCiOut()
    {
        await using var server = await StartServer();
        await using var admin = await SignInAsGlobalAdmin(server);
        await using var cli = server.CreateAppClient();

        var day = RandomPastDay();
        var (licensed, running, fromCi, withoutTrial) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        try
        {
            // bit new's Trials: two outside CI, one from CI.
            var trials = cli.GetController<ILicenseController>();
            await trials.Trial(new() { Project = licensed, Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken);
            await trials.Trial(new() { Project = running, Name = "Fabrikam", Commit = NewCommit() }, TestContext.CancellationToken);
            await trials.Trial(new() { Project = fromCi, Name = "Ci", Commit = NewCommit(), Ci = "github-actions" }, TestContext.CancellationToken);

            // A team licenses one of them during its Trial, and a project that never had one.
            var team = await CreateTeam(admin, "Growth Labs");
            var licenses = admin.GetController<ILicenseController>();
            await licenses.Issue(new() { Project = licensed, Name = "Contoso", Commit = NewCommit(), TeamId = team }, TestContext.CancellationToken);
            await licenses.Issue(new() { Project = withoutTrial, Name = "Northwind", Commit = NewCommit(), TeamId = team }, TestContext.CancellationToken);

            await MoveTrial(server, licensed, At(day, 10));
            await MoveTrial(server, running, At(day, 10));
            await MoveTrial(server, fromCi, At(day, 10));
            await MoveLicense(server, licensed, At(day.AddDays(3), 10));
            await MoveLicense(server, withoutTrial, At(day.AddDays(5), 10));
            await AddProduction(server, team, licensed, At(day.AddDays(10), 10));

            var data = await admin.GetController<IGrowthController>().GetData("UTC", TestContext.CancellationToken);
            var days = data.Days.Where(count => count.Day >= day && count.Day <= day.AddDays(40)).ToDictionary(count => count.Day);

            Assert.AreEqual(2, days[day].ProjectsMade, "The two Trials outside CI; the one from CI holds no License, so it isn't a project here.");
            Assert.AreEqual(1, days[day.AddDays(5)].ProjectsMade, "A project licensed with no Trial is made by its License.");
            Assert.AreEqual(1, days[day.AddDays(3)].ProjectsLicensed);
            Assert.AreEqual(1, days[day.AddDays(5)].ProjectsLicensed);
            Assert.AreEqual(1, days[day.AddDays(3)].TeamsLicensed, "The team's first License.");
            Assert.AreEqual(0, days[day.AddDays(5)].TeamsLicensed, "Its second License isn't another team.");
            Assert.AreEqual(1, days[day.AddDays(10)].InProduction);
            Assert.AreEqual(2, days[day.AddDays(30)].TrialsEnded, "The Trial from CI ends too, uncounted.");
            Assert.AreEqual(1, days[day.AddDays(30)].TrialsEndedLicensed);
            Assert.AreEqual(1, days[day.AddDays(30)].TrialsConverted, "Counted as converted when its Trial ended, the later of the two.");

            var delay = data.LicenseDelays.Single(delay => delay.Day == day.AddDays(3));
            Assert.AreEqual(3, delay.Days);
            Assert.AreEqual(1, delay.Count);

            Assert.IsGreaterThanOrEqualTo(1, data.PeopleInLicensedTeams);
            Assert.IsGreaterThanOrEqualTo(1, data.TeamsByProjects[1], "A team with two licensed projects.");
        }
        finally
        {
            await Remove(server, licensed, running, fromCi, withoutTrial);
        }
    }

    /// <summary>A day is the viewer's: a Trial at 22:30 UTC is the next day in Tehran.</summary>
    [TestMethod]
    public async Task TheGrowthCounts_Should_FollowTheViewersTimeZone()
    {
        await using var server = await StartServer();
        await using var admin = await SignInAsGlobalAdmin(server);
        await using var cli = server.CreateAppClient();

        var day = RandomPastDay();
        var project = Guid.NewGuid();

        await cli.GetController<ILicenseController>().Trial(new() { Project = project, Name = "Contoso", Commit = NewCommit() }, TestContext.CancellationToken);
        await MoveTrial(server, project, new DateTimeOffset(day.ToDateTime(new TimeOnly(22, 30)), TimeSpan.Zero));

        var growth = admin.GetController<IGrowthController>();
        var utc = await growth.GetData("UTC", TestContext.CancellationToken);
        var tehran = await growth.GetData("Asia/Tehran", TestContext.CancellationToken);
        await Remove(server, project);

        Assert.AreEqual(1, utc.Days.Single(count => count.Day == day).ProjectsMade);
        Assert.AreEqual(1, tehran.Days.Single(count => count.Day == day.AddDays(1)).ProjectsMade);
        Assert.DoesNotContain(count => count.Day == day && count.ProjectsMade > 0, tehran.Days);
    }

    /// <summary>Only the global admin has the growth page's feature: a team's own admin doesn't.</summary>
    [TestMethod]
    public async Task TheGrowthCounts_Should_BeTheGlobalAdminsOnly()
    {
        await using var server = await StartServer();

        await using var anonymous = server.CreateAppClient();
        await Assert.ThrowsExactlyAsync<UnauthorizedException>(() => anonymous.GetController<IGrowthController>().GetData(null, TestContext.CancellationToken));

        var email = await TestUsers.SignUp(server, TestContext.CancellationToken);
        await using var member = server.CreateAppClient();
        await member.AuthManager.SignIn(new() { Email = email, Password = TestUsers.Password }, TestContext.CancellationToken);
        await CreateTeam(member, "Someone's team");

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => member.GetController<IGrowthController>().GetData(null, TestContext.CancellationToken));

        Assert.Contains(AppFeatures.System.Growth_View, AppFeatures.GetGlobalAdminFeatures().Select(feature => feature.Value).ToArray());
        Assert.DoesNotContain(AppFeatures.System.Growth_View, AppFeatures.GetTenantAdminFeatures().Select(feature => feature.Value).ToArray());
        Assert.DoesNotContain(AppFeatures.System.Growth_View, AppFeatures.GetDemoFeatures().Select(feature => feature.Value).ToArray());
    }

    private async Task<AppTestServer> StartServer()
    {
        var server = new AppTestServer();

        await server.Build().Start(TestContext.CancellationToken);

        return server;
    }

    /// <summary>An account of its own in the seeded g-admin role, signed in after it joined, so its token carries the role.</summary>
    private async Task<AppClient> SignInAsGlobalAdmin(AppTestServer server)
    {
        var email = await TestUsers.SignUp(server, TestContext.CancellationToken);

        await using (var scope = server.ApiApp.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userId = await dbContext.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync(TestContext.CancellationToken);
            var roleId = await dbContext.Roles.Where(role => role.Name == AppRoles.GlobalAdmin).Select(role => role.Id).SingleAsync(TestContext.CancellationToken);

            await dbContext.UserRoles.AddAsync(new UserRole { UserId = userId, RoleId = roleId }, TestContext.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.CancellationToken);
        }

        var client = server.CreateAppClient();
        await client.AuthManager.SignIn(new() { Email = email, Password = TestUsers.Password }, TestContext.CancellationToken);

        return client;
    }

    private async Task<Guid> CreateTeam(AppClient client, string title)
        => (await client.GetController<ITenantController>().Create(new() { Name = $"t{Guid.NewGuid():N}"[..20], Title = title }, TestContext.CancellationToken)).Id;

    private async Task MoveTrial(AppTestServer server, Guid project, DateTimeOffset issuedOn)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProjectTrials
            .Where(trial => trial.Id == project)
            .ExecuteUpdateAsync(setters => setters.SetProperty(trial => trial.IssuedOn, issuedOn).SetProperty(trial => trial.ExpiresOn, issuedOn.AddDays(30)), TestContext.CancellationToken);
    }

    /// <summary>The project's Licenses, and the team's project made with the first.</summary>
    private async Task MoveLicense(AppTestServer server, Guid project, DateTimeOffset issuedOn)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.IssuedLicenses.IgnoreQueryFilters()
            .Where(license => license.Project!.BitProjectId == project)
            .ExecuteUpdateAsync(setters => setters.SetProperty(license => license.IssuedOn, issuedOn), TestContext.CancellationToken);

        await dbContext.Projects.IgnoreQueryFilters()
            .Where(row => row.BitProjectId == project)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.CreatedOn, issuedOn), TestContext.CancellationToken);
    }

    /// <summary>Monitoring watching the team's project in production, since <paramref name="since"/>.</summary>
    private async Task AddProduction(AppTestServer server, Guid team, Guid project, DateTimeOffset since)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var projectId = await dbContext.Projects.IgnoreQueryFilters().Where(row => row.TenantId == team && row.BitProjectId == project).Select(row => row.Id).SingleAsync(TestContext.CancellationToken);

        await dbContext.Monitorings.AddAsync(new ProjectMonitoring
        {
            Id = Guid.NewGuid(),
            TenantId = team,
            ProjectId = projectId,
            Environment = ProjectMonitoring.Production,
            Address = $"https://{Guid.NewGuid():N}.growth.example",
            ServerAddress = $"https://{Guid.NewGuid():N}.growth.example",
            Name = "Contoso",
            CreatedOn = since
        }, TestContext.CancellationToken);

        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
    }

    /// <summary>
    /// Takes away what a test moved to a day long past, so the page's running totals in the database the tests share start
    /// where today's data does.
    /// </summary>
    private async Task Remove(AppTestServer server, params Guid[] projects)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Monitorings.IgnoreQueryFilters().Where(row => projects.Contains(row.Project!.BitProjectId!.Value)).ExecuteDeleteAsync(CancellationToken.None);
        await dbContext.IssuedLicenses.IgnoreQueryFilters().Where(row => projects.Contains(row.Project!.BitProjectId!.Value)).ExecuteDeleteAsync(CancellationToken.None);
        await dbContext.Projects.IgnoreQueryFilters().Where(row => (row.BitProjectId != null && projects.Contains(row.BitProjectId.Value))).ExecuteDeleteAsync(CancellationToken.None);
        await dbContext.ProjectTrials.Where(row => projects.Contains(row.Id)).ExecuteDeleteAsync(CancellationToken.None);
    }

    /// <summary>A day of a year long past, so no other test's rows, made today, fall on it or the 40 days after.</summary>
    private static DateOnly RandomPastDay() => new DateOnly(1900, 1, 1).AddDays(Random.Shared.Next(0, 365 * 100));

    private static DateTimeOffset At(DateOnly day, int hour) => new(day.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero);

    private static string NewCommit() => Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()) + Convert.ToHexStringLower(Guid.NewGuid().ToByteArray())[..8];
}
