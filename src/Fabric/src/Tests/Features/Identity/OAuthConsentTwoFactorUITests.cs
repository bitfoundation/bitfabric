using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Bit.Fabric.Shared.Features.Identity.OAuth;
using Bit.Fabric.Shared.Infrastructure.Services;

namespace Bit.Fabric.Tests.Features.Identity;

/// <summary>
/// An MCP client asks for <c>/dev-mcp</c>, which takes an account that signed in with two-factor authentication. The
/// consent page used to answer an account without it with a dead end - "this request cannot be authorized", go home - so
/// the user had to find the setting and start the client's sign-in over. Now the page says what is needed, leads to the
/// setting, and the setting leads back to the same request.
/// </summary>
[TestClass, TestCategory("UITest"), Retry(2)]
public partial class OAuthConsentTwoFactorUITests : AppPageTest
{
    private const string TestClientId = "test-mcp-client";
    private const string TestRedirectUri = "http://127.0.0.1:33418/callback";

    [TestMethod]
    public async Task AnAdminWithoutTwoFactor_Should_TurnItOnFromTheConsentPage_AndComeBackToAuthorize()
    {
        await using var server = new AppTestServer(Context);
        await server.Build(configureTestConfiguration: configuration =>
        {
            configuration["OAuth:Clients:0:ClientId"] = TestClientId;
            configuration["OAuth:Clients:0:ClientName"] = "Test MCP Client";
            configuration["OAuth:Clients:0:RedirectUris:0"] = TestRedirectUri;
        }).Start(TestContext.CancellationToken);

        var email = await TestUsers.SignUp(server, TestContext.CancellationToken);
        var userId = await GrantGlobalAdmin(server, email);

        try
        {
            await SignInPanelUtils.SignIn(Page, server, email, TestUsers.Password, email);

            await Page.GotoAsync(ConsentUrl(server), new() { WaitUntil = WaitUntilState.NetworkIdle });

            // The page says what is missing, and offers the way to it.
            await Expect(Page.GetByText(AppStrings.OAuthConsentTwoFactorRequiredTitle)).ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.OAuthConsentTurnOnTwoFactor }).ClickAsync();

            // The settings' two-factor section, where the test plays the authenticator app.
            var qrLink = Page.Locator("a[href^='otpauth']").First;
            await qrLink.WaitForAsync();
            var authenticatorUri = await qrLink.GetAttributeAsync("href") ?? throw new InvalidOperationException("The otpauth link has no href.");
            var secret = Regex.Match(authenticatorUri, "secret=([^&]+)").Groups[1].Value;

            // Filling the last digit enables it (TwoFactorSection's OnFill), so no click on Verify.
            await Page.Locator(".bit-otp-inp").First.FillAsync(ComputeTotp(secret));

            // Turning it on doesn't put the second factor into this session, so the way back is a new sign-in.
            await Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.TfaReturnSignInAgain }).ClickAsync();

            await Expect(Page).ToHaveTitleAsync(AppStrings.SignInPageTitle);
            await SignInPanelUtils.FillCredentials(Page, email, TestUsers.Password);
            await Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.Continue, Exact = true }).ClickAsync();

            await Expect(Page.GetByText(AppStrings.TfaPanelTitle)).ToBeVisibleAsync();
            await Page.Locator(".bit-otp-inp").First.FillAsync(ComputeTotp(secret));

            // Back on the consent page of the same request, which this session can now authorize.
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.Authorize })).ToBeVisibleAsync();
            Assert.Contains(PageUrls.OAuthConsent, Page.Url);
            await Expect(Page.GetByText(AppStrings.OAuthConsentTwoFactorRequiredTitle)).ToHaveCountAsync(0);
        }
        finally
        {
            await RevokeGlobalAdmin(server, userId);
        }
    }

    /// <summary>The consent page the authorize endpoint redirects an MCP client's browser to, asking for /dev-mcp.</summary>
    private static string ConsentUrl(AppTestServer server)
    {
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(RandomNumberGenerator.GetBytes(32)));

        return QueryHelpers.AddQueryString(new Uri(server.WebAppAddress, PageUrls.OAuthConsent).ToString(), new Dictionary<string, string?>
        {
            ["client_id"] = TestClientId,
            ["redirect_uri"] = TestRedirectUri,
            ["response_type"] = "code",
            ["scope"] = OAuthScopes.DevMcp,
            ["state"] = "test-state",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            // The issuer is the api the page calls (See GetIssuer).
            ["resource"] = $"{server.ApiAppAddress.ToString().TrimEnd('/')}/dev-mcp"
        });
    }

    /// <summary>
    /// The seeded g-admin role, which holds System.DevMcp. Written directly, since only a global admin can grant it, and
    /// revoked when the test ends: the database outlives the run.
    /// </summary>
    private static async Task<Guid> GrantGlobalAdmin(AppTestServer server, string email)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var userId = await dbContext.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
        var roleId = await dbContext.Roles.Where(role => role.Name == AppRoles.GlobalAdmin).Select(role => role.Id).SingleAsync();

        await dbContext.UserRoles.AddAsync(new UserRole { UserId = userId, RoleId = roleId });
        await dbContext.SaveChangesAsync();

        return userId;
    }

    private static async Task RevokeGlobalAdmin(AppTestServer server, Guid userId)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var roleId = await dbContext.Roles.Where(role => role.Name == AppRoles.GlobalAdmin).Select(role => role.Id).SingleAsync();

        await dbContext.UserRoles.Where(userRole => userRole.UserId == userId && userRole.RoleId == roleId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// What an authenticator app shows for <paramref name="base32Secret"/> now: RFC 6238 with ASP.NET Core Identity's
    /// parameters, HMAC-SHA1, 30 second steps and 6 digits.
    /// </summary>
    private static string ComputeTotp(string base32Secret)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);

        var hash = HMACSHA1.HashData(Base32Decode(base32Secret), counter);
        var offset = hash[^1] & 0x0F;
        var binary = BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset, 4)) & 0x7FFFFFFF;

        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Decode(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        List<byte> bytes = [];
        int buffer = 0, bits = 0;

        foreach (var character in Uri.UnescapeDataString(base32).TrimEnd('=').ToUpperInvariant())
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character);
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(buffer >> bits));
                buffer &= (1 << bits) - 1;
            }
        }

        return [.. bytes];
    }
}
