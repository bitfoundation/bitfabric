namespace Boilerplate.WorkbenchTests.Features.Shell;

/// <summary>
/// The Workbench's sections reach each other: every section, a page's own view included, has the same header, whose
/// switcher names the section the user is in and opens every section, on a desktop and on a phone alike.
/// </summary>
[TestClass, TestCategory("UITest")]
public class SectionNavigationUITests : WorkbenchPageTest
{
    /// <summary>Where each of the four sections starts, as the switcher links to it, Home first.</summary>
    private static readonly string[] sectionLinks = [WorkbenchUrls.HomeTop, WorkbenchUrls.Pages, WorkbenchUrls.Boards, WorkbenchUrls.Progress];

    private ILocator Header => Page.Locator("header.shell-header");

    private ILocator Switcher => Header.Locator(".section-menu button").First;

    private ILocator Menu => Page.GetByRole(AriaRole.Menu);

    private async Task Open(string path)
    {
        await Page.GotoAsync(new Uri(Server.Address, path).ToString());
        await WaitUntilInteractive();
    }

    /// <summary>Opens the switcher, which names the current section, and checks it leads to every section with that one checked.</summary>
    private async Task ExpectTheMenu(string current)
    {
        await Expect(Switcher).ToHaveTextAsync(current);

        await Press(Switcher);

        var items = Menu.GetByRole(AriaRole.Menuitemradio);

        await Expect(items).ToHaveCountAsync(4);

        for (var i = 0; i < sectionLinks.Length; i++)
        {
            await Expect(items.Nth(i)).ToHaveAttributeAsync("href", sectionLinks[i]);
        }

        await Expect(Menu.GetByRole(AriaRole.Menuitemradio, new() { Checked = true })).ToContainTextAsync(current);

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Menu).ToBeHiddenAsync();
    }

    [TestMethod, EverySurface]
    public async Task EverySectionsHeader_Should_LeadToEveryOtherSection(WorkbenchSurface surface)
    {
        (string Path, string Name)[] sections =
        [
            (WorkbenchUrls.Home, WorkbenchStrings.Home),
            (WorkbenchUrls.Boards, WorkbenchStrings.Boards),
            (WorkbenchUrls.Progress, WorkbenchStrings.Progress)
        ];

        foreach (var (path, name) in sections)
        {
            await Open(path);

            await Expect(Header.Locator("a.brand")).ToBeVisibleAsync();
            await ExpectTheMenu(name);
        }
    }

    [TestMethod, EverySurface]
    public async Task APagesOwnView_Should_HaveTheSameHeaderAsEverySection(WorkbenchSurface surface)
    {
        await OpenWorkbench("theme");

        await Expect(Header.Locator("a.brand")).ToBeVisibleAsync();
        await ExpectTheMenu(WorkbenchStrings.Pages);
    }

    [TestMethod, EverySurface]
    public async Task ThePagesSection_Should_StartOnTheFirstPage(WorkbenchSurface surface)
    {
        await Page.GotoAsync(new Uri(Server.Address, WorkbenchUrls.Pages.TrimStart('/')).ToString());
        await WaitForViews();

        await Expect(Header.Locator(".crumb-title")).ToHaveTextAsync(WorkbenchStrings.ThemePageTitle);
    }

    [TestMethod, EverySurface]
    public async Task TheHeader_Should_GoFromBoardsToProgressAndOnToThePagesWithoutHome(WorkbenchSurface surface)
    {
        await Open(WorkbenchUrls.Boards);

        await GoTo(WorkbenchStrings.Boards, WorkbenchUrls.Progress);

        await Expect(Page).ToHaveURLAsync(new Regex($"{WorkbenchUrls.Progress}$"));
        await WaitUntilInteractive();

        await GoTo(WorkbenchStrings.Progress, WorkbenchUrls.Pages);

        await Expect(Page).ToHaveURLAsync(new Regex($"/pages/{WorkbenchPageCatalog.Pages[0].Key}"));
        await WaitForViews();
        await Expect(Switcher).ToHaveTextAsync(WorkbenchStrings.Pages);
    }

    /// <summary>Goes to another section, by where it starts, through the switcher, which names the current one.</summary>
    private async Task GoTo(string current, string sectionUrl)
    {
        await Expect(Switcher).ToHaveTextAsync(current);
        await Press(Switcher);
        await Press(Menu.Locator($"[role='menuitemradio'][href='{sectionUrl}']"));
    }

    [TestMethod, EverySurface]
    public async Task TheHeader_Should_FitEverySectionsScreen(WorkbenchSurface surface)
    {
        foreach (var path in new[] { WorkbenchUrls.Home, WorkbenchUrls.Boards, WorkbenchUrls.Progress })
        {
            await Open(path);

            var width = await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth");

            Assert.IsLessThanOrEqualTo(Page.ViewportSize!.Width, width, $"{path} scrolls sideways.");

            await Expect(Header.GetByRole(AriaRole.Button, new() { Name = WorkbenchStrings.ToggleWorkbenchTheme, Exact = true })).ToBeInViewportAsync();
        }
    }

    [TestMethod, EverySurface]
    public async Task NoHeader_Should_HaveAProgressButtonOfItsOwn(WorkbenchSurface surface)
    {
        foreach (var path in new[] { WorkbenchUrls.Home, WorkbenchUrls.Boards, WorkbenchUrls.Page("theme") })
        {
            await Open(path);

            // Progress is one of the switcher's sections, which counts what waits there; no button leads there apart from it.
            await Expect(Header.Locator($"a[href='{WorkbenchUrls.Progress}']:not([role='menuitemradio'])")).ToHaveCountAsync(0);
        }
    }
}
