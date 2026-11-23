namespace Bit.Fabric.Tests.Features.Configuration;

/// <summary>
/// A deployment's servers send their telemetry to the dashboard over gRPC inside the Docker Compose network: an address a
/// page and an app can't reach, in a protocol a page can't speak. So the CD builds the app with the web app's own address
/// as <c>Belemetry:OtlpEndpoint</c>, whose <c>/v1/</c> paths the tunnel sends to the dashboard's OTLP/HTTP port (See
/// .docs/15, In a Deployment).
/// </summary>
[TestClass, TestCategory("IntegrationTest")]
public class BrowserTelemetryTests
{
    public TestContext TestContext { get; set; } = default!;

    /// <summary>
    /// Server.Web writes the browser script's settings into every page it serves, before Blazor starts and in Blazor
    /// Server, where nothing else would export what only the page sees: its errors, page views and web vitals.
    /// </summary>
    [TestMethod]
    public async Task ThePage_Should_PostToTheWebAppsAddress_WhileTheServerSendsOverGrpc()
    {
        await using var server = new AppTestServer();
        await server.Build(configureTestConfiguration: configuration =>
            {
                configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://compose-dashboard:18889";
                configuration["OTEL_EXPORTER_OTLP_PROTOCOL"] = "grpc";
                configuration["Belemetry:OtlpEndpoint"] = "https://app.example.invalid";
            })
            .Start(TestContext.CancellationToken);

        using var visitor = new HttpClient { BaseAddress = server.WebAppAddress };

        var html = await visitor.GetStringAsync("/en-US/", TestContext.CancellationToken);

        Assert.Contains("{\"type\":\"otlp\",\"endpoint\":\"https://app.example.invalid/\",\"protocol\":\"http/protobuf\"", html);
        Assert.DoesNotContain("compose-dashboard", html, "A page never gets the servers' own endpoint.");
    }
}
