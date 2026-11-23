using System.Net;
using System.Text.Json;
using Bit.Fabric.Client.Core.Components.Pages.Home;
using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Tests.Features.Home;

/// <summary>
/// /cli and /cli.md as a crawler and an AI agent read them: bit CLI, the tool every project is made and kept with, which
/// bitplatform.dev's own bit CLI page leads to. What it does for the visitor comes first, then that their AI runs it, every
/// command, how to install it and how to check the build; the Markdown says what the page says; and the page names only
/// commands bit CLI has, read from its own source in this repository.
/// </summary>
public partial class HomePageSeoTests
{
    /// <summary>
    /// What the page must never say (bitfabric-internal/.agents/product-marketing.md, Claims): the technology underneath, a
    /// command line in the persona's face, a computer to buy, or that building takes months.
    /// </summary>
    private static readonly string[] CliNeverSays = ["C#", "Blazor", "run a command", "laptop", "for months", "over months"];

    [TestMethod]
    public async Task TheCliPage_Should_SayWhatBitCliDoesForYou_ThenEveryCommand_AndHowToInstallIt()
    {
        await using var server = await StartPrerenderingServer();
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");
        var origin = server.WebAppAddress.ToString().TrimEnd('/');
        var localizer = server.WebApp.Services.GetRequiredService<IStringLocalizer<AppStrings>>();

        var html = await GetPage(server, Googlebot, $"/en-US{PageUrls.Cli}");
        var head = Head(html);

        Assert.AreEqual(AppStrings.CliPageTitle, Match(head, "<title>([^<]*)</title>"));
        Assert.AreEqual(AppStrings.CliPageDescription, Meta(head, "name", "description"));
        Assert.Contains($"<link rel=\"alternate\" type=\"text/markdown\" href=\"{origin}{AppCliGuide.MarkdownUrl}\"", head, "The page leads agents to its Markdown copy.");
        AssertOneH1(html, AppStrings.CliTitle);
        // What it does for you first, then that your AI runs it, then the reference a developer looks for
        AssertBandsInOrder(html, "what", "your-ai", "commands", "install", "check", "questions");

        var text = PlainText(html);
        foreach (var fact in CliFacts(localizer))
        {
            Assert.IsTrue(Contains(text, fact), $"The page says \"{fact}\".");
        }
        foreach (var word in CliNeverSays)
        {
            Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase, $"The page says nothing of \"{word}\".");
        }

        // Every command in the list is one of bit CLI's, and every command bit --help lists is in it
        var commands = BitCliCommands();
        var listed = AppCliGuide.Commands(localizer).Select(command => command.Command).ToArray();
        foreach (var command in listed.Concat(AppCliGuide.Stages(localizer).SelectMany(stage => stage.Commands).Select(command => command.Command)).Concat(AppCliGuide.Conversation(localizer).Select(turn => turn.Command)))
        {
            var words = command.Split(' ');
            Assert.AreEqual("bit", words[0], $"\"{command}\" is a command of bit.");
            Assert.Contains(words[1], commands, $"bit CLI has the command \"{words[1]}\" the page names in \"{command}\".");
            if (words.Length > 2 && words[2].StartsWith("http", StringComparison.Ordinal) is false && char.IsLower(words[2][0]))
            {
                Assert.Contains(words[2], commands, $"bit CLI has the command \"{words[2]}\" the page names in \"{command}\".");
            }
        }
        foreach (var command in TopLevelBitCliCommands())
        {
            Assert.Contains($"bit {command}", listed, $"The list has bit {command}, which bit --help lists.");
        }

        // Its source, the package and the guide, and Get started
        foreach (var url in new[] { AppCliGuide.SourceUrl, AppCliGuide.PackageUrl, AppCliGuide.GuideUrl, AppCliGuide.LicenseUrl, $"/en-US{PageUrls.NewProject}" })
        {
            Assert.Contains($"href=\"{url}\"", html, $"The page links {url}.");
        }

        // A SoftwareApplication made by the company, the page about it, and the way back to the home page
        using var document = JsonDocument.Parse(JsonLdOf(html));
        var graph = AssertJsonLdOfAPage(document, origin, PageUrls.Cli, "WebPage", "SoftwareApplication", "BreadcrumbList");
        var cli = graph[1];
        Assert.AreEqual("bit CLI", cli.GetProperty("name").GetString());
        Assert.AreEqual(cli.GetProperty("@id").GetString(), graph[0].GetProperty("about").GetProperty("@id").GetString(), "The page is about bit CLI.");
        Assert.AreEqual(AppCliGuide.PackageUrl, cli.GetProperty("downloadUrl").GetString());
        Assert.AreEqual(AppCliGuide.LicenseUrl, cli.GetProperty("license").GetString());
        Assert.AreEqual($"{origin}/#organization", cli.GetProperty("publisher").GetProperty("@id").GetString(), "The company makes it.");

        // The Persian page is a page of its own, in Persian, with the commands as they are typed
        var persianHtml = await GetPage(server, Googlebot, $"/fa-IR{PageUrls.Cli}");
        Assert.Contains("<html lang=\"fa-IR\"", persianHtml);
        var persian = PlainText(persianHtml);
        Assert.Contains(AppStrings.ResourceManager.GetString(nameof(AppStrings.CliLead), new CultureInfo("fa-IR"))!, persian, "The Persian page has its Persian lead.");
        foreach (var install in AppCliGuide.Installs(localizer))
        {
            Assert.Contains(install.Line, persian, $"The Persian page gives \"{install.Line}\" as it is typed.");
        }
        foreach (var word in CliNeverSays)
        {
            Assert.DoesNotContain(word, persian, StringComparison.OrdinalIgnoreCase, $"The Persian page says nothing of \"{word}\".");
        }

        // The ways to it: the home page's How it works and the trust page's Check it yourself
        foreach (var page in new[] { PageUrls.Home, PageUrls.Trust })
        {
            var from = await GetPage(server, Googlebot, page is "/" ? "/en-US/" : $"/en-US{page}");
            Assert.Contains($"href=\"/en-US{PageUrls.Cli}\"", from, $"{page} leads to the bit CLI page.");
        }
    }

    /// <summary>
    /// /cli.md is the page as Markdown for AI agents, written by the code that renders the page from the same texts, so the two
    /// agree: every command, every line to paste and every link, with this host's addresses.
    /// </summary>
    [TestMethod]
    public async Task CliMd_Should_SayWhatTheCliPageSays()
    {
        await using var server = await StartPrerenderingServer();
        using var client = NoRedirectClient(server);
        var origin = server.WebAppAddress.ToString().TrimEnd('/');

        using var response = await client.GetAsync(AppCliGuide.MarkdownUrl, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(response.Content.Headers.ContentType);
        Assert.AreEqual("text/markdown", response.Content.Headers.ContentType.MediaType);
        var markdown = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);

        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = new("en-US");
        var text = PlainText(await GetPage(server, Googlebot, $"/en-US{PageUrls.Cli}"));
        var localizer = server.WebApp.Services.GetRequiredService<IStringLocalizer<AppStrings>>();

        Assert.StartsWith($"# {AppStrings.CliTitle}\n", markdown, "It opens with the page's h1.");
        foreach (var fact in CliFacts(localizer))
        {
            Assert.Contains(fact, markdown, $"The Markdown says \"{fact}\".");
            Assert.Contains(fact, text, $"The page says \"{fact}\" too.");
        }
        foreach (var command in AppCliGuide.Commands(localizer))
        {
            Assert.Contains($"| `{command.Command}` | {command.Text} |", markdown, $"The list has {command.Command}.");
        }

        Assert.Contains($"{origin}/en-US{PageUrls.Cli}", markdown, "It names the page, on this host.");
        Assert.Contains($"({origin}/en-US{PageUrls.NewProject})", markdown, "It links Get started, on this host.");
        foreach (var url in new[] { AppCliGuide.SourceUrl, AppCliGuide.PackageUrl, AppCliGuide.GuideUrl, AppCliGuide.LicenseUrl })
        {
            Assert.Contains($"]({url})", markdown, $"It links {url}.");
        }
        foreach (var word in CliNeverSays.Append("{{").Append("PLACEHOLDER"))
        {
            Assert.DoesNotContain(word, markdown, StringComparison.OrdinalIgnoreCase, $"The Markdown says nothing of \"{word}\".");
        }
    }

    /// <summary>What /cli says, which /cli.md has to say too: what each command does for you, why your AI runs it, every command, the lines to paste, the checks and the questions.</summary>
    private static IEnumerable<string> CliFacts(IStringLocalizer<AppStrings> localizer) =>
        [
            AppStrings.CliLead,
            .. AppCliGuide.Stages(localizer).SelectMany(stage => new[] { stage.Title, stage.Text }.Concat(stage.Commands.SelectMany(command => new[] { command.Command, command.Title, command.Text }))),
            .. AppCliGuide.Conversation(localizer).SelectMany(turn => new[] { turn.Ask, turn.Command, turn.Reply }),
            .. AppCliGuide.YourAI(localizer).SelectMany(tile => new[] { tile.Title, tile.Text }),
            .. AppCliGuide.Commands(localizer).SelectMany(command => new[] { command.Command, command.Text }),
            .. AppCliGuide.Installs(localizer).SelectMany(install => new[] { install.Title, install.Text, install.Line }),
            .. AppCliGuide.Check(localizer).SelectMany(tile => new[] { tile.Title, tile.Text }),
            AppCliGuide.AttestationLine,
            AppCliGuide.TelemetryOffLine,
            .. AppCliGuide.Questions(localizer).SelectMany(question => new[] { question.Question, question.Answer })
        ];

    /// <summary>
    /// A page's words as a reader gets them: without its scripts and tags, its entities read and its white space one space, so a
    /// line to paste that the page sets word by word reads as the one line it is.
    /// </summary>
    private static string PlainText(string html) =>
        Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(WithoutScripts(html), "<[^>]+>", string.Empty)), @"\s+", " ");

    /// <summary>Every command and subcommand bit CLI declares, read from its source in this repository (src/Cli/Bit.Cli/Commands).</summary>
    private static string[] BitCliCommands() =>
        [.. Directory.GetFiles(Path.Combine(BitCliSource(), "Commands"), "*.cs")
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), "new Command\\(\"(?<name>[a-z-]+)\"").Select(match => match.Groups["name"].Value))
            .Distinct()];

    /// <summary>The commands bit's root command has, as CliApp registers them, without the one Windows' elevated process runs for bit itself.</summary>
    private static string[] TopLevelBitCliCommands() =>
        [.. Regex.Matches(File.ReadAllText(Path.Combine(BitCliSource(), "CliApp.cs")), "(?<name>\\w+)Command\\.Create\\(")
            .Select(match => match.Groups["name"].Value)
            .Where(name => name is not "AdminHelper")
            .Select(name => Regex.Replace(name, "(?<!^)([A-Z])", "-$1").ToLowerInvariant())];

    /// <summary>bit CLI's source folder, found from the tests' own folder up to the repository's root.</summary>
    private static string BitCliSource()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var source = Path.Combine(folder.FullName, "src", "Cli", "Bit.Cli");
            if (Directory.Exists(Path.Combine(source, "Commands")))
                return source;
        }

        throw new DirectoryNotFoundException("bit CLI's source (src/Cli/Bit.Cli) isn't in a folder above the tests.");
    }
}
