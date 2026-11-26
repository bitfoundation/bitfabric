using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Workbench.Components.Pages.Growth.Data;

/// <summary>Which way a figure moved against the period before.</summary>
public enum GrowthTone { Up, Down, Flat, New }

/// <summary>A figure's change as the page writes it: "18%", "4 pts" or "New", and which way it went.</summary>
public sealed record GrowthDelta(string Text, GrowthTone Tone);

/// <summary>How the page writes its numbers, dates and changes, in the view's culture.</summary>
public static class GrowthFormat
{
    public static string Count(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    public static string Share(double value) => (value * 100).ToString("0", CultureInfo.CurrentCulture) + "%";

    public static string Decimal(double value) => value.ToString("0.0", CultureInfo.CurrentCulture);

    /// <summary>An average: one decimal under ten, whole above.</summary>
    public static string Average(double value) => value.ToString(value < 10 ? "0.#" : "0", CultureInfo.CurrentCulture);

    /// <summary>A number of days, with a half when there is one.</summary>
    public static string Days(double value) => value.ToString("0.#", CultureInfo.CurrentCulture);

    /// <summary>"+212", or nothing when nothing was added.</summary>
    public static string? Added(GrowthChange change) => change.Now > 0 ? "+" + Count(change.Now) : null;

    /// <summary>The change of a count against the period before, in percent; New when there was none before.</summary>
    public static GrowthDelta? Change(GrowthChange change, IStringLocalizer localizer)
    {
        if (change.Before is 0)
            return change.Now is 0 ? null : new(localizer[nameof(WorkbenchStrings.GrowthNew)], GrowthTone.New);

        var ratio = (change.Now - change.Before) / (double)change.Before;

        // Three times as many or more reads better as a multiple than as a percentage in the thousands.
        if (change.Now >= change.Before * 3)
        {
            var times = change.Now / (double)change.Before;

            return new(localizer[nameof(WorkbenchStrings.GrowthTimes), times.ToString(times < 10 ? "0.#" : "0", CultureInfo.CurrentCulture)], GrowthTone.Up);
        }

        var percent = Math.Round(Math.Abs(ratio) * 100);

        return new(percent.ToString("0", CultureInfo.CurrentCulture) + "%", percent is 0 ? GrowthTone.Flat : ratio > 0 ? GrowthTone.Up : GrowthTone.Down);
    }

    /// <summary>The change of a share against the period before, in percentage points.</summary>
    public static GrowthDelta? Points(double? now, double? before, IStringLocalizer localizer)
    {
        if (now is null || before is null)
            return null;

        var points = Math.Round((now.Value - before.Value) * 100);

        var size = Math.Abs(points);
        var text = size is 1 ? localizer[nameof(WorkbenchStrings.GrowthPoint)] : localizer[nameof(WorkbenchStrings.GrowthPoints), size.ToString("0", CultureInfo.CurrentCulture)];

        return new(text,
                   points is 0 ? GrowthTone.Flat : points > 0 ? GrowthTone.Up : GrowthTone.Down);
    }

    /// <summary>A bar's label: the day, the first day of a week, or the month in the view's calendar.</summary>
    public static string BucketLabel(DateOnly start, GrowthBucket bucket, bool withYear)
    {
        var date = start.ToDateTime(TimeOnly.MinValue);
        var culture = CultureInfo.CurrentCulture;

        return bucket switch
        {
            GrowthBucket.Month => date.ToString(withYear ? "MMM yyyy" : "MMM", culture),
            _ => date.ToString("d MMM", culture)
        };
    }
}
