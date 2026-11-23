using System.Globalization;
using System.Resources;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace Bit.Fabric.Workbench.Infrastructure.Services;

/// <summary>
/// Saves texts changed in Edit text mode into the resx files of each culture they come from (WorkbenchStrings or AppStrings),
/// and keeps them in memory, so pages show them before a restart picks up the resx.
/// </summary>
public sealed partial class WorkbenchTextEdits
{
    // Each resource with its resx folder, from this project's folder
    private static readonly Dictionary<string, (Type Type, string Folder)> resources = new()
    {
        [nameof(WorkbenchStrings)] = (typeof(WorkbenchStrings), "Resources"),
        [nameof(AppStrings)] = (typeof(AppStrings), Path.Combine("..", "Shared", "Resources"))
    };

    [AutoInject] private IConfiguration configuration = default!;
    [AutoInject] private IHttpContextAccessor httpContextAccessor = default!;

    private readonly ConcurrentDictionary<(string Resource, string Culture, string Key), string> edited = [];
    private readonly ConcurrentDictionary<string, SemaphoreSlim> fileLocks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the project folder, and with it the resx files, is on this machine.</summary>
    public bool CanWriteFiles => WorkbenchFolders.Project is not null;

    /// <summary>A text saved since startup in the current UI culture, or null.</summary>
    public string? Find(string resource, string key)
    {
        return edited.TryGetValue((resource, CultureInfo.CurrentUICulture.Name, key), out var value) ? value : null;
    }

    /// <summary>An entry's text in every supported culture, with the texts saved since startup.</summary>
    public WorkbenchTextTranslation[] Translations(string resource, string key)
    {
        if (resources.TryGetValue(resource, out var entry) is false)
            return [];

        var manager = new ResourceManager(entry.Type);

        // A resource with fewer languages than the app lists only those it has.
        return [.. CultureInfoManager.SupportedCultures.Where(supported => HasLanguage(resource, supported.Culture.Name)).Select(supported =>
        {
            var culture = supported.Culture;
            var value = edited.TryGetValue((resource, culture.Name, key), out var edit) ? edit : manager.GetString(key, culture);
            return new WorkbenchTextTranslation(culture.Name, supported.DisplayName, value);
        })];
    }

    /// <summary>
    /// Whether a text may replace an entry's: it must be a valid format string with the same placeholders, like {0}, as the
    /// entry's text in the default culture. An entry whose text isn't a format string takes any text.
    /// </summary>
    public bool KeepsPlaceholders(string resource, string key, string value)
    {
        var placeholders = PlaceholdersOf(value);
        var original = edited.TryGetValue((resource, CultureInfoManager.DefaultCulture.Name, key), out var edit)
            ? edit
            : resources.TryGetValue(resource, out var entry) ? new ResourceManager(entry.Type).GetString(key, CultureInfoManager.DefaultCulture) : null;

        if (original is null)
            return placeholders is not null;

        return PlaceholdersOf(original) is not { } expected || (placeholders is not null && placeholders.SetEquals(expected));
    }

    /// <summary>
    /// Writes an entry's text into the culture's resx, adding the entry when the file lacks it. Returns false when the request
    /// may not change the texts, the text changes the entry's placeholders, or the file isn't there.
    /// </summary>
    public async Task<bool> Save(string resource, string key, string culture, string value)
    {
        if (CanChange is false || KeepsPlaceholders(resource, key, value) is false)
            return false;

        if (WorkbenchFolders.Project is not { } folder || ResxFile(folder, resource, culture) is not { } path || File.Exists(path) is false)
            return false;

        var fileLock = fileLocks.GetOrAdd(Path.GetFullPath(path), _ => new(1, 1));

        await fileLock.WaitAsync();

        try
        {
            // The resx is edited as text, so the rest of the file, its BOM included, stays as it was.
            var raw = await File.ReadAllBytesAsync(path);
            var hasBom = raw.AsSpan().StartsWith(Encoding.UTF8.Preamble);
            var text = Encoding.UTF8.GetString(hasBom ? raw[3..] : raw);
            var name = SecurityElement.Escape(key) ?? key;
            var escaped = value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            var entry = new Regex($@"(<data name=""{Regex.Escape(name)}""[^>]*>\s*<value>)(.*?)(</value>)", RegexOptions.Singleline);

            text = entry.IsMatch(text)
                ? entry.Replace(text, match => match.Groups[1].Value + escaped + match.Groups[3].Value, 1)
                : text.Insert(text.LastIndexOf("</root>", StringComparison.Ordinal), $"    <data name=\"{name}\" xml:space=\"preserve\">\n        <value>{escaped}</value>\n    </data>\n");

            await File.WriteAllBytesAsync(path, [.. (hasBom ? Encoding.UTF8.Preamble.ToArray() : []), .. Encoding.UTF8.GetBytes(text)]);
        }
        finally
        {
            fileLock.Release();
        }

        // The languages that share the file, like the default's other regions, show the text as well.
        foreach (var (_, sharing) in CultureInfoManager.SupportedCultures.Where(supported => ResxFile(folder, resource, supported.Culture.Name) == path))
        {
            edited[(resource, sharing.Name, key)] = value;
        }

        return true;
    }

    /// <summary>Whether the current request, or the circuit it started, may change the texts.</summary>
    private bool CanChange => httpContextAccessor.HttpContext is { } context && WorkbenchLocalRequests.CanChange(context, configuration);

    /// <summary>Whether a resource has a resx file of the culture's language; true when the project folder isn't on this machine to tell.</summary>
    private static bool HasLanguage(string resource, string culture)
    {
        return WorkbenchFolders.Project is not { } folder || ResxFile(folder, resource, culture) is { } path && File.Exists(path);
    }

    /// <summary>The resx of a resource in a culture: the neutral file for the default culture's language, otherwise the language's file.</summary>
    private static string? ResxFile(string projectFolder, string resource, string culture)
    {
        if (resources.TryGetValue(resource, out var entry) is false || CultureInfoManager.GetCultureInfo(culture) is not { } info)
            return null;

        var folder = Path.Combine(projectFolder, entry.Folder);

        return info.TwoLetterISOLanguageName == CultureInfoManager.DefaultCulture.TwoLetterISOLanguageName
            ? Path.Combine(folder, $"{resource}.resx")
            : Path.Combine(folder, $"{resource}.{info.TwoLetterISOLanguageName}.resx");
    }

    /// <summary>The indexes of a format string's placeholders, or null when the text isn't a valid format string.</summary>
    private static HashSet<int>? PlaceholdersOf(string text)
    {
        CompositeFormat format;

        try
        {
            format = CompositeFormat.Parse(text);
        }
        catch (FormatException)
        {
            return null;
        }

        HashSet<int> used = [];
        object?[] arguments = [.. Enumerable.Range(0, format.MinimumArgumentCount).Select(index => new Placeholder(index, used))];

        _ = string.Format(CultureInfo.InvariantCulture, format, arguments);

        return used;
    }

    /// <summary>A format argument that notes the index of the placeholder that formats it.</summary>
    private sealed class Placeholder(int index, HashSet<int> used) : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            used.Add(index);
            return string.Empty;
        }

        public override string ToString() => ToString(null, null);
    }
}
