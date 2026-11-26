using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Tenants;
using Bit.Fabric.Server.Api.Infrastructure.Data;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// A project's public status page as a visitor meets it (bitfabric-internal/specs/monitoring.md, Status page): a plain page
/// from the server, with the banner, the parts of the app and their 90 days, without the app around it.
/// </summary>
[TestClass, TestCategory("UITest"), Retry(2)]
public partial class StatusPageUITests : AppPageTest
{
    [TestMethod]
    public async Task AVisitor_Should_SeeHowTheAppIsDoing_OnAPlainPage()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        var slug = await PublishAStatusPage(server);

        await Page.GotoAsync(new Uri(server.WebAppAddress, $"status/{slug}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "All systems operational" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Website and app")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Get notified" })).ToBeVisibleAsync();

        // A plain page: none of the app's own navigation around it.
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Projects" })).ToHaveCountAsync(0);

        // In the visitor's language, right to left in Persian.
        await Page.GotoAsync(new Uri(server.WebAppAddress, $"fa-IR/status/{slug}").ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("dir", "rtl");
    }

    /// <summary>A watched project with its page published, as the add flow and the Status page tab leave it.</summary>
    private async Task<string> PublishAStatusPage(AppTestServer server)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var slug = $"ui-{Guid.NewGuid():N}"[..20];

        Tenant tenant = new() { Id = Guid.CreateVersion7(), Name = slug, Title = "Test team" };
        ProjectMonitoring project = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Id,
            Address = "https://customer.example",
            ServerAddress = "https://customer.example",
            Name = "Customer app",
            CreatedOn = now.AddDays(-3),
            LastCheckedAt = now,
            Health = ProjectHealth.Operational,
            ConnectionStatus = ProjectConnectionStatus.Connected
        };
        StatusPage page = new()
        {
            Id = Guid.CreateVersion7(),
            MonitoringId = project.Id,
            TenantId = tenant.Id,
            Slug = slug,
            IsPublished = true,
            Languages = ["en-US"],
            Components = [new() { Id = Guid.CreateVersion7(), TenantId = tenant.Id, Name = [new() { Culture = "en-US", Text = "Website and app" }], CheckKeys = ["AppDbContext"] }]
        };

        await dbContext.AddRangeAsync(tenant, project, page);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        return slug;
    }
}
