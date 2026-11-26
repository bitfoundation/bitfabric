namespace Boilerplate.WorkbenchTests.Features.Shell;

/// <summary>
/// The Workbench's sections reach each other: the header of Home, Flows, Boards and Progress leads to every section and marks
/// the one the user is in, as a bar on a desktop and as one menu on a phone, and a page's own view reaches them from the menu
/// in its breadcrumb.
/// </summary>
[TestClass, TestCategory("UITest")]
public class SectionNavigationUITests : WorkbenchPageTest
{
    /// <summary>Where each section starts, as the header links to it.</summary>
    private static readonly string[] sectionLinks = [WorkbenchUrls.HomePages, WorkbenchUrls.Flows, WorkbenchUrls.Boards, WorkbenchUrls.Progress];

    private ILocator Header => Page.Locator("header.shell-header");

    private ILocator SectionBar => Header.GetByRole(AriaRole.Navigation, new() { Name = WorkbenchStrings.WorkbenchSections, Exact = true });

    private ILocator Menu => Page.GetByRole(AriaRole.Menu);

    private async Task Open(string path)
    {
        await Page.GotoAsync(new Uri(Server.Address, path).ToString());
        await WaitUntilInteractive();
    }

    /// <summary>Opens the menu of sections a button names the current one on, and checks it leads to every section with that one checked.</summary>
    private async Task ExpectTheMenu(ILocator button, string current)
    {
        await Expect(button).ToHaveTextAsync(current);

        await Press(button);

        var items = Menu.GetByRole(AriaRole.Menuitemradio);

        await Expect(items).ToHaveCountAsync(5);
        await Expect(items.Nth(0)).ToHaveAttributeAsync("href", WorkbenchUrls.HomeTop);

        for (var i = 0; i < sectionLinks.Length; i++)
        {
            await Expect(items.Nth(i + 1)).ToHaveAttributeAsync("href", sectionLinks[i]);
        }

        await Expect(Menu.GetByRole(AriaRole.Menuitemradio, new() { Checked = true })).ToHaveTextAsync(current);

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Menu).ToBeHiddenAsync();
    }

    [TestMethod, EverySurface]
    public async Task EverySectionsHeader_Should_LeadToEveryOtherSection(WorkbenchSurface surface)
    {
        (string Path, string Name)[] sections =
        [
            (WorkbenchUrls.Home, WorkbenchStrings.Home),
            (WorkbenchUrls.Flows, WorkbenchStrings.Flows),
            (WorkbenchUrls.Boards, WorkbenchStrings.Boards),
            (WorkbenchUrls.Progress, WorkbenchStrings.Progress)
        ];

        foreach (var (path, name) in sections)
        {
            await Open(path);

            await Expect(Header.Locator("a.brand")).ToBeVisibleAsync();

            if (IsPhone)
            {
                await Expect(SectionBar).ToBeHiddenAsync();
                await ExpectTheMenu(Header.Locator(".section-menu button").First, name);
            }
            else
            {
                var links = SectionBar.GetByRole(AriaRole.Link);

                await Expect(links).ToHaveCountAsync(sectionLinks.Length);

                for (var i = 0; i < sectionLinks.Length; i++)
                {
                    await Expect(links.Nth(i)).ToHaveAttributeAsync("href", sectionLinks[i]);
                }

                // Home is the logo's, so its own header marks no section.
                if (path == WorkbenchUrls.Home)
                {
                    await Expect(SectionBar.Locator("[aria-current='page']")).ToHaveCountAsync(0);
                }
                else
                {
                    await Expect(SectionBar.Locator("[aria-current='page']")).ToHaveTextAsync(new Regex($"{Regex.Escape(name)}$"));
                }
            }
        }
    }

    [TestMethod, EverySurface]
    public async Task APagesOwnView_Should_LeadToEverySectionFromItsBreadcrumb(WorkbenchSurface surface)
    {
        await OpenWorkbench("theme");

        if (IsPhone)
        {
            await OpenTools();
            await ExpectTheMenu(Tools.Locator(".section-menu button").First, WorkbenchStrings.Pages);
        }
        else
        {
            await ExpectTheMenu(Page.Locator(".topbar .section-menu button").First, WorkbenchStrings.Pages);
        }
    }

    [TestMethod, EverySurface]
    public async Task TheHeader_Should_GoFromBoardsToProgressAndOnToFlowsWithoutHome(WorkbenchSurface surface)
    {
        await Open(WorkbenchUrls.Boards);

        await GoTo(WorkbenchStrings.Boards, WorkbenchStrings.Progress);

        await Expect(Page).ToHaveURLAsync(new Regex($"{WorkbenchUrls.Progress}$"));
        await WaitUntilInteractive();

        await GoTo(WorkbenchStrings.Progress, WorkbenchStrings.Flows);

        await Expect(Page).ToHaveURLAsync(new Regex($"{WorkbenchUrls.Flows}$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = WorkbenchStrings.Flows, Level = 1 })).ToBeVisibleAsync();
    }

    /// <summary>Goes to another section the way this surface's header offers it: a link of the bar, or an item of the menu.</summary>
    private async Task GoTo(string current, string section)
    {
        if (IsPhone)
        {
            var button = Header.Locator(".section-menu button").First;

            await Expect(button).ToHaveTextAsync(current);
            await Press(button);
            await Press(Menu.GetByRole(AriaRole.Menuitemradio, new() { Name = section }));
        }
        else
        {
            await Press(SectionBar.GetByRole(AriaRole.Link, new() { Name = section }));
        }
    }

    [TestMethod, EverySurface]
    public async Task TheHeader_Should_FitEverySectionsScreen(WorkbenchSurface surface)
    {
        foreach (var path in new[] { WorkbenchUrls.Home, WorkbenchUrls.Flows, WorkbenchUrls.Boards, WorkbenchUrls.Progress })
        {
            await Open(path);

            var width = await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth");

            Assert.IsLessThanOrEqualTo(Page.ViewportSize!.Width, width, $"{path} scrolls sideways.");

            await Expect(Header.GetByRole(AriaRole.Button, new() { Name = WorkbenchStrings.ToggleWorkbenchTheme, Exact = true })).ToBeInViewportAsync();
        }
    }
}
