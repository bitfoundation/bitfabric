using Bit.Fabric.Server.Api.Infrastructure.Data;

namespace Bit.Fabric.Tests.Features.Data;

/// <summary>
/// Template 11.0.0-pre-21 renamed TenantUsers to TenantMemberships and Users.HasProfilePicture to HasProfileImage. 0.0.13 kept
/// the old names next to the new ones, so 0.0.12 kept working while its migrations ran, and the release after it dropped them
/// (See bitfabric-internal/specs/changes/2026-11-17-drop-old-names.md).
/// </summary>
[TestClass, TestCategory("IntegrationTest")]
public partial class OldDatabaseNamesTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task TheOldTenantUsersAndHasProfilePictureNames_Should_BeGone()
    {
        await using var server = new AppTestServer();
        await server.Build().Start(TestContext.CancellationToken);

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The new names are found, so the queries below can't pass by finding nothing at all.
        Assert.IsTrue(await Query(dbContext, $"""SELECT to_regclass('"TenantMemberships"') IS NOT NULL AS "Value" """));
        Assert.IsTrue(await Query(dbContext, $"""SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'Users' AND column_name = 'HasProfileImage') AS "Value" """));

        Assert.IsFalse(await Query(dbContext, $"""SELECT to_regclass('"TenantUsers"') IS NOT NULL AS "Value" """));
        Assert.IsFalse(await Query(dbContext, $"""SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'Users' AND column_name = 'HasProfilePicture') AS "Value" """));
        Assert.IsFalse(await Query(dbContext, $"""SELECT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'Users_SyncHasProfileImage') AS "Value" """));
        Assert.IsFalse(await Query(dbContext, $"""SELECT to_regprocedure('"Users_SyncHasProfileImage"()') IS NOT NULL AS "Value" """));
    }

    private async Task<bool> Query(AppDbContext dbContext, FormattableString sql)
    {
        return (await dbContext.Database.SqlQuery<bool>(sql).ToListAsync(TestContext.CancellationToken)).Single();
    }
}
