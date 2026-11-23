using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// bit Fabric's client metadata document (CIMD): its OAuth client id is this document's own address, so a customer's app
/// learns who asks for its health, and where the answer may go, without any registration
/// (bitfabric-internal/specs/monitoring.md).
/// </summary>
[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]"), AllowAnonymous]
public partial class MonitoringClientController : AppControllerBase
{
    [AutoInject] private MonitoringOAuthClient oauthClient = default!;
    [AutoInject] private IFusionCache cache = default!;

    [HttpGet]
    public IActionResult Metadata()
    {
        var webAppUrl = Request.GetWebAppUrl().GetLeftPart(UriPartial.Authority);

        return new JsonResult(new MonitoringClientMetadata
        {
            ClientId = oauthClient.ClientIdFor(Request.GetIssuer()),
            ClientName = "bit Fabric",
            ClientUri = webAppUrl,
            RedirectUris = [$"{Request.GetIssuer().TrimEnd('/')}{MonitoringOAuthClient.CallbackPath}"],
            GrantTypes = ["authorization_code", "refresh_token"],
            ResponseTypes = ["code"],
            // A public client: PKCE proves it's the same one that asked, and it holds no secret.
            TokenEndpointAuthMethod = "none",
            Scope = MonitoringOAuthClient.HealthzScope
        }, MonitoringJsonContext.Default.Options);
    }

    /// <summary>
    /// Where a customer's app sends the browser back after its sign-in and approval page, which the add page opened in a
    /// popup, a new tab or the system browser. It hands the answer to that page the way external sign-in
    /// does: web-interop-app.html passes it to the window that opened it, or to the native app's local server, and closes.
    /// The code is useless here: only the add page's CompleteConnect, with the verifier kept on the server, can spend it.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Callback(string? state, string? code, string? error, string? iss, CancellationToken cancellationToken)
    {
        var pending = string.IsNullOrEmpty(state) ? null : await cache.GetOrDefaultAsync<string>(ProjectsController.PendingKey(state), token: cancellationToken) is { } json
            ? JsonSerializer.Deserialize(json, MonitoringJsonContext.Default.PendingConnection)
            : null;

        var answer = QueryHelpers.AddQueryString(PageUrls.AddProjectCallback, new Dictionary<string, string?>
        {
            ["state"] = state,
            ["code"] = code,
            ["error"] = error,
            ["iss"] = iss
        }.Where(parameter => parameter.Value is not null));

        var interop = $"{PageUrls.WebInteropApp}?actionName=ExternalSignInCallback&url={Uri.EscapeDataString(answer)}";

        // Check out Client.Web/wwwroot/web-interop-app.html's comments.
        if (pending?.LocalHttpPort is { } port)
            return Redirect(new Uri(new Uri($"http://localhost:{port}"), $"{interop}&localHttpPort={port}&localHttpNonce={Uri.EscapeDataString(pending.LocalHttpNonce ?? "")}").ToString());

        return Redirect(new Uri(Request.GetWebAppUrl(), interop).ToString());
    }
}
