using Bit.Fabric.Server.Api.Features.Tenants;
using Bit.Fabric.Shared.Features.Tenants;
using Bit.Fabric.Shared.Features.Tenants.Dtos;
using Bit.Fabric.Shared.Infrastructure.Services.Contracts;

namespace Bit.Fabric.Tests.Features.Tenants;

/// <summary>
/// GetTenants takes ODataQueryOptions, which the OpenAPI document leaves out for the $filter, $top and the rest it is bound
/// from (Program.Services.cs). Leaving it out must change nothing in what the action reads from the query string.
/// </summary>
[TestClass, TestCategory("IntegrationTest")]
public partial class TenantManagementQueryTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task GetTenants_Should_FilterSortAndPageByTheQueryOptions()
    {
        await using var server = new AppTestServer();
        await server.Build().Start(TestContext.CancellationToken);

        // Three teams of this test alone, whose names differ only in their last letter, so every collation sorts them alike.
        var prefix = $"q{Guid.NewGuid():N}"[..16];

        await using (var scope = server.ApiApp.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await dbContext.Tenants.AddRangeAsync(new[] { "c", "a", "b" }.Select(letter => new Tenant { Name = $"{prefix}{letter}", Title = "Query options test" }), TestContext.CancellationToken);

            await dbContext.SaveChangesAsync(TestContext.CancellationToken);
        }

        await using var client = server.CreateAppClient();

        await client.AuthManager.SignIn(new()
        {
            Email = TestData.DefaultTestEmail,
            Password = TestData.DefaultTestPassword
        }, TestContext.CancellationToken);

        var tenantManagementController = client.GetController<ITenantManagementController>();
        var ofThisTest = $"startswith({nameof(TenantDto.Name)},'{prefix}')";

        var all = await tenantManagementController.WithQuery(new ODataQuery { Filter = ofThisTest, OrderBy = $"{nameof(TenantDto.Name)} asc" }.ToString())
            .GetTenants(TestContext.CancellationToken);

        Assert.AreEqual(3, all.TotalCount);
        Assert.AreSequenceEqual(new[] { $"{prefix}a", $"{prefix}b", $"{prefix}c" }, all.Items.Select(t => t.Name).ToArray());

        var page = await tenantManagementController.WithQuery(new ODataQuery { Filter = ofThisTest, OrderBy = $"{nameof(TenantDto.Name)} desc", Top = 1, Skip = 1 }.ToString())
            .GetTenants(TestContext.CancellationToken);

        // The count is of every team the filter finds, before the page is taken.
        Assert.AreEqual(3, page.TotalCount);
        Assert.AreEqual($"{prefix}b", page.Items.Single().Name);
    }
}
