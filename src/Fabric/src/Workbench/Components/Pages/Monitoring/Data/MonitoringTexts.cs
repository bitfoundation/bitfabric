using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// The sample world's texts in the languages it writes them in. bit Fabric writes its own (an incident's title, its
/// updates) in every language from the app's resources, as the server's ProjectTexts does; a team writes in the ones it
/// chose. English and Persian for now.
/// </summary>
public static class MonitoringTexts
{
    public const string MainCulture = "en-US";

    public static readonly string[] Cultures = [MainCulture, "fa-IR"];

    /// <summary>bit Fabric's own text in every language, written by <paramref name="write"/> with that language's resources.</summary>
    public static Dictionary<string, string> OfBitFabric(Func<IStringLocalizer, string> write)
        => Cultures.ToDictionary(culture => culture, culture => write(new CultureLocalizer(CultureInfoManager.CreateCultureInfo(culture))));

    /// <summary>What the team wrote, in English, and in Persian when they wrote that too.</summary>
    public static Dictionary<string, string> OfTeam(string english, string? persian = null)
        => persian is null ? new() { [MainCulture] = english } : new() { [MainCulture] = english, ["fa-IR"] = persian };

    /// <summary>The text in the culture, else in its language, else in the main language, else any.</summary>
    public static string In(IReadOnlyDictionary<string, string> texts, string culture, string mainCulture = MainCulture)
        => Find(texts, culture) ?? Find(texts, mainCulture) ?? texts.Values.FirstOrDefault() ?? "";

    /// <summary>Whether the text was written in the culture's language.</summary>
    public static bool Has(IReadOnlyDictionary<string, string> texts, string culture) => Find(texts, culture) is not null;

    /// <summary>In the language the view is in.</summary>
    public static string InCurrent(IReadOnlyDictionary<string, string> texts) => In(texts, CultureInfo.CurrentUICulture.Name);

    private static string? Find(IReadOnlyDictionary<string, string> texts, string culture)
    {
        if (texts.TryGetValue(culture, out var exact) && string.IsNullOrWhiteSpace(exact) is false)
            return exact;

        var language = culture.Split('-')[0];

        return texts.FirstOrDefault(pair => pair.Key.Split('-')[0] == language && string.IsNullOrWhiteSpace(pair.Value) is false).Value;
    }

    /// <summary>The app's resources in one culture, whatever the view's is, formatted the way that culture writes numbers.</summary>
    private sealed class CultureLocalizer(CultureInfo culture) : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, AppStrings.ResourceManager.GetString(name, culture) ?? name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(culture, this[name].Value, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
