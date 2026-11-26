namespace Bit.Fabric.Tests.Features.Configuration;

[TestClass, TestCategory("IntegrationTest")]
public partial class ForceUpdateTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task AClientWhoseMajorVersionIsBehindTheServers_Should_BeRefused()
    {
        await using var server = new AppTestServer();

        // The app's versions have no major yet, so the server is given one, and the web's minimum is taken away.
        await server.Build(configureTestConfiguration: configuration =>
            {
                configuration["ServerVersion"] = "2.5.1";
                configuration["SupportedAppVersions:MinimumSupportedWebAppVersion"] = null;
            })
            .Start(TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, await CallAs(server, "1.9.9"));
        Assert.AreEqual(HttpStatusCode.OK, await CallAs(server, "2.0.0"));
    }

    /// <summary>
    /// 0.0.23 (Teams, Licenses and Projects) reshaped the projects' and monitoring's api that every client up to 0.0.22
    /// calls, so those are asked to update instead of finding it broken, as the clients up to 0.0.12 were when template
    /// 11.0.0-pre-21 renamed the profile image's endpoints and the fields of the user and the tenant.
    /// </summary>
    [TestMethod]
    [DataRow("Web")]
    [DataRow("Windows")]
    public async Task AClientFromBeforeTheProjectsAndMonitoringChange_Should_BeAskedToUpdate(string platform)
    {
        await using var server = new AppTestServer();

        await server.Build().Start(TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, await CallAs(server, "0.0.22", platform));
        Assert.AreEqual(HttpStatusCode.OK, await CallAs(server, "0.0.23", platform));
    }

    [TestMethod]
    public async Task AMinimumVersionSetForThePlatform_Should_WinOverTheMajorVersionRule()
    {
        await using var server = new AppTestServer();

        await server.Build(configureTestConfiguration: configuration => configuration["SupportedAppVersions:MinimumSupportedWebAppVersion"] = "0.5.0")
            .Start(TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, await CallAs(server, "0.9.0"));
        Assert.AreEqual(HttpStatusCode.BadRequest, await CallAs(server, "0.4.0"));
    }

    private async Task<HttpStatusCode> CallAs(AppTestServer server, string appVersion, string platform = "Web")
    {
        using var client = server.CreateRawHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "alive");
        request.Headers.Add("X-App-Version", appVersion);
        request.Headers.Add("X-App-Platform", platform);

        using var response = await client.SendAsync(request, TestContext.CancellationToken);

        return response.StatusCode;
    }
}
