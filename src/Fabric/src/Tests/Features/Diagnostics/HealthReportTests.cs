using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Client.Core.Infrastructure.Services.HttpMessageHandlers;

namespace Bit.Fabric.Tests.Features.Diagnostics;

/// <summary>
/// <c>/healthz</c> is where bit Fabric's own health is read: its monitoring reads it with a token, as it reads any other
/// app's (bitfabric-internal/specs/monitoring.md), and a global admin reads it signed in.
/// </summary>
[TestClass, TestCategory("IntegrationTest")]
public partial class HealthReportTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task TheHealthReport_Should_ShowAGlobalAdminEveryCheck_AndNobodyElse()
    {
        await using var server = new AppTestServer();
        await server.Build().Start(TestContext.CancellationToken);

        var healthzUrl = new Uri(server.ApiAppAddress, "healthz");

        using (var anonymousClient = server.CreateRawHttpClient())
        using (var anonymousResponse = await anonymousClient.GetAsync(healthzUrl, TestContext.CancellationToken))
        {
            Assert.AreEqual(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode, "An anonymous caller must not read the report.");
        }

        var email = await TestUsers.SignUp(server, TestContext.CancellationToken);

        await using (var userClient = server.CreateAppClient())
        {
            await userClient.AuthManager.SignIn(new() { Email = email, Password = TestUsers.Password }, TestContext.CancellationToken);

            await Assert.ThrowsExactlyAsync<ForbiddenException>(
                () => userClient.HttpClient.GetAsync(healthzUrl, TestContext.CancellationToken),
                "A signed-in user without the feature must not read the report.");
        }

        await using var adminClient = server.CreateAppClient();
        await adminClient.AuthManager.SignIn(new() { Email = TestData.DefaultTestEmail, Password = TestData.DefaultTestPassword }, TestContext.CancellationToken);

        using HttpRequestMessage request = new(HttpMethod.Get, healthzUrl);
        // A check that fails here makes the answer a 503, with the report in its body all the same.
        request.Options.Set(new HttpRequestOptionsKey<HttpStatusCode[]>(RequestOptionNames.AllowedStatusCodes), [HttpStatusCode.ServiceUnavailable]);
        using var response = await adminClient.HttpClient.SendAsync(request, TestContext.CancellationToken);

        var report = await response.Content.ReadFromJsonAsync(
            adminClient.Services.GetRequiredService<JsonSerializerOptions>().GetTypeInfo<HealthReportDto>(), TestContext.CancellationToken);

        Assert.IsNotNull(report);

        foreach (var check in new[] { "AppDbContext", "hangfire", "binStorage" })
        {
            Assert.IsTrue(report.Entries.ContainsKey(check), $"The report names '{check}'.");
        }
    }
}
