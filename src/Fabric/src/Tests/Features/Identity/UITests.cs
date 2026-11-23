using Bit.Fabric.Tests.Features.Layout;

namespace Bit.Fabric.Tests.Features.Identity;

[TestClass, TestCategory("UITest"), Retry(2)]
public partial class UITests : AppPageTest
{
    [TestMethod]
    public async Task UnauthorizedUser_Should_RenderNotAuthorizedComponent()
    {
        await using var server = new AppTestServer(Context);

        await server.Build(configureTestServices: services =>
        {
            // Services registered in this test project will be used instead of the application's services, allowing you to fake certain behaviors during testing.
        }).Start(TestContext.CancellationToken);

        await Page.GotoAsync(new Uri(server.WebAppAddress, PageUrls.Settings).ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Expect(Page)
            .ToHaveTitleAsync(AppStrings.NotAuthorizedPageTitle);
    }

    [TestMethod]
    public async Task SignIn_Should_WorkAsExpected()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        var serverAddress = server.WebAppAddress;

        await Page.GotoAsync(new Uri(server.WebAppAddress, PageUrls.SignIn).ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Expect(Page).ToHaveTitleAsync(AppStrings.SignInPageTitle);

        const string email = TestData.DefaultTestEmail;
        const string password = TestData.DefaultTestPassword;
        const string userFullName = TestData.DefaultTestFullName;

        await SignInPanelUtils.FillCredentials(Page, email, password);
        await Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.Continue, Exact = true }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(server.WebAppAddress.ToString());
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = userFullName })).ToBeVisibleAsync();
        await Expect(Page.Locator(".bit-prs.persona").First).ToContainTextAsync(userFullName);
        await Expect(Page.Locator(".bit-prs.persona").Last).ToContainTextAsync(userFullName);
        // The "Sign in" affordance is a BitActionButton WITH an Href, so it renders as an <a> - role link, not button.
        // Exact = true additionally stops the locator matching the neighbouring "Sign in (Popup)" button by substring.
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = AppStrings.SignIn, Exact = true })).ToBeHiddenAsync();
    }

    [TestMethod]
    public async Task ASignedInUser_Should_SeeTheirAccountAndWhatTheyManage_AndBeAVisitorAgainAfterSigningOut()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await SignInPanelUtils.SignIn(Page, server, TestData.DefaultTestEmail, TestData.DefaultTestPassword, TestData.DefaultTestFullName);

        // The seeded user manages the app and is signed into a tenant.
        await Expect(AppShellPage.NavPanelItem(Page, AppStrings.Settings)).ToBeVisibleAsync();
        await Expect(AppShellPage.NavPanelGroup(Page, AppStrings.Management)).ToBeVisibleAsync();
        await Expect(AppShellPage.CurrentTenant(Page)).ToBeVisibleAsync();
        await Expect(AppShellPage.NavPanelLink(Page, AppStrings.SignIn)).ToHaveCountAsync(0);

        await AppShellPage.SignOut(Page, TestData.DefaultTestFullName);

        await AppShellPage.ExpectAVisitorsShell(Page);
        await Expect(AppShellPage.AccountMenuButton(Page, TestData.DefaultTestFullName)).ToHaveCountAsync(0);
    }

    /// <summary>
    /// The server is held from answering who the user is, so the name in the header can only come from what the device
    /// kept of the account; meanwhile the user's name changed on the server, as an edit on another device would change it.
    /// </summary>
    [TestMethod]
    public async Task AReturningUser_Should_SeeTheirAccountAtOnce_AndTheServersLatestWhenItAnswers()
    {
        var gate = new RequestGate();

        await using var server = new AppTestServer(Context);
        await server.Build(configureTestApiAppServices: services => services.AddRequestGate(gate)).Start(TestContext.CancellationToken);

        var email = await TestUsers.SignUp(server, TestContext.CancellationToken);
        await SignInPanelUtils.SignIn(Page, server, email, TestUsers.Password, displayName: email);

        const string fullName = "Returning Scenario";
        await TestUsers.SetFullName(server, email, fullName);

        gate.Hold("/api/v1/User/GetCurrentUser");
        try
        {
            await Page.ReloadAsync(new() { WaitUntil = WaitUntilState.Load });

            await Expect(AppShellPage.AccountMenuButton(Page, email)).ToBeVisibleAsync();
        }
        finally
        {
            gate.Release();
        }

        await Expect(AppShellPage.AccountMenuButton(Page, fullName)).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task SigningOut_Should_LeaveNothingOfTheAccountOnTheDevice()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await SignInPanelUtils.SignIn(Page, server, TestData.DefaultTestEmail, TestData.DefaultTestPassword, TestData.DefaultTestFullName);

        await AppShellPage.SignOut(Page, TestData.DefaultTestFullName);
        await AppShellPage.ExpectAVisitorsShell(Page);

        var keptOnTheDevice = await Page.EvaluateAsync<string>("() => JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }) + document.cookie");

        Assert.IsFalse(keptOnTheDevice.Contains(TestData.DefaultTestEmail, StringComparison.OrdinalIgnoreCase), keptOnTheDevice);
        Assert.IsFalse(keptOnTheDevice.Contains(TestData.DefaultTestFullName, StringComparison.OrdinalIgnoreCase), keptOnTheDevice);
    }

    [TestMethod]
    public async Task ASignedInUser_Should_OpenAProtectedPage_ByItsLink()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        await SignInPanelUtils.SignIn(Page, server, TestData.DefaultTestEmail, TestData.DefaultTestPassword, TestData.DefaultTestFullName);

        await Page.GotoAsync(new Uri(server.WebAppAddress, PageUrls.Settings).ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Expect(Page).ToHaveTitleAsync(AppStrings.SettingsPageTitle);
        await Expect(AppShellPage.PageHeading(Page)).ToHaveTextAsync(AppStrings.Settings);
        await Expect(AppShellPage.AccountMenuButton(Page, TestData.DefaultTestFullName)).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task AVisitor_Should_SignUp_AndBeSignedIn_ByTheLinkInTheConfirmationEmail()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        var email = TestUsers.NewEmail();

        await Page.GotoAsync(new Uri(server.WebAppAddress, PageUrls.SignUp).ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(Page).ToHaveTitleAsync(AppStrings.SignUpPageTitle);
        await Page.WaitForBlazorInteractive();

        await Page.GetByPlaceholder(AppStrings.EmailPlaceholder).FillEnsuringStable(email);
        await Page.GetByPlaceholder(AppStrings.PasswordPlaceholder).FillEnsuringStable(TestUsers.Password);
        await Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.SignUp, Exact = true }).ClickAsync();

        await Expect(Page).ToHaveTitleAsync(AppStrings.ConfirmPageTitle);

        var confirmationEmail = await server.WaitForCapturedEmail(email, captured => captured.Kind is CapturedEmailKind.EmailToken, TestContext.CancellationToken);

        await Page.GotoAsync(confirmationEmail.Link!.ToString(), new() { WaitUntil = WaitUntilState.NetworkIdle });

        // A new user has no full name yet, so the app calls them by their e-mail.
        await Expect(AppShellPage.AccountMenuButton(Page, email)).ToBeVisibleAsync();
        await Expect(Page).ToHaveTitleAsync(AppStrings.HomePageTitle);
        await Expect(AppShellPage.NavPanelItem(Page, AppStrings.Settings)).ToBeVisibleAsync();
    }
}
