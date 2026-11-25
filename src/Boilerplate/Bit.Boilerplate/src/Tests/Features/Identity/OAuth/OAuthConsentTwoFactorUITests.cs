using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using OtpNet;
using Boilerplate.Shared.Features.Identity.OAuth;

namespace Boilerplate.Tests.Features.Identity.OAuth;

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

        // The account and its g-admin role first, so the browser's sign-in below already carries System.DevMcp.
        await using var client = server.CreateAppClient();
        var (email, userId) = await TestAccountUtils.CreateAndSignIn(client, TestContext.CancellationToken);
        await using var grant = await TestAccountUtils.MakeGlobalAdmin(client, userId, TestContext.CancellationToken);

        await MagicLinkSignInUtils.SignInAgainViaMagicLinkOtp(Page, server, email, TestContext.CancellationToken);

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
        await BitOtpInputUtils.FillOtpInputs(Page, ComputeTotp(secret));

        // Turning it on doesn't put the second factor into this session, so the way back is a new sign-in.
        await Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.TfaReturnSignInAgain }).ClickAsync();

        await Expect(Page).ToHaveTitleAsync(AppStrings.SignInPageTitle);
        await MagicLinkSignInUtils.RequestMagicLinkAndOtpOnCurrentPanel(Page, email);
        var otpEmail = await server.WaitForCapturedEmail(email, captured => captured.Kind is CapturedEmailKind.Otp, TestContext.CancellationToken);
        await BitOtpInputUtils.FillOtpInputs(Page, otpEmail.Token!);

        await Expect(Page.GetByText(AppStrings.TfaPanelTitle)).ToBeVisibleAsync();
        await BitOtpInputUtils.FillOtpInputs(Page, ComputeTotp(secret));

        // Back on the consent page of the same request, which this session can now authorize.
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.Authorize })).ToBeVisibleAsync();
        Assert.Contains(PageUrls.OAuthConsent, Page.Url);
        await Expect(Page.GetByText(AppStrings.OAuthConsentTwoFactorRequiredTitle)).ToHaveCountAsync(0);
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

    private static string ComputeTotp(string base32Secret) => new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();
}
