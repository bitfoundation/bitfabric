using Bit.Fabric.Server.Api.Features.Identity.Components;
using Bit.Fabric.Server.Api.Features.Monitoring.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Bit.Fabric.Tests.Features.Identity;

/// <summary>
/// Every email bit Fabric sends, rendered the way the api renders it: the other tests capture an email's token and link
/// before any template runs (See TestIdentityEmailService), so a template that throws or loses its link would reach users
/// unnoticed. The rendered emails are attached to the test's results, to be looked at.
/// </summary>
[TestClass, TestCategory("IntegrationTest")]
public class EmailsTests
{
    private const string Code = "482913";

    [TestMethod]
    [DataRow("en-US")]
    [DataRow("fa-IR")]
    public async Task EveryEmail_Should_RenderInTheSharedFrame_WithItsCodeAndLink_InTheReadersLanguage(string culture)
    {
        await using var server = new AppTestServer();
        server.Build();

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        // A request to the api, as the one that asks for a code; it names no origin, so the emails link to the configured web app.
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Request.Scheme = server.ApiAppAddress.Scheme;
        httpContext.Request.Host = new(server.ApiAppAddress.Authority);
        var renderer = scope.ServiceProvider.GetRequiredService<HtmlRenderer>();
        var link = new Uri($"{server.WebAppAddress}{culture}/sign-in?otp={Code}&return-url=%2Fsettings");
        var webAppUrl = server.WebAppAddress;

        var (originalCulture, originalUICulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfoManager.SupportedCultures.Single(c => c.Culture.Name == culture).Culture;

        try
        {
            (string Name, string Html, bool HasCode, Uri? Link)[] emails =
            [
                ("confirm-email", await Render<EmailTokenTemplate>(renderer, new EmailTokenTemplateModel { Email = "sara@example.com", Token = Code, Link = link, ValidFor = TimeSpan.FromMinutes(2) }, httpContext), true, link),
                ("reset-password", await Render<ResetPasswordTokenTemplate>(renderer, new ResetPasswordTokenTemplateModel { DisplayName = "Sara", Token = Code, Link = link, ValidFor = TimeSpan.FromMinutes(2) }, httpContext), true, link),
                ("sign-in-code", await Render<OtpTemplate>(renderer, new OtpTemplateModel { DisplayName = "Sara", Token = Code, Link = link, ValidFor = TimeSpan.FromMinutes(2) }, httpContext), true, link),
                ("two-factor", await Render<TwoFactorTokenTemplate>(renderer, new TwoFactorTokenTemplateModel { DisplayName = "Sara", Token = Code, ValidFor = TimeSpan.FromMinutes(2) }, httpContext), true, null),
                ("elevated-access", await Render<ElevatedAccessTokenTemplate>(renderer, new ElevatedAccessTokenTemplateModel { DisplayName = "Sara", Token = Code, ValidFor = TimeSpan.FromMinutes(5) }, httpContext), true, null),
                ("account-exists", await Render<AccountAlreadyExistsTemplate>(renderer, new AccountAlreadyExistsTemplateModel { DisplayName = "Sara", SignInLink = link, ForgotPasswordLink = link }, httpContext), false, link),
                ("team-invitation", await Render<TenantInvitationTemplate>(renderer, new TenantInvitationTemplateModel { DisplayName = "Sara", InviterDisplayName = "Daan", TenantTitle = "Bluebird", Link = link }, httpContext), false, link),
                ("alert", await Render<ProjectAlertEmailTemplate>(renderer, new ProjectAlertEmailModel { Title = "Bluebird: the app doesn't answer", Body = "Its health checks have failed for 3 minutes.", ProjectName = "Bluebird", Link = link, WebAppUrl = webAppUrl }), false, link),
                ("weekly-scans", await Render<ProjectScansEmailTemplate>(renderer, new ProjectScansEmailModel
                {
                    DisplayName = "Sara",
                    TeamName = "Bluebird",
                    WebAppUrl = webAppUrl,
                    Projects = [new("Bluebird", "bluebird.app", link, [new("Security", "B+, needs work", "#8A5A00"), new("Performance", "92, good", "#107C10")], ["The background jobs dashboard is open to everyone"])]
                }), false, link)
            ];

            foreach (var (name, html, hasCode, emailLink) in emails)
            {
                var file = Path.Combine(TestContext.TestRunResultsDirectory ?? Path.GetTempPath(), "emails", $"{name}.{culture}.html");
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                await File.WriteAllTextAsync(file, html, TestContext.CancellationToken);
                TestContext.AddResultFile(file);

                Assert.Contains($"{webAppUrl.AbsoluteUri.TrimEnd('/')}/images/icons/bit-logo.png", html, $"{name}: the logo the web app serves.");
                Assert.DoesNotContain("Bit.Fabric", html, $"{name}: the product is called bit Fabric.");
                Assert.Contains(culture == "fa-IR" ? "dir=\"rtl\"" : "dir=\"ltr\"", html, $"{name}: in the reader's direction.");
                Assert.Contains("<title>", html, $"{name}: titled with its subject.");

                if (hasCode)
                    Assert.Contains($">{Code}</td>", html, $"{name}: shows its code.");

                if (emailLink is not null)
                    Assert.Contains($"href=\"{emailLink.AbsoluteUri.Replace("&", "&amp;")}\"", html, $"{name}: links to the page with nothing unescaped.");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }

    private static Task<string> Render<TTemplate>(HtmlRenderer renderer, object model, HttpContext? httpContext = null)
        where TTemplate : IComponent
    {
        var parameters = new Dictionary<string, object?> { ["Model"] = model };

        if (httpContext is not null)
            parameters["HttpContext"] = httpContext;

        return renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<TTemplate>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    public TestContext TestContext { get; set; } = default!;
}
