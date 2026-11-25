namespace Boilerplate.WorkbenchTests.Features.Explainers;

/// <summary>
/// The explainers an agent builds instead of a long answer in the chat, through the two that ship as examples: each reads
/// as a document, what the reader changes on it works, it keeps reading left to right in a right-to-left view, and the
/// home page leads to them.
/// </summary>
[TestClass, TestCategory("UITest")]
public class ExplainersUITests : WorkbenchPageTest
{
    private ILocator Heading(string name, int level) => Page.GetByRole(AriaRole.Heading, new() { Name = name, Level = level, Exact = true });

    private ILocator Settings => Page.Locator("#settings li.setting");

    private ILocator Summary => Page.Locator(".calculator-summary");

    [TestMethod, EverySurface]
    public async Task TheCiCdExplainer_Should_ReadAsADocumentOfThisProject(WorkbenchSurface surface)
    {
        await OpenPreview("ci-cd");

        await Expect(Heading("How Boilerplate is built and deployed", 1)).ToBeVisibleAsync();
        await Expect(Page.Locator("main.explainer-page article.explainer[lang='en']")).ToBeVisibleAsync();
        await Expect(Page.GetByText("An example of an explainer")).ToBeVisibleAsync();

        foreach (var section in new[] { "At a glance", "From a commit to a running site", "The workflow files", "Test and Production", "What a server deployment does", "Where each job runs", "Secrets and variables", "Sources" })
        {
            await Expect(Heading(section, 2)).ToBeAttachedAsync();
        }

        await Expect(Page.Locator(".pipeline .node-title", new() { HasText = "cd-test.yml" })).ToBeVisibleAsync();
    }

    [TestMethod, EverySurface]
    public async Task TheCiCdExplainersSettings_Should_NarrowToAKindAndASearch(WorkbenchSurface surface)
    {
        await OpenPreview("ci-cd");

        var all = await Settings.CountAsync();

        await Press(Page.Locator("#settings label", new() { HasText = "Secrets" }));

        await Expect(Settings.Filter(new() { HasText = "SERVER_ADDRESS" })).ToHaveCountAsync(0);
        await Expect(Settings.Filter(new() { HasNotText = "Secret" })).ToHaveCountAsync(0);

        await Press(Page.Locator("#settings label", new() { HasText = "All" }));
        await Page.GetByRole(AriaRole.Searchbox, new() { Name = "Search the settings" }).FillAsync("APP_TITLE");

        await Expect(Settings).ToHaveCountAsync(1);
        await Expect(Settings.Locator(".setting-name")).ToHaveTextAsync("APP_TITLE, APP_ID");
        await Expect(Page.Locator(".settings-count")).ToHaveTextAsync($"1 of {all} settings");
    }

    [TestMethod, EverySurface]
    public async Task ThePaymentsExplainer_Should_WorkOutWhatEachOneKeepsFromASale(WorkbenchSurface surface)
    {
        await OpenPreview("payments");

        await Expect(Heading("Taking payments in your app", 1)).ToBeVisibleAsync();
        await Expect(Summary).ToHaveTextAsync("On a €10.00 sale, Stripe keeps the least, €0.40, and Google Play and the App Store the most, €1.50.");

        // Stripe Billing's 0.7% puts Mollie ahead on a subscription.
        await Press(Page.Locator("#one-sale label", new() { HasText = "Subscription" }));

        await Expect(Summary).ToHaveTextAsync("On a €10.00 subscription payment, Mollie keeps the least, €0.43, and Google Play and the App Store the most, €1.50.");

        // At the lowest price, the fixed fees outweigh the stores' 15%.
        await Press(Page.Locator("#one-sale label", new() { HasText = "One-off" }));
        await Page.Locator("#one-sale [role='slider'], #one-sale input[type='range']").First.FocusAsync();
        await Page.Keyboard.PressAsync("Home");

        await Expect(Summary).ToHaveTextAsync("On a €1.00 sale, Google Play and the App Store keep the least, €0.15, and Paddle the most, €0.55.");
    }

    [TestMethod, EverySurface]
    public async Task ThePaymentsExplainersStores_Should_ShowTheRulesOfThePickedRegion(WorkbenchSurface surface)
    {
        await OpenPreview("payments");

        await Expect(Page.Locator("#stores").GetByText("26%", new() { Exact = false }).First).ToBeVisibleAsync();

        await Press(Page.Locator("#stores").GetByRole(AriaRole.Tab, new() { Name = "The US" }));

        await Expect(Page.Locator("#stores").GetByText("the Supreme Court hears the case", new() { Exact = false })).ToBeVisibleAsync();
    }

    [TestMethod, OnDesktops]
    public async Task AnExplainer_Should_KeepReadingInItsOwnLanguageInARightToLeftView(WorkbenchSurface surface)
    {
        await OpenPreview("payments", "?culture=fa-IR");

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("dir", "rtl");
        await Expect(Page.Locator("article.explainer")).ToHaveAttributeAsync("dir", "ltr");
        await Expect(Page.Locator("article.explainer")).ToHaveCSSAsync("direction", "ltr");
    }

    [TestMethod, EverySurface]
    public async Task TheHomePage_Should_LeadToTheExplainers(WorkbenchSurface surface)
    {
        await Page.GotoAsync(Server.Address.ToString());

        var section = Page.Locator("#explainers");

        await Expect(section.GetByRole(AriaRole.Heading, new() { Name = WorkbenchStrings.HomeExplainersTitle, Level = 2 })).ToBeVisibleAsync();
        await Expect(section.GetByText(WorkbenchStrings.HomeExplainersPrompt)).ToBeVisibleAsync();
        await Expect(section.Locator("a[href='/workbench/ci-cd?device=laptop-hidpi']")).ToContainTextAsync(WorkbenchStrings.CiCdExplainerTitle);
        await Expect(section.Locator("a[href='/workbench/payments?device=laptop-hidpi']")).ToContainTextAsync(WorkbenchStrings.PaymentsExplainerTitle);
    }
}
