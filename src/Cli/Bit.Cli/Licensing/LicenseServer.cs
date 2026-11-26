using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Cli.Infrastructure;

namespace Bit.Cli.Licensing;

/// <summary>bit Fabric's two endpoints bit calls for a license. Either answers null when bit Fabric can't be reached.</summary>
public sealed class LicenseServer(LicenseOptions options, CliLog log)
{
    public const string TrialPath = "api/v1/License/Trial";

    public const string ClaimPath = "api/v1/License/Claim";

    /// <summary>
    /// A new project's trial, which <c>bit new</c> asks for right after the project's first commit. <paramref name="ci"/>
    /// names the CI it runs in (See CliEnvironment.CiName), and is left out elsewhere: bit Fabric only keeps the trials made
    /// in CI out of its figures. The trial, and what it allows, is the same either way.
    /// </summary>
    public Task<string?> TrialAsync(Guid project, string name, string commit, string? ci, CancellationToken cancellationToken)
    {
        var request = new JsonObject { ["project"] = project.ToString("D"), ["name"] = name, ["commit"] = commit };

        if (ci is not null)
        {
            request["ci"] = ci;
        }

        var body = request.ToJsonString();

        return SendAsync(new HttpRequestMessage(HttpMethod.Post, TrialPath) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, cancellationToken);
    }

    /// <summary>The license the license page issued with <paramref name="code"/>; null until then.</summary>
    public Task<string?> ClaimAsync(string code, CancellationToken cancellationToken)
    {
        return SendAsync(new HttpRequestMessage(HttpMethod.Get, $"{ClaimPath}?code={Uri.EscapeDataString(code)}"), cancellationToken);
    }

    private async Task<string?> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var ownHandler = options.Http is null ? new HttpClientHandler() : null;
        using var http = new HttpClient(options.Http ?? ownHandler!, disposeHandler: false) { BaseAddress = new Uri(LicenseOptions.ApiAddress), Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"bit-cli/{BuildInfo.Version.Split('+')[0]}");

        try
        {
            using (request)
            using (var response = await http.SendAsync(request, cancellationToken))
            {
                if (response.IsSuccessStatusCode is false)
                {
                    log.Write($"bit Fabric answered {request.RequestUri} with {(int)response.StatusCode}.");
                    return null;
                }

                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

                return json.RootElement.ValueKind is JsonValueKind.Object && json.RootElement.TryGetProperty("license", out var license) && license.ValueKind is JsonValueKind.String
                    ? license.GetString()
                    : null;
            }
        }
        catch (Exception exp) when (exp is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException && cancellationToken.IsCancellationRequested is false)
        {
            log.Write($"bit Fabric couldn't be reached for {request.RequestUri}: {exp.Message}");
            return null;
        }
    }
}
