using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// bitfabric.co/cli: bit CLI, the tool every bit Fabric project is made and kept with, and that bitplatform.dev's bit CLI page
/// leads to. What it does for the visitor comes first, in plain words, then that their AI runs it for them, every command,
/// how to install it and how to check the build. Its words are <see cref="AppCliGuide"/>'s, which /cli.md writes out too.
/// </summary>
public partial class CliPage
{
    [AutoInject] private Clipboard clipboard = default!;

    /// <summary>The line copied last, which its button says was copied.</summary>
    private string? copied;

    private static string GetStartedUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.NewProject);

    private static string ForUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.For);

    private string MarkdownUrl => $"{AppHomeJsonLd.SiteUrlOf(NavigationManager)}{AppCliGuide.MarkdownUrl}";

    private string Lead => Localizer[nameof(AppStrings.CliLead)];

    /// <summary>The title, a sentence a line where the screen is wide enough, as on the home page.</summary>
    private string[] TitleSentences => [.. SentenceEnd().Split(Localizer[nameof(AppStrings.CliTitle)]).Where(sentence => sentence.Length > 0)];

    /// <summary>The space after a sentence's full stop, in the scripts the app's languages use.</summary>
    [GeneratedRegex(@"(?<=[.!?。！？।])\s*")]
    private static partial Regex SentenceEnd();

    /// <summary>
    /// A place on this page, with the page's own path: the site's base address is its root, so a bare #anchor would lead to
    /// the home page, as the band titles' addresses know (See AppHomeBandTitle).
    /// </summary>
    private string InPage(string anchor) => $"{new Uri(NavigationManager.Uri).AbsolutePath}#{anchor}";

    /// <summary>A sentence's text and the commands in it, in order: <paramref name="format"/>'s {0}, {1}... are the commands.</summary>
    private static IEnumerable<(string Part, bool IsCommand)> Parts(string format, string[] commands)
    {
        var at = 0;
        foreach (Match placeholder in Placeholder().Matches(format))
        {
            if (placeholder.Index > at)
                yield return (format[at..placeholder.Index], false);

            yield return (commands[int.Parse(placeholder.Groups[1].Value, CultureInfo.InvariantCulture)], true);
            at = placeholder.Index + placeholder.Length;
        }

        if (at < format.Length)
            yield return (format[at..], false);
    }

    [GeneratedRegex(@"\{(\d)\}")]
    private static partial Regex Placeholder();

    private async Task Copy(string text)
    {
        await clipboard.WriteText(text);
        copied = text;
    }

    /// <summary>
    /// The page as JSON-LD: bit CLI as a SoftwareApplication made by the company, the page about it, and the way back to the
    /// home page. No offer, rating or review: there are none.
    /// </summary>
    private string BuildJsonLd()
    {
        var siteUrl = AppHomeJsonLd.SiteUrlOf(NavigationManager);
        var cliId = $"{siteUrl}/#bit-cli";
        var title = Localizer[nameof(AppStrings.CliPageTitle)].Value;
        var description = Localizer[nameof(AppStrings.CliPageDescription)].Value;

        var cli = new JsonObject
        {
            ["@type"] = "SoftwareApplication",
            ["@id"] = cliId,
            ["name"] = "bit CLI",
            ["description"] = description,
            ["applicationCategory"] = "DeveloperApplication",
            ["operatingSystem"] = "Windows, macOS, Linux",
            ["url"] = CanonicalUrl,
            ["downloadUrl"] = AppCliGuide.PackageUrl,
            ["license"] = AppCliGuide.LicenseUrl,
            ["publisher"] = AppHomeJsonLd.Ref(AppHomeJsonLd.OrganizationId(siteUrl)),
            ["isPartOf"] = AppHomeJsonLd.Ref(AppHomeJsonLd.SoftwareId(siteUrl))
        };

        return AppHomeJsonLd.Document(
            AppHomeJsonLd.WebPage(CanonicalUrl, siteUrl, title, description, cliId),
            cli,
            AppHomeJsonLd.Breadcrumb(CanonicalUrl, siteUrl, Localizer[nameof(AppStrings.Home)], "bit CLI"));
    }
}
