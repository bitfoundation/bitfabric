using System.Collections.Concurrent;

namespace Bit.Fabric.Shared.Features.Monitoring;

/// <summary>
/// An SLA month (bitfabric-internal/specs/monitoring.md, Times follow the viewer's time zone): from midnight of its first
/// day to midnight of the next month's first day in the project's time zone, on the Gregorian calendar whatever calendar
/// the viewer reads dates in, so the tab, the CSV and the PDF all name the same month.
/// </summary>
public static class ProjectSlaMonths
{
    private static readonly ConcurrentDictionary<string, CultureInfo> gregorianCultures = new();

    /// <summary>The instant a month starts in <paramref name="zone"/>; a midnight a clock change skips starts at the hour after it.</summary>
    public static DateTimeOffset StartOf(int year, int month, TimeZoneInfo zone)
    {
        var midnight = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);

        while (zone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddHours(1);
        }

        return new(TimeZoneInfo.ConvertTimeToUtc(midnight, zone), TimeSpan.Zero);
    }

    /// <summary>The month <paramref name="at"/> falls in, in <paramref name="zone"/>.</summary>
    public static (int Year, int Month) MonthOf(DateTimeOffset at, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(at, zone);
        return (local.Year, local.Month);
    }

    /// <summary>The month <paramref name="count"/> months after (or before, when negative) the given one.</summary>
    public static (int Year, int Month) Add(int year, int month, int count)
    {
        var date = new DateTime(year, month, 1).AddMonths(count);
        return (date.Year, date.Month);
    }

    /// <summary>
    /// <paramref name="culture"/> on the Gregorian calendar, with its month names in its own language: Persian reads
    /// dates on the Persian calendar and Arabic on Umm al-Qura, whose months don't line up with an SLA month.
    /// </summary>
    public static CultureInfo Gregorian(CultureInfo culture)
    {
        if (culture.Calendar is GregorianCalendar)
            return culture;

        return gregorianCultures.GetOrAdd(culture.Name, _ =>
        {
            var gregorian = (CultureInfo)culture.Clone();
            // Setting the calendar drops the names cached for the culture's own one.
            gregorian.DateTimeFormat.Calendar = gregorian.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault() ?? new GregorianCalendar();
            return gregorian;
        });
    }

    /// <summary>"November 2026", in <paramref name="culture"/>'s language.</summary>
    public static string NameOf(int year, int month, CultureInfo culture)
        => new DateTime(year, month, 1).ToString("MMMM yyyy", Gregorian(culture));

    /// <summary>"Nov", in <paramref name="culture"/>'s language.</summary>
    public static string ShortNameOf(int year, int month, CultureInfo culture)
        => new DateTime(year, month, 1).ToString("MMM", Gregorian(culture));
}
