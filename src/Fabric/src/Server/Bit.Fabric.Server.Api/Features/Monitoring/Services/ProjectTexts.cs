using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// bit Fabric's own texts about a project, such as an incident's title, written once in every language it speaks
/// (bitfabric-internal/specs/monitoring.md, the status page's languages), so each reader, alert and subscriber gets
/// theirs without a translation at read time.
/// </summary>
public partial class ProjectTexts
{
    [AutoInject] private IStringLocalizer<AppStrings> localizer = default!;

    /// <summary>The text in every supported culture, written by <paramref name="write"/> while that culture is current.</summary>
    public List<ProjectText> InEveryLanguage(Func<IStringLocalizer<AppStrings>, string> write)
    {
        var current = CultureInfo.CurrentUICulture;
        var currentFormat = CultureInfo.CurrentCulture;

        try
        {
            return [.. CultureInfoManager.SupportedCultures.Select(supported =>
            {
                CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = supported.Culture;
                return new ProjectText { Culture = supported.Culture.Name, Text = write(localizer) };
            })];
        }
        finally
        {
            CultureInfo.CurrentUICulture = current;
            CultureInfo.CurrentCulture = currentFormat;
        }
    }

    /// <summary>The text in <paramref name="culture"/>, or its language's, or in English.</summary>
    public static string? In(IEnumerable<ProjectText> texts, string? culture)
    {
        var all = texts as IReadOnlyCollection<ProjectText> ?? [.. texts];
        var language = culture?.Split('-')[0];

        return (all.FirstOrDefault(text => text.Culture == culture)
                ?? all.FirstOrDefault(text => language is not null && text.Culture?.StartsWith($"{language}-", StringComparison.Ordinal) is true)
                ?? all.FirstOrDefault(text => text.Culture == CultureInfoManager.DefaultCulture.Name)
                ?? all.FirstOrDefault())?.Text;
    }

    /// <inheritdoc cref="MonitoringWords.Duration"/>
    public static string Duration(IStringLocalizer<AppStrings> localizer, TimeSpan duration) => MonitoringWords.Duration(localizer, duration);

    /// <inheritdoc cref="MonitoringWords.Parts"/>
    public static string Parts(IStringLocalizer<AppStrings> localizer, IReadOnlyList<string> titles) => MonitoringWords.Parts(localizer, titles);
}
