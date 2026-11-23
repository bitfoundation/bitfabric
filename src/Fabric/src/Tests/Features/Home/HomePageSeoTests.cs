using System.Net;
using Bit.Fabric.Client.Core.Components.Pages.Home;
using Bit.Fabric.Client.Films.Components;
using Bunit;
using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;
using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Tests.Features.Home;

/// <summary>
/// Search engines read the films of the home page and of /features (bitfabric-internal/specs/home-page.md, "Search engines
/// read the films, and the interactive page doesn't build them"): the prerendered page carries each film's text in its
/// card, hidden, and the interactive page builds a film only when its info button is pressed. Since 2026-11-18 the
/// catalogue, with most films, is on /features, and the home page has eight of them. The app runs with prerendering on
/// here, as production has it (Server.Web's appsettings.Production.json): only a prerendered page has anything for a
/// crawler.
/// </summary>
[TestClass, TestCategory("IntegrationTest")]
public partial class HomePageSeoTests
{
    private const string Googlebot = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";
    private const string Chrome = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36";
    private const string TelegramBot = "TelegramBot (like TwitterBot)";

    /// <summary>
    /// A few films from different sections, enough to tell the page carries them all, and the ones the cards split or added on
    /// 2026-11-10: magic links and quick sign-in, single sign-on, reCAPTCHA and the firewall, human in the loop, MCP, dev-mcp,
    /// the Workbench's side by side, store screenshots and flows, and zero downtime deployment, which had a note before.
    /// </summary>
    private static readonly string[] Films =
    [
        "website", "admin-panel", "social", "offline", "rtl", "troubleshoot",
        "magic", "quick-sign-in", "sso", "recaptcha", "firewall", "human-in-the-loop", "mcp", "dev-mcp", "side-by-side", "screenshots", "flows", "zero-downtime"
    ];

    /// <summary>The films of the home page's eight cards (bitfabric-internal/specs/home-page.md, 2026-11-18).</summary>
    private static readonly string[] HomeFilms = ["website", "two-step", "tenants", "admin-panel", "focused", "rtl", "offline", "gdpr"];

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task ACrawler_Should_ReadEveryFilm_OnTheFeaturesPage()
    {
        await using var server = await StartPrerenderingServer();

        var html = await GetFeaturesPage(server, Googlebot);

        Assert.DoesNotContain("<blazor-ssr", html, "A crawler runs no script, so nothing of the page may be left to streaming.");
        AssertHasFilmsText(server, html, Films);
        var hidden = Regex.Matches(html, "<p hidden[ >]").Count;
        Assert.IsGreaterThanOrEqualTo(30, hidden, $"Every feature with an info button carries its film's text, hidden; the page has {hidden} such parts.");

        // The pictures come after the page is interactive, so a crawler reads what they say as a plain list
        Assert.IsTrue(Contains(html, AppStrings.AppHomeGoingLiveServedText), "The going live picture's words are in the page.");
        Assert.IsTrue(Contains(html, AppStrings.AppHomeProtectionFrontText), "The protection picture's words are in the page.");
    }

    /// <summary>
    /// The home page (bitfabric-internal/specs/home-page.md, 2026-11-18): the hero's title is its one h1, as the header gives a
    /// page without a title no heading, its seven bands follow in order as h2s, a crawler reads its eight films and its
    /// questions' answers though they are closed, and it no longer brings the pictures' stylesheet, which only /features and
    /// /contents need.
    /// </summary>
    [TestMethod]
    public async Task TheHomePage_Should_HaveTheHerosTitleAsItsOneH1_ThenItsSevenBands()
    {
        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");

        var html = await GetHomePage(server, Googlebot);

        var h1s = Regex.Matches(html, "<h1[ >].*?</h1>", RegexOptions.Singleline);
        Assert.HasCount(1, h1s, "The page has one h1.");
        foreach (var sentence in new[] { "Build with AI.", "Own everything." })
        {
            Assert.Contains(sentence, h1s[0].Value, $"The h1 is the hero's title: \"{sentence}\".");
        }

        var bands = new[] { "own", "day-one", "compare", "cost", "if-we-stop", "how", "questions" }
            .Select(band => (Band: band, At: Regex.Match(html, $"<h2[^>]*\\bid=\"ah-{band}-title\"").Index))
            .ToArray();
        foreach (var (band, at) in bands)
        {
            Assert.IsGreaterThan(0, at, $"The {band} band's title is an h2.");
        }
        Assert.AreSequenceEqual(bands.Select(b => b.Band), bands.OrderBy(b => b.At).Select(b => b.Band), "The bands come in their order.");

        AssertHasFilmsText(server, html, HomeFilms);
        // The numbers in the answers are AppHomeNumbers', the ones under the hero, not a copy in each language
        var builtOn = string.Format(AppStrings.AppHomeAnswerBuiltOn, AppHomeNumbers.GitHubStars, AppHomeNumbers.Contributors, AppHomeNumbers.Downloads);
        foreach (var answer in new[] { AppStrings.AppHomeAnswerAISeesCode, AppStrings.AppHomeAnswerDeveloper, builtOn, AppStrings.AppHomeAnswerMac })
        {
            Assert.IsTrue(Contains(html, answer), $"The answer \"{answer}\" is in the page, though its question is closed.");
        }
        Assert.IsTrue(Contains(html, string.Format(AppStrings.AppHomeUpgradesAreYours, AppHomeNumbers.ImprovementsAMonth)), "The upgrades line names the improvements a month.");

        Assert.DoesNotContain("bit-blow.css", html, "The home page has no picture, so it brings none of their styles.");
        Assert.Contains($"href=\"/en-US{PageUrls.Features}\"", html, "The home page leads to everything a project starts with.");
    }

    /// <summary>
    /// /contents is the home page as it was until 2026-11-18: the same hero's title as its one h1, then every band
    /// of it in its order, What we add and the comparison first and this site's bill after Fast and watched over. What only a
    /// home page has stays there: the site's JSON-LD and the onboarding's anchor.
    /// </summary>
    [TestMethod]
    public async Task TheContentsPage_Should_KeepEveryBandOfTheOldHomePage_InItsOrder()
    {
        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");

        var html = await GetContentsPage(server, Googlebot);

        var h1s = Regex.Matches(html, "<h1[ >].*?</h1>", RegexOptions.Singleline);
        Assert.HasCount(1, h1s, "The page has one h1.");
        Assert.Contains("Own everything.", h1s[0].Value, "The h1 is the old home page's title.");
        Assert.IsTrue(Contains(html, AppStrings.AppHomeYourAppStartsFullFeaturedNotFromAn), "The old hero's line is in the page.");

        var bands = new[]
            {
                "values", "compare", "platforms", "try", "identity", "workbench", "landing", "batteries", "ai-dlc",
                "fast", "cost", "going-live", "protection", "watch", "agents", "trust", "customers"
            }
            .Select(band => (Band: band, At: Regex.Match(html, $"<h2[^>]*\\bid=\"ah-{band}-title\"").Index))
            .ToArray();
        foreach (var (band, at) in bands)
        {
            Assert.IsGreaterThan(0, at, $"The {band} band's title is an h2.");
        }
        Assert.AreSequenceEqual(bands.Select(b => b.Band), bands.OrderBy(b => b.At).Select(b => b.Band), "The bands come in the old home page's order.");

        foreach (var text in new[] { AppStrings.AppHomeZeroLockIn, AppStrings.AppHomeStandardOpenCodeThatRunsAnywhereKeep, AppStrings.AppHomeAProTeamOfAIAgents, AppStrings.AppHomeComparePromise })
        {
            Assert.IsTrue(Contains(html, text), $"\"{text}\" is in the page.");
        }

        Assert.DoesNotContain("application/ld+json", WebUtility.HtmlDecode(html), "The site's JSON-LD is the home page's alone.");
        Assert.DoesNotContain("data-bonboarding-anchor", html, "The onboarding is the home page's alone.");
    }

    [TestMethod]
    public async Task ACrawler_Should_ReadTheComponentsShowcase_ThoughItIsClosed()
    {
        await using var server = await StartPrerenderingServer();

        var html = await GetFeaturesPage(server, Googlebot);

        // Closed, the accordion hands its panel to the browser as hidden="until-found", so its cards are in the page
        Assert.Contains("hidden=\"until-found\"", html, "The showcase's panel is in the page, closed.");
        Assert.IsTrue(Contains(html, AppStrings.AppHomeShowcaseIntro), "The line on tokens and costs is in the page.");
        foreach (var (title, demo) in new[]
        {
            (AppStrings.AppHomeDataGrid, "datagrid#example2"),
            (AppStrings.AppHomeFullCalendar, "fullcalendar#example1"),
            (AppStrings.AppHomeMap, "map#example2"),
            (AppStrings.AppHomePdfViewer, "pdfviewer#example1"),
            (AppStrings.AppHomeCharts, "chart#pie-pie"),
            (AppStrings.AppHomeFileUpload, "fileupload#example16"),
            (AppStrings.AppHomeResourceTimeline, "resourcetimeline#example10")
        })
        {
            Assert.IsTrue(Contains(html, title), $"The {title} card is in the page.");
            Assert.Contains($"href=\"https://blazorui.bitplatform.dev/components/{demo}\"", html, $"The {title} card's info button opens its demo.");
        }
    }

    /// <summary>
    /// The agents a project works with (bitfabric-internal/specs/home-page.md, 2026-11-19, "Keep both"): Gemini stays and
    /// Antigravity joins it, right after it, in the agents band's card, in its film's caption that a crawler reads, and among
    /// the film's chips.
    /// </summary>
    [TestMethod]
    public async Task TheAgentsBand_Should_NameAntigravity_RightAfterGemini()
    {
        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");

        var html = await GetFeaturesPage(server, Googlebot);

        var band = WebUtility.HtmlDecode(Regex.Match(html, "<section[^>]*id=\"ah-agents\".*?</section>", RegexOptions.Singleline).Value);
        Assert.IsFalse(string.IsNullOrEmpty(band), "The agents band is in the page.");
        Assert.Contains("Copilot, Codex, Claude, Gemini, Antigravity, Cursor", band, "The card names Antigravity right after Gemini.");
        Assert.Contains("Use Copilot, Codex, Claude, Gemini, Antigravity or Cursor", band, "The film's first caption, which a crawler reads, names it too.");

        var chips = ScenesOf(server, "agents")[0].Parts.OfType<FilmChips>().Single().Items;
        Assert.AreEqual(Array.IndexOf(chips, "Gemini") + 1, Array.IndexOf(chips, "Antigravity"), "The film shows Antigravity's chip right after Gemini's.");
    }

    /// <summary>
    /// The two edges and bit Fabric between them (bitfabric-internal/specs/home-page.md, 2026-11-17): the comparison is in the
    /// prerendered page, right after what every project starts with and before this site's bill (2026-11-18), as a table a
    /// crawler and a screen reader read row by row, with the line under it.
    /// </summary>
    [TestMethod]
    public async Task ACrawler_Should_ReadTheComparison_AfterWhatEveryProjectStartsWith()
    {
        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");

        var html = await GetHomePage(server, Googlebot);

        var band = Regex.Match(html, "<section[^>]*id=\"ah-compare\".*?</section>", RegexOptions.Singleline).Value;
        Assert.IsFalse(string.IsNullOrEmpty(band), "The comparison is in the page.");
        var compare = html.IndexOf("id=\"ah-compare\"", StringComparison.Ordinal);
        Assert.IsGreaterThan(html.IndexOf("id=\"ah-day-one\"", StringComparison.Ordinal), compare, "The comparison comes after what every project starts with.");
        Assert.IsLessThan(html.IndexOf("id=\"ah-cost\"", StringComparison.Ordinal), compare, "The comparison comes before this site's bill.");
        Assert.HasCount(7, Regex.Matches(band, "<th scope=\"row\""), "Each of its seven rows is named.");
        Assert.HasCount(3, Regex.Matches(band, "<th scope=\"col\""), "It compares three.");
        foreach (var text in new[]
        {
            AppStrings.AppHomeCompareTitle, AppStrings.AppHomeCompareIntro, AppStrings.AppHomeCompareHostedBuilder, AppStrings.AppHomeCompareEmptyProject,
            AppStrings.AppHomeCompareSecurity, AppStrings.AppHomeCompareHostedSecurity, AppStrings.AppHomeCompareFabricTraining, AppStrings.AppHomeComparePromise, AppStrings.AppHomeCompareNote
        })
        {
            Assert.IsTrue(Contains(band, text), $"\"{text}\" is in the comparison.");
        }
    }

    /// <summary>
    /// What a hosted builder has (bitfabric-internal/specs/home-page.md, Decisions, 2026-11-19): every one of them exports
    /// code, so the comparison no longer says the visitor's code lives with them. It says their prompts, their users and those
    /// users' data do, and that the app runs on the builder's servers. The languages other than English and Persian lost their
    /// translation of the old sentence, so they show the corrected English rather than the old claim.
    /// </summary>
    [TestMethod]
    public async Task TheComparison_Should_SayAHostedBuilderHasTheUsersAndRunsTheApp_NotThatItKeepsTheCode()
    {
        await using var server = await StartPrerenderingServer();

        foreach (var (culture, says, saysNot) in new[]
        {
            ("en-US", "your prompts, your users and their data live with them, and your app runs on their servers", "your prompts, your code and your users"),
            ("fa-IR", "promptها، کاربران و داده‌هایشان پیش آن‌ها می‌مانند، و اپ شما روی سرورهای آن‌ها اجرا می‌شود", "promptها، کد و کاربرانتان"),
            // No translation of the corrected sentence yet: the English one, never the old Dutch one
            ("nl-NL", "your prompts, your users and their data live with them, and your app runs on their servers", "je prompts, je code en je gebruikers")
        })
        {
            var band = WebUtility.HtmlDecode(Regex.Match(await GetPage(server, Googlebot, $"/{culture}/"), "<section[^>]*id=\"ah-compare\".*?</section>", RegexOptions.Singleline).Value);

            Assert.Contains(says, band, $"The {culture} comparison says what a hosted builder has.");
            Assert.DoesNotContain(saysNot, band, $"The {culture} comparison no longer says a hosted builder keeps the code.");
        }
    }

    [TestMethod]
    public async Task ABrowser_Should_GetTheFilmsText_OnceThePageHasLoaded()
    {
        await using var server = await StartPrerenderingServer();

        // Read to its end, the streamed parts included
        AssertHasFilmsText(server, await GetFeaturesPage(server, Chrome), Films);
        AssertHasFilmsText(server, await GetHomePage(server, Chrome), HomeFilms);
    }

    [TestMethod]
    public async Task TheInteractivePage_Should_BuildAFilm_OnlyWhenItsInfoButtonIsPressed()
    {
        await using var server = new AppTestServer();
        await server.Build().Start(TestContext.CancellationToken);

        await using var ctx = server.CreateBunitContext();
        var cut = ctx.Render<HomePage>();

        var website = ScenesOf(server, "website");

        Assert.IsFalse(Contains(cut.Markup, website[0].Caption), "The interactive page carries none of the films' text.");

        // The website's card is the first of the home page's cards (bitfabric-internal/specs/home-page.md, 2026-11-18)
        cut.FindAll(".ah-card-info")[0].Click();

        await cut.WaitForAssertionAsync(() => Assert.IsTrue(Contains(cut.Markup, website[0].Caption), "The film plays once its button is pressed."),
            timeout: TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// A link to the home page, the project wizard or any other page, pasted in a messenger or on a social network, shows the
    /// page's own title and description and bit Fabric's picture (bitfabric-internal/specs/home-page.md, 2026-11-11):
    /// AppPageData renders the preview on every page. Those bots run no script and read only the document's head, so all of
    /// it has to be there in the first response, the picture included.
    /// </summary>
    [TestMethod]
    public async Task ASharedLink_Should_ShowThePagesTitleDescriptionAndPicture()
    {
        await using var server = await StartPrerenderingServer();
        using var client = new HttpClient();

        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");

        foreach (var (path, title, description) in new[]
        {
            ("/en-US/", AppStrings.HomePageTitle, AppStrings.DescriptionMetaTagValue),
            ($"/en-US{PageUrls.Contents}", AppStrings.ContentsPageTitle, AppStrings.ContentsPageDescription),
            ($"/en-US{PageUrls.Features}", AppStrings.FeaturesPageTitle, AppStrings.FeaturesPageDescription),
            ($"/en-US{PageUrls.Pricing}", AppStrings.PricingPageTitle, AppStrings.PricingPageDescription),
            ($"/en-US{PageUrls.Trust}", AppStrings.TrustPageTitle, AppStrings.TrustPageDescription),
            ($"/en-US{PageUrls.About}", AppStrings.AboutBitFabricPageTitle, AppStrings.AboutBitFabricPageDescription),
            ($"/en-US{PageUrls.Compare}", AppStrings.CompareHubPageTitle, AppStrings.CompareHubDescription),
            ($"/en-US{PageUrls.CompareLovable}", AppStrings.CompareLovablePageTitle, AppStrings.CompareLovableDescription),
            ($"/en-US{PageUrls.NewProject}", AppStrings.NewProjectPageTitle, AppStrings.NewProjectPageDescription),
            ($"/en-US{PageUrls.SetupProject}", AppStrings.SetupProjectPageTitle, AppStrings.SetupProjectPageDescription),
            ($"/en-US{PageUrls.Terms}", AppStrings.TermsPageTitle, AppStrings.DescriptionMetaTagValue)
        })
        {
            var html = await GetPage(server, TelegramBot, path);
            var head = html[..html.IndexOf("</head>", StringComparison.Ordinal)];

            Assert.AreEqual(title, Match(head, "<title>([^<]*)</title>"), $"{path}'s title");
            Assert.AreEqual(description, Meta(head, "name", "description"), $"{path}'s description");
            Assert.AreEqual(title, Meta(head, "property", "og:title"), $"{path}'s title where it is shared");
            Assert.AreEqual(description, Meta(head, "property", "og:description"), $"{path}'s description where it is shared");
            Assert.AreEqual("summary_large_image", Meta(head, "name", "twitter:card"), $"{path} is shared with its picture large");

            var picture = Meta(head, "property", "og:image");
            Assert.IsNotNull(picture, $"{path} names its picture.");
            using var response = await client.GetAsync(picture, TestContext.CancellationToken);
            Assert.IsTrue(response.IsSuccessStatusCode, $"{path}'s picture, {picture}, answers {response.StatusCode}.");
            Assert.IsNotNull(response.Content.Headers.ContentType, $"{path}'s picture has a type.");
            Assert.AreEqual("image/png", response.Content.Headers.ContentType.MediaType, $"{path}'s picture is a png.");
        }
    }

    /// <summary>
    /// Why the line is safe (bitfabric-internal/specs/getting-started.md, 2026-11-18) is in the prerendered page of both pages
    /// that give a line, the whole of it, for a search engine and for a visitor before the page is interactive, where it isn't
    /// folded.
    /// </summary>
    [TestMethod]
    public async Task WhyTheLineIsSafe_Should_BeInThePrerenderedPage_OfBothPagesThatGiveALine()
    {
        await using var server = await StartPrerenderingServer();

        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");

        foreach (var path in new[] { $"/en-US{PageUrls.NewProject}?step=4", $"/en-US{PageUrls.SetupProject}?here=1" })
        {
            var text = WebUtility.HtmlDecode(await GetPage(server, Googlebot, path));

            foreach (var line in new[] { AppStrings.NewProjectSafeTitle, AppStrings.NewProjectSafeInstaller, AppStrings.NewProjectSafeSigned, "gh attestation verify <bit.dll> --repo bitfoundation/bitplatform" })
            {
                Assert.Contains(line, text, $"{path} prerenders \"{line}\".");
            }
        }
    }

    private async Task<AppTestServer> StartPrerenderingServer()
    {
        var server = new AppTestServer();
        await server.Build(configureTestConfiguration: configuration => configuration["WebAppRender:PrerenderEnabled"] = "true")
            .Start(TestContext.CancellationToken);
        return server;
    }

    private Task<string> GetHomePage(AppTestServer server, string userAgent) => GetPage(server, userAgent, "/en-US/");

    private Task<string> GetFeaturesPage(AppTestServer server, string userAgent) => GetPage(server, userAgent, $"/en-US{PageUrls.Features}");

    private Task<string> GetContentsPage(AppTestServer server, string userAgent) => GetPage(server, userAgent, $"/en-US{PageUrls.Contents}");

    private async Task<string> GetPage(AppTestServer server, string userAgent, string path)
    {
        using var client = new HttpClient { BaseAddress = server.WebAppAddress };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

        using var response = await client.GetAsync(path, TestContext.CancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
    }

    /// <summary>
    /// Every scene's caption of a few films, in English. The page runs one caption into the next (See HomePage.TextOf), so
    /// each is looked for without the dots it ends or starts with.
    /// </summary>
    private static void AssertHasFilmsText(AppTestServer server, string html, string[] films)
    {
        foreach (var film in films)
        {
            foreach (var scene in ScenesOf(server, film))
            {
                Assert.IsTrue(Contains(html, scene.Caption), $"The {film} film's caption \"{scene.Caption}\" is not in the page.");
            }
        }
    }

    private static AppHomeFilmScene[] ScenesOf(AppTestServer server, string film)
    {
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");

        return new AppHomeFilms(server.WebApp.Services.GetRequiredService<IStringLocalizer<AppStrings>>()).Of(film);
    }

    private static string? Match(string html, string pattern) =>
        Regex.Match(html, pattern) is { Success: true } match ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;

    private static string? Meta(string head, string attribute, string name) =>
        Match(head, $"<meta {attribute}=\"{Regex.Escape(name)}\" content=\"([^\"]*)\"");

    private static bool Contains(string html, string caption) =>
        WebUtility.HtmlDecode(html).Contains(caption.Trim().Trim('.', '…').Trim(), StringComparison.Ordinal);

}
