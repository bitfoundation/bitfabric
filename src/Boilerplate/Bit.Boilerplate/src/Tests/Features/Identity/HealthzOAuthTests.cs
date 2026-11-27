using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Boilerplate.Shared.Features.Diagnostic;
using Boilerplate.Shared.Features.Identity.OAuth;
using Boilerplate.Shared.Features.Identity.OAuth.Dtos;

namespace Boilerplate.Tests.Features.Identity;

/// <summary>
/// The way a monitor such as bit Fabric reads this app's health, end to end: it discovers <c>/healthz</c>'s protected
/// resource metadata, gets the <c>healthz</c> scope through the authorization code flow with PKCE, reads
/// <c>/healthz/v1</c> with the token and renews it with the refresh token. A change that breaks any step stops every
/// monitor connected to the app at once, while the app itself keeps working, so nothing else would show it.
/// </summary>
[TestClass, TestCategory("IntegrationTest")]
public class HealthzOAuthTests
{
    public TestContext TestContext { get; set; } = default!;

    private const string TestClientId = "test-monitor";
    private const string TestRedirectUri = "http://127.0.0.1:33418/callback";

    [TestMethod]
    public async Task AMonitor_Should_ConnectThroughOAuth_ReadTheHealthReport_AndRenewItsToken()
    {
        await using var server = new AppTestServer();
        await server.Build(configureTestConfiguration: configuration =>
        {
            // A real monitor's client id is the https url of its metadata document, which a loopback server can't fetch.
            configuration["OAuth:Clients:0:ClientId"] = TestClientId;
            configuration["OAuth:Clients:0:ClientName"] = "Test monitor";
            configuration["OAuth:Clients:0:RedirectUris:0"] = TestRedirectUri;
        }).Start(TestContext.CancellationToken);

        var issuer = server.ApiAppAddress.ToString().TrimEnd('/');
        using var httpClient = server.CreateRawHttpClient(new HttpClientHandler { AllowAutoRedirect = false });

        // 1. Without a token, /healthz/v1 says where to go and what to ask for.
        using (var anonymous = await httpClient.GetAsync("healthz/v1", TestContext.CancellationToken))
        {
            Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.StatusCode);

            var challenge = string.Join(' ', anonymous.Headers.WwwAuthenticate.Select(header => $"{header.Scheme} {header.Parameter}"));

            Assert.Contains($"resource_metadata=\"{issuer}/.well-known/oauth-protected-resource/healthz\"", challenge);
            Assert.Contains($"scope=\"{OAuthScopes.Healthz}\"", challenge);
        }

        // 2. The protected resource metadata names this server and the scope, and the template version, before any token.
        using (var metadata = JsonDocument.Parse(await httpClient.GetStringAsync(".well-known/oauth-protected-resource/healthz", TestContext.CancellationToken)))
        {
            Assert.AreEqual($"{issuer}/healthz", metadata.RootElement.GetProperty("resource").GetString());
            Assert.AreSequenceEqual([issuer], metadata.RootElement.GetProperty("authorization_servers").EnumerateArray().Select(item => item.GetString()).ToArray());
            Assert.AreSequenceEqual([OAuthScopes.Healthz], metadata.RootElement.GetProperty("scopes_supported").EnumerateArray().Select(item => item.GetString()).ToArray());
            Assert.AreEqual(BitJsonVersion(), metadata.RootElement.GetProperty("bit_template_version").GetString(),
                "The version the build read from bit.json, so a monitor can tell an app made from bit-bp and its age.");
        }

        using (var authorizationServer = JsonDocument.Parse(await httpClient.GetStringAsync(".well-known/oauth-authorization-server", TestContext.CancellationToken)))
        {
            Assert.AreEqual(issuer, authorizationServer.RootElement.GetProperty("issuer").GetString());
            Assert.AreEqual($"{issuer}/oauth/authorize", authorizationServer.RootElement.GetProperty("authorization_endpoint").GetString());
            Assert.AreEqual($"{issuer}/oauth/token", authorizationServer.RootElement.GetProperty("token_endpoint").GetString());
            Assert.Contains("S256", authorizationServer.RootElement.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(item => item.GetString()).ToArray());
            Assert.Contains("refresh_token", authorizationServer.RootElement.GetProperty("grant_types_supported").EnumerateArray().Select(item => item.GetString()).ToArray());
        }

        // 3. The browser goes to /oauth/authorize, which hands it to the consent page with the request intact.
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var request = new OAuthAuthorizeRequestDto
        {
            ClientId = TestClientId,
            RedirectUri = TestRedirectUri,
            ResponseType = "code",
            Scope = OAuthScopes.Healthz,
            State = "test-state",
            CodeChallenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
            CodeChallengeMethod = "S256",
            Resource = $"{issuer}/healthz"
        };

        using (var authorize = await httpClient.GetAsync(QueryHelpers.AddQueryString("oauth/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = request.ClientId,
            ["redirect_uri"] = request.RedirectUri,
            ["response_type"] = request.ResponseType,
            ["scope"] = request.Scope,
            ["state"] = request.State,
            ["code_challenge"] = request.CodeChallenge,
            ["code_challenge_method"] = request.CodeChallengeMethod,
            ["resource"] = request.Resource
        }), TestContext.CancellationToken))
        {
            Assert.AreEqual(HttpStatusCode.Redirect, authorize.StatusCode);
            Assert.Contains(PageUrls.OAuthConsent, authorize.Headers.Location?.OriginalString ?? "");
        }

        // 4. A user holding Operations_View, signed in with two factor, approves on the consent page.
        await using var client = server.CreateAppClient();
        var userId = await SignInWithTwoFactorAsGlobalAdmin(server, client);

        try
        {
            var oauthController = client.GetController<IOAuthController>();

            var consent = await oauthController.Review(request, TestContext.CancellationToken);
            Assert.AreSequenceEqual([OAuthScopes.Healthz], consent.Scopes);

            var approval = await oauthController.Approve(request, TestContext.CancellationToken);
            var redirectQuery = QueryHelpers.ParseQuery(new Uri(approval.RedirectUrl).Query);

            Assert.AreEqual(request.State, redirectQuery["state"].ToString());
            Assert.AreEqual(issuer, redirectQuery["iss"].ToString());

            // 5. The code and the PKCE verifier buy the token pair.
            var (status, body) = await PostToken(server, new()
            {
                ["grant_type"] = "authorization_code",
                ["code"] = redirectQuery["code"].ToString(),
                ["client_id"] = TestClientId,
                ["redirect_uri"] = TestRedirectUri,
                ["code_verifier"] = verifier
            });

            Assert.AreEqual(HttpStatusCode.OK, status, $"The code exchange failed: {body}");

            using var granted = JsonDocument.Parse(body);
            var accessToken = granted.RootElement.GetProperty("access_token").GetString()!;
            var refreshToken = granted.RootElement.GetProperty("refresh_token").GetString()!;
            Assert.AreEqual(OAuthScopes.Healthz, granted.RootElement.GetProperty("scope").GetString());

            // 6. The token reads the health report.
            await AssertReadsTheHealthReport(server, client, accessToken);

            // 7. The refresh token renews the access token, and the new one reads the report too.
            (status, body) = await PostToken(server, new()
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = TestClientId
            });

            Assert.AreEqual(HttpStatusCode.OK, status, $"The refresh failed: {body}");

            using var refreshed = JsonDocument.Parse(body);
            var newRefreshToken = refreshed.RootElement.GetProperty("refresh_token").GetString()!;
            Assert.AreNotEqual(refreshToken, newRefreshToken, "Refresh tokens rotate.");

            await AssertReadsTheHealthReport(server, client, refreshed.RootElement.GetProperty("access_token").GetString()!);

            // 8. A refresh that can't succeed answers 400 invalid_grant, never 500: the monitor reconnects on the one and
            // retries later on the other, so telling them apart is what keeps it from retrying a dead approval forever.
            await AssertInvalidGrant(server, refreshToken, "The rotated-out refresh token, whose replay revokes the approval.");
            await AssertInvalidGrant(server, newRefreshToken, "The latest refresh token of an approval that was revoked, whose session row is gone.");
            await AssertInvalidGrant(server, "not-a-token", "A refresh token this server never issued.");
        }
        finally
        {
            await RevokeGlobalAdmin(server, userId);
        }
    }

    private async Task AssertReadsTheHealthReport(AppTestServer server, AppClient client, string accessToken)
    {
        using var httpClient = server.CreateRawHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "healthz/v1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, TestContext.CancellationToken);

        // Unhealthy is a report too: a check that needs an outside service may fail on a developer's machine.
        Assert.IsTrue(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"The token should read /healthz/v1, but it answered {(int)response.StatusCode}.");

        var report = await response.Content.ReadFromJsonAsync(
            client.Services.GetRequiredService<JsonSerializerOptions>().GetTypeInfo<HealthReportDto>(), TestContext.CancellationToken);

        Assert.IsNotNull(report);
        Assert.IsNotEmpty(report.Entries, "The report lists every check.");
    }

    private async Task AssertInvalidGrant(AppTestServer server, string refreshToken, string what)
    {
        var (status, body) = await PostToken(server, new()
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = TestClientId
        });

        Assert.AreEqual(HttpStatusCode.BadRequest, status, $"{what} Body: {body}");
        Assert.AreEqual("invalid_grant", JsonDocument.Parse(body).RootElement.GetProperty("error").GetString(), what);
    }

    private async Task<(HttpStatusCode Status, string Body)> PostToken(AppTestServer server, Dictionary<string, string> form)
    {
        using var httpClient = server.CreateRawHttpClient();
        using var content = new FormUrlEncodedContent(form);
        using var response = await httpClient.PostAsync("oauth/token", content, TestContext.CancellationToken);

        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.CancellationToken));
    }

    /// <summary>
    /// Granting <c>healthz</c> takes <c>Operations_View</c> and a session that signed in with two factor. The seeded
    /// account has no two factor and other tests sign in with its password alone, so this makes an account of its own,
    /// global admin, with two factor on, and signs in with its password and a recovery code.
    /// </summary>
    private async Task<Guid> SignInWithTwoFactorAsGlobalAdmin(AppTestServer server, AppClient client)
    {
        const string password = "P@ssw0rdP@ssw0rd";
        var email = $"{Guid.NewGuid():N}@healthz.test";
        string recoveryCode;
        Guid userId;

        await using (var scope = server.ApiApp.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = new User { Email = email, UserName = email, EmailConfirmed = true, LockoutEnabled = true };
            var created = await userManager.CreateAsync(user, password);
            Assert.IsTrue(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Description)));

            await userManager.ResetAuthenticatorKeyAsync(user);
            await userManager.SetTwoFactorEnabledAsync(user, true);
            recoveryCode = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 1))!.Single();

            var globalAdminRoleId = await dbContext.Roles.Where(role => role.Name == AppRoles.GlobalAdmin).Select(role => role.Id).SingleAsync(TestContext.CancellationToken);
            await dbContext.UserRoles.AddAsync(new UserRole { UserId = user.Id, RoleId = globalAdminRoleId }, TestContext.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.CancellationToken);

            userId = user.Id;
        }

        try
        {
            var requiresTwoFactor = await client.AuthManager.SignIn(new() { Email = email, Password = password, TwoFactorCode = recoveryCode }, TestContext.CancellationToken);
            Assert.IsFalse(requiresTwoFactor, "The recovery code should have completed the two factor sign-in.");
        }
        catch
        {
            // The caller's finally starts only once this returns, so a failed sign-in takes the role back here.
            await RevokeGlobalAdmin(server, userId);
            throw;
        }

        return userId;
    }

    /// <summary>The test database outlives the run, so the account must not stay a global admin there.</summary>
    private static async Task RevokeGlobalAdmin(AppTestServer server, Guid userId)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var globalAdminRoleId = await dbContext.Roles.Where(role => role.Name == AppRoles.GlobalAdmin).Select(role => role.Id).SingleAsync();

        await dbContext.UserRoles.Where(userRole => userRole.UserId == userId && userRole.RoleId == globalAdminRoleId).ExecuteDeleteAsync();
    }

    /// <summary>The version in the <c>bit.json</c> above the test binaries: the template's own, or a generated project's.</summary>
    private static string? BitJsonVersion()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && File.Exists(Path.Combine(directory.FullName, "bit.json")) is false)
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory, "No bit.json above the test binaries.");

        using var bitJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "bit.json")));

        return bitJson.RootElement.GetProperty("version").GetString();
    }
}
