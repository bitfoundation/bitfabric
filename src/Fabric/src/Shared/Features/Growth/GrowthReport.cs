using System.Globalization;
using Bit.Fabric.Shared.Features.Growth.Dtos;

namespace Bit.Fabric.Shared.Features.Growth;

/// <summary>The span the growth page shows: each is compared with the same length of time just before it, but All.</summary>
public enum GrowthPeriod { Week, Month, Quarter, Year, All }

/// <summary>What a bar or a point of the growth page's charts stands for.</summary>
public enum GrowthBucket { Day, Week, Month }

/// <summary>A count in the period and in the same length of time just before it.</summary>
public readonly record struct GrowthChange(int Now, int Before);

/// <summary>
/// The growth page's figures for one period, built from the day-by-day counts of <see cref="GrowthDataDto"/>: the totals,
/// the changes against the period before, the bars and lines of the charts, where every Trial is now, the days to a
/// License and the teams. Months are the given calendar's, so a Persian reader sees Persian months.
/// </summary>
public sealed class GrowthReport
{
    public const int DaysToLicenseBuckets = 5;

    public const int TeamBuckets = 4;

    /// <summary>How long a Trial lasts; its last bucket of days to a License starts the day after.</summary>
    public const int TrialDays = 30;

    public required DateOnly Today { get; init; }

    public required GrowthPeriod Period { get; init; }

    public required GrowthBucket Bucket { get; init; }

    /// <summary>The first day of each bar, oldest first; the last one runs to today.</summary>
    public required DateOnly[] BucketStarts { get; init; }

    /// <summary>The day of the first project; today when there is none.</summary>
    public DateOnly FirstDay { get; init; }

    /// <summary>How many of the first bars end before the first project, so a line can start where the data does.</summary>
    public int EmptyLead => BucketStarts.Where((_, index) => (index + 1 < BucketStarts.Length ? BucketStarts[index + 1] : Today.AddDays(1)) <= FirstDay).Count();

    /// <summary>The last bar is a month not over yet.</summary>
    public bool LastBucketIsPartial { get; init; }

    /// <summary>The days the period's counts are taken over: its length, or the days since the first project for All.</summary>
    public int PeriodDays { get; init; }

    public int Projects { get; init; }

    public int LicensedProjects { get; init; }

    /// <summary>The teams that hold at least one License: a License is a team's.</summary>
    public int TeamsWithLicense { get; init; }

    public int PeopleInLicensedTeams { get; init; }

    public int Accounts { get; init; }

    /// <summary>Licensed projects whose production environment monitoring watches.</summary>
    public int InProduction { get; init; }

    public GrowthChange ProjectsMade { get; init; }

    public GrowthChange ProjectsLicensed { get; init; }

    public GrowthChange NewTeamsWithLicense { get; init; }

    public GrowthChange NewAccounts { get; init; }

    public GrowthChange NewInProduction { get; init; }

    /// <summary>Of the Trials that ended in the period, the share whose project holds a License; null when no Trial ever ran.</summary>
    public double? Conversion { get; init; }

    /// <summary>How many Trials <see cref="Conversion"/> is taken over.</summary>
    public int ConversionOf { get; init; }

    /// <summary>How many of those have a License.</summary>
    public int ConversionLicensed { get; init; }

    public double? ConversionBefore { get; init; }

    /// <summary>No Trial ended in the period, so <see cref="Conversion"/> is the share licensed so far, over every Trial.</summary>
    public bool ConversionIsSoFar { get; init; }

    public int[] MadePerBucket { get; init; } = [];

    public int[] LicensedPerBucket { get; init; } = [];

    public int[] ProjectsTotal { get; init; } = [];

    public int[] LicensedTotal { get; init; } = [];

    public int[] TeamsWithLicenseTotal { get; init; } = [];

    public int[] InProductionTotal { get; init; } = [];

    /// <summary>Trial → License as it stood at the end of each bar.</summary>
    public double?[] ConversionAt { get; init; } = [];

    /// <summary>Where every Trial is now.</summary>
    public int TrialsLicensed { get; init; }

    public int TrialsRunning { get; init; }

    public int TrialsEndingThisWeek { get; init; }

    public int TrialsEndedUnlicensed { get; init; }

    public int TrialsInProgress => TrialsRunning + TrialsEndingThisWeek;

    public int Trials => TrialsLicensed + TrialsInProgress + TrialsEndedUnlicensed;

    /// <summary>Projects licensed in the period after a Trial, by days from its start: the same day, 1-7, 8-14, 15-30, after it.</summary>
    public int[] DaysToLicense { get; init; } = new int[DaysToLicenseBuckets];

    public double? MedianDaysToLicense { get; init; }

    /// <summary>Teams with a License by how many projects they licensed: 1, 2, 3-5, 6 or more.</summary>
    public int[] TeamsByProjects { get; init; } = new int[TeamBuckets];

    public double? ProjectsPerTeam { get; init; }

    public double? MoreThanOneShare { get; init; }

    public bool IsEmpty => Projects is 0;

    public bool HasBefore => Period is not GrowthPeriod.All;

    public static int Days(GrowthPeriod period) => period switch
    {
        GrowthPeriod.Week => 7,
        GrowthPeriod.Month => 30,
        GrowthPeriod.Quarter => 91,
        GrowthPeriod.Year => 365,
        _ => int.MaxValue
    };

    /// <summary>The figures of <paramref name="period"/>, its months in <paramref name="calendar"/>.</summary>
    /// <param name="bucket">The bars or points to count by, when not the period's own.</param>
    public static GrowthReport Compute(GrowthDataDto data, GrowthPeriod period, Calendar calendar, GrowthBucket? bucket = null)
    {
        var today = data.Today;
        var counts = new DayCounts(data.Days, today);
        var firstDay = counts.FirstMade ?? today;

        var (shownBucket, starts, partial) = Buckets(period, today, firstDay, calendar, bucket);
        var length = Days(period);
        var from = period is GrowthPeriod.All ? DateOnly.MinValue : today.AddDays(1 - length);
        var before = period is GrowthPeriod.All ? DateOnly.MinValue : from.AddDays(-length);

        GrowthChange Change(Func<GrowthDayDto, int> count)
            => new(counts.Sum(count, from, today), period is GrowthPeriod.All ? 0 : counts.Sum(count, before, from.AddDays(-1)));

        DateOnly EndOf(int index) => index == starts.Length - 1 ? today : starts[index + 1].AddDays(-1);

        int[] PerBucket(Func<GrowthDayDto, int> count) => [.. starts.Select((start, index) => counts.Sum(count, start, EndOf(index)))];

        int[] TotalAt(Func<GrowthDayDto, int> count) => [.. starts.Select((_, index) => counts.Sum(count, DateOnly.MinValue, EndOf(index)))];

        // The Trials that ended in the period; with none, the share licensed so far over every Trial.
        var endedInPeriod = counts.Sum(day => day.TrialsEnded, from, today);
        var isSoFar = endedInPeriod is 0;
        var conversionOf = isSoFar ? counts.Sum(day => day.TrialsEnded, DateOnly.MinValue, DateOnly.MaxValue) : endedInPeriod;
        var conversionLicensed = isSoFar
            ? counts.Sum(day => day.TrialsEndedLicensed, DateOnly.MinValue, DateOnly.MaxValue)
            : counts.Sum(day => day.TrialsEndedLicensed, from, today);
        var endedBefore = period is GrowthPeriod.All ? 0 : counts.Sum(day => day.TrialsEnded, before, from.AddDays(-1));

        double? ConversionAsOf(DateOnly day)
        {
            var ended = counts.Sum(count => count.TrialsEnded, DateOnly.MinValue, day);

            return ended is 0 ? null : counts.Sum(count => count.TrialsConverted, DateOnly.MinValue, day) / (double)ended;
        }

        var delays = data.LicenseDelays.Where(delay => delay.Day >= from && delay.Day <= today && delay.Count > 0).ToArray();
        var teams = data.TeamsByProjects.Sum();

        return new()
        {
            Today = today,
            FirstDay = firstDay,
            Period = period,
            Bucket = shownBucket,
            BucketStarts = starts,
            LastBucketIsPartial = partial,
            PeriodDays = period is GrowthPeriod.All ? today.DayNumber - firstDay.DayNumber + 1 : length,
            Projects = counts.Sum(day => day.ProjectsMade, DateOnly.MinValue, today),
            LicensedProjects = counts.Sum(day => day.ProjectsLicensed, DateOnly.MinValue, today),
            TeamsWithLicense = counts.Sum(day => day.TeamsLicensed, DateOnly.MinValue, today),
            PeopleInLicensedTeams = data.PeopleInLicensedTeams,
            Accounts = counts.Sum(day => day.AccountsCreated, DateOnly.MinValue, today),
            InProduction = counts.Sum(day => day.InProduction, DateOnly.MinValue, today),
            ProjectsMade = Change(day => day.ProjectsMade),
            ProjectsLicensed = Change(day => day.ProjectsLicensed),
            NewTeamsWithLicense = Change(day => day.TeamsLicensed),
            NewAccounts = Change(day => day.AccountsCreated),
            NewInProduction = Change(day => day.InProduction),
            Conversion = conversionOf is 0 ? null : conversionLicensed / (double)conversionOf,
            ConversionOf = conversionOf,
            ConversionLicensed = conversionLicensed,
            ConversionBefore = isSoFar || endedBefore is 0 ? null : counts.Sum(day => day.TrialsEndedLicensed, before, from.AddDays(-1)) / (double)endedBefore,
            ConversionIsSoFar = isSoFar,
            MadePerBucket = PerBucket(day => day.ProjectsMade),
            LicensedPerBucket = PerBucket(day => day.ProjectsLicensed),
            ProjectsTotal = TotalAt(day => day.ProjectsMade),
            LicensedTotal = TotalAt(day => day.ProjectsLicensed),
            TeamsWithLicenseTotal = TotalAt(day => day.TeamsLicensed),
            InProductionTotal = TotalAt(day => day.InProduction),
            ConversionAt = [.. starts.Select((_, index) => ConversionAsOf(EndOf(index)))],
            TrialsLicensed = counts.Sum(day => day.TrialsEndedLicensed, DateOnly.MinValue, DateOnly.MaxValue),
            TrialsRunning = counts.Sum(day => day.TrialsEnded - day.TrialsEndedLicensed, today.AddDays(8), DateOnly.MaxValue),
            TrialsEndingThisWeek = counts.Sum(day => day.TrialsEnded - day.TrialsEndedLicensed, today.AddDays(1), today.AddDays(7)),
            TrialsEndedUnlicensed = counts.Sum(day => day.TrialsEnded - day.TrialsEndedLicensed, DateOnly.MinValue, today),
            DaysToLicense =
            [
                delays.Where(delay => delay.Days <= 0).Sum(delay => delay.Count),
                delays.Where(delay => delay.Days is >= 1 and <= 7).Sum(delay => delay.Count),
                delays.Where(delay => delay.Days is >= 8 and <= 14).Sum(delay => delay.Count),
                delays.Where(delay => delay.Days is >= 15 and <= TrialDays).Sum(delay => delay.Count),
                delays.Where(delay => delay.Days > TrialDays).Sum(delay => delay.Count)
            ],
            MedianDaysToLicense = Median(delays),
            TeamsByProjects = data.TeamsByProjects.Length == TeamBuckets ? data.TeamsByProjects : new int[TeamBuckets],
            ProjectsPerTeam = teams is 0 ? null : data.TeamProjects / (double)teams,
            MoreThanOneShare = teams is 0 ? null : data.TeamsByProjects.Skip(1).Sum() / (double)teams
        };
    }

    /// <summary>
    /// The bars of a period: days for a week or a month, rolling weeks that end today for a quarter, the calendar's last
    /// twelve months for a year; All picks by how long there has been data.
    /// </summary>
    private static (GrowthBucket Bucket, DateOnly[] Starts, bool LastIsPartial) Buckets(GrowthPeriod period, DateOnly today, DateOnly firstDay, Calendar calendar, GrowthBucket? forced)
    {
        var span = today.DayNumber - firstDay.DayNumber + 1;

        var bucket = forced is { } chosen && (chosen is not GrowthBucket.Week || span > 31) ? chosen : period switch
        {
            GrowthPeriod.Week or GrowthPeriod.Month => GrowthBucket.Day,
            GrowthPeriod.Quarter => GrowthBucket.Week,
            GrowthPeriod.Year => GrowthBucket.Month,
            _ => span <= 31 ? GrowthBucket.Day : span <= 182 ? GrowthBucket.Week : GrowthBucket.Month
        };

        var from = period switch
        {
            GrowthPeriod.All => firstDay,
            GrowthPeriod.Year => AddMonths(MonthStart(today, calendar), -11, calendar),
            _ => today.AddDays(1 - Days(period))
        };

        switch (bucket)
        {
            case GrowthBucket.Day:
                return (bucket, [.. Enumerable.Range(0, today.DayNumber - from.DayNumber + 1).Select(from.AddDays)], false);

            case GrowthBucket.Week:
                var weeks = (today.DayNumber - from.DayNumber) / 7 + 1;
                return (bucket, [.. Enumerable.Range(0, weeks).Select(index => today.AddDays(-6 - 7 * (weeks - 1 - index)))], false);

            default:
                var months = new List<DateOnly>();

                for (var month = MonthStart(from, calendar); month <= today; month = AddMonths(month, 1, calendar))
                {
                    months.Add(month);
                }

                return (bucket, [.. months], AddMonths(months[^1], 1, calendar).AddDays(-1) > today);
        }
    }

    private static DateOnly MonthStart(DateOnly day, Calendar calendar)
    {
        var date = day.ToDateTime(TimeOnly.MinValue);

        return DateOnly.FromDateTime(calendar.ToDateTime(calendar.GetYear(date), calendar.GetMonth(date), 1, 0, 0, 0, 0));
    }

    private static DateOnly AddMonths(DateOnly day, int months, Calendar calendar)
        => DateOnly.FromDateTime(calendar.AddMonths(day.ToDateTime(TimeOnly.MinValue), months));

    private static double? Median(GrowthLicenseDelayDto[] delays)
    {
        var total = delays.Sum(delay => delay.Count);

        if (total is 0)
            return null;

        var sorted = delays.OrderBy(delay => delay.Days).ToArray();

        int At(int position)
        {
            var seen = 0;

            foreach (var delay in sorted)
            {
                seen += delay.Count;

                if (position < seen)
                    return delay.Days;
            }

            return sorted[^1].Days;
        }

        return total % 2 is 1 ? At(total / 2) : (At(total / 2 - 1) + At(total / 2)) / 2.0;
    }

    /// <summary>Sums of the day-by-day counts over any span of days, from running totals.</summary>
    private sealed class DayCounts
    {
        private readonly GrowthDayDto[] days;

        public DayCounts(IEnumerable<GrowthDayDto> days, DateOnly today)
        {
            this.days = [.. days.OrderBy(day => day.Day)];
            FirstMade = this.days.FirstOrDefault(day => day.ProjectsMade > 0 && day.Day <= today)?.Day;
        }

        /// <summary>The first day a project was made.</summary>
        public DateOnly? FirstMade { get; }

        public int Sum(Func<GrowthDayDto, int> count, DateOnly from, DateOnly to)
        {
            if (to < from)
                return 0;

            var sum = 0;

            foreach (var day in days)
            {
                if (day.Day > to)
                    break;

                if (day.Day >= from)
                {
                    sum += count(day);
                }
            }

            return sum;
        }
    }
}
