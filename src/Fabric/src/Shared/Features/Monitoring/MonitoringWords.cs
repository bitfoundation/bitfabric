namespace Bit.Fabric.Shared.Features.Monitoring;

/// <summary>
/// The few ways monitoring puts numbers, lists and regions into words, one for the server's texts in every language (See
/// ProjectTexts) and the pages alike, so a duration reads the same in an alert, an incident and History.
/// </summary>
public static class MonitoringWords
{
    /// <summary>"less than a minute", "12 min", "3 h", "3 h 5 min", "1 day 4 h" or "2 days 4 h", in the current culture.</summary>
    public static string Duration(IStringLocalizer localizer, TimeSpan duration) => duration switch
    {
        { TotalMinutes: < 1 } => localizer[nameof(AppStrings.MonitoringLessThanAMinute)],
        { TotalHours: < 1 } => localizer[nameof(AppStrings.MonitoringDurationMinutes), (int)duration.TotalMinutes],
        { TotalDays: < 1 } => duration.Minutes is 0
            ? localizer[nameof(AppStrings.MonitoringDurationHours), (int)duration.TotalHours]
            : localizer[nameof(AppStrings.MonitoringDurationHoursMinutes), (int)duration.TotalHours, duration.Minutes],
        { TotalDays: < 2 } => localizer[nameof(AppStrings.MonitoringDurationOneDayHours), duration.Hours],
        _ => localizer[nameof(AppStrings.MonitoringDurationDaysHours), (int)duration.TotalDays, duration.Hours]
    };

    /// <summary>
    /// What separates the parts of a line, such as a time, a duration and a name: a middle dot, except in Persian, where a
    /// middle dot beside Persian digits reads as a zero, so its own comma separates them instead.
    /// </summary>
    public static string Dot(IStringLocalizer localizer) => localizer[nameof(AppStrings.MonitoringDot)];

    /// <summary>Names in a sentence with the language's own comma and "and": "Email", "Email and SMS", "Email, SMS and Push".</summary>
    public static string Parts(IStringLocalizer localizer, IReadOnlyList<string> names) => names.Count switch
    {
        0 => "",
        1 => names[0],
        _ => localizer[nameof(AppStrings.MonitoringPartsAnd), string.Join(localizer[nameof(AppStrings.MonitoringPartsSeparator)], names.Take(names.Count - 1)), names[^1]]
    };

    /// <summary>
    /// Where a round ran from, in the reader's language: bit Fabric's own server's "Germany", or a location of the bitplatform
    /// Network by the English name its readings carry (See NetworkLocations). Any other region keeps the name it came with.
    /// </summary>
    public static string Region(IStringLocalizer localizer, string region) => region switch
    {
        "Germany" => localizer[nameof(AppStrings.MonitoringLocationGermany)],
        _ => NetworkLocations.FindByName(region) is { } location ? localizer[location.NameKey] : region
    };
}
