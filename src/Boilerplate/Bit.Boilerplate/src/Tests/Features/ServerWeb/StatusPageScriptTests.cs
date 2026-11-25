using Microsoft.Extensions.Configuration;

namespace Boilerplate.Tests.Features.ServerWeb;

/// <summary>
/// The bar the app's status page on bit Fabric shows at the top of the web app while something is wrong: Server.Web loads
/// the page's script when <c>StatusPageScriptUrl</c> names it, and the Content-Security-Policy allows the script's origin.
/// Left empty, as it ships, nothing is loaded and nothing is allowed.
/// </summary>
[TestClass, TestCategory("IntegrationTest")]
public class StatusPageScriptTests
{
    private const string ScriptUrl = "https://status.example.invalid/api/v1/PublicStatus/Embed/the-app";

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [DataRow(ScriptUrl, DisplayName = "Set")]
    [DataRow(null, DisplayName = "Empty")]
    public async Task TheWebApp_Should_LoadTheStatusPageScript_OnlyWhenOneIsSet(string? scriptUrl)
    {
        await using var server = new AppTestServer();
        await server.Build(
            configureTestServices: services => services.FakeExternalStatistics(),
            configureTestConfiguration: configuration => configuration["StatusPageScriptUrl"] = scriptUrl)
            .Start(TestContext.CancellationToken);

        using var visitorHttpClient = new HttpClient { BaseAddress = server.WebAppAddress };

        var html = await visitorHttpClient.GetStringAsync("/", TestContext.CancellationToken);

        // Outside Development, App.razor sends this policy as the page's header.
        var csp = Boilerplate.Client.Core.Components.Layout.ContentSecurityPolicy.BuildCspString(new(), new AbsoluteServerAddressProvider { GetAddress = () => new Uri("https://api.example.invalid/") },
            server.WebApp.Services.GetRequiredService<IConfiguration>());
        var scriptSrc = csp.Split(';').Single(directive => directive.Trim().StartsWith("script-src ", StringComparison.Ordinal));

        if (scriptUrl is null)
        {
            Assert.DoesNotContain("PublicStatus/Embed", html);
            Assert.DoesNotContain("status.example.invalid", scriptSrc);
        }
        else
        {
            Assert.Contains($"<script src=\"{scriptUrl}\" async></script>", html);
            Assert.Contains(" https://status.example.invalid", scriptSrc, "The policy allows the script's origin, or the browser blocks it.");
        }
    }
}
