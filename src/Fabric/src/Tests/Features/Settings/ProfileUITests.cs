using Bit.Fabric.Tests.Features.Layout;

namespace Bit.Fabric.Tests.Features.Settings;

[TestClass, TestCategory("UITest"), Retry(2)]
public partial class ProfileUITests : AppPageTest
{
    /// <summary>
    /// The profile page changes the signed in user's details in place and tells the rest of the app about it, rather
    /// than having the app load the user again (See ProfileSection.PublishUserDataUpdated), so the header has to follow
    /// a change it was never sent as a new user.
    /// </summary>
    [TestMethod]
    public async Task SettingAFullName_Should_ShowItAcrossTheApp_AtOnceAndAfterAReload()
    {
        await using var server = new AppTestServer(Context);
        await server.Build().Start(TestContext.CancellationToken);

        var email = await TestUsers.SignUp(server, TestContext.CancellationToken);
        const string fullName = "Profile Scenario";

        // A new user has no full name yet, so the app calls them by their e-mail.
        await SignInPanelUtils.SignIn(Page, server, email, TestUsers.Password, displayName: email);

        await Page.GoToInApp($"{PageUrls.Settings}/{PageUrls.SettingsSections.Profile}");

        var fullNameField = Page.GetByRole(AriaRole.Textbox, new() { Name = AppStrings.FullName, Exact = true });
        await Expect(fullNameField).ToHaveValueAsync(string.Empty);

        await fullNameField.FillEnsuringStable(fullName);
        await Page.GetByRole(AriaRole.Button, new() { Name = AppStrings.Save, Exact = true }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Status, new() { Name = AppStrings.ProfileUpdatedSuccessfullyMessage })).ToBeVisibleAsync();
        await Expect(AppShellPage.AccountMenuButton(Page, fullName)).ToBeVisibleAsync();
        await Expect(AppShellPage.AccountMenuButton(Page, email)).ToHaveCountAsync(0);

        await Page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Expect(AppShellPage.AccountMenuButton(Page, fullName)).ToBeVisibleAsync();
        await Expect(fullNameField).ToHaveValueAsync(fullName);
    }
}
