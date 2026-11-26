using System.Globalization;

namespace Bit.Fabric.Workbench.Components.Pages.Growth.Data;

/// <summary>The span the page shows, picked at its top: each is compared with the same length of time just before it, but All.</summary>
public enum GrowthPeriod { Week, Month, Quarter, Year, All }

/// <summary>What a bar or a point of the charts stands for.</summary>
public enum GrowthBucket { Day, Week, Month }

/// <summary>A count in the period and in the same length of time just before it.</summary>
public readonly record struct GrowthChange(int Now, int Before);

/// <summary>
/// The page's figures for one period, totals only, as the server will compute them in the database: no project, name or
/// account in it. Built here from a sample world by <see cref="Compute"/>.
/// </summary>
public sealed class GrowthReport
{
    public const int DaysToLicenseBuckets = 5;

    public const int TeamBuckets = 4;

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

    public int Projects { get; init; }

    public int LicensedProjects { get; init; }

    /// <summary>The teams that hold at least one License: a License is a team's.</summary>
    public int TeamsWithLicense { get; init; }

    /// <summary>The people in those teams.</summary>
    public int PeopleInLicensedTeams { get; init; }

    public int Accounts { get; init; }

    /// <summary>Licensed projects that monitoring watches in production, linked to their License by the project's id.</summary>
    public int InProduction { get; init; }

    public GrowthChange ProjectsMade { get; init; }

    public GrowthChange ProjectsLicensed { get; init; }

    public GrowthChange NewTeamsWithLicense { get; init; }

    public GrowthChange NewAccounts { get; init; }

    public GrowthChange NewInProduction { get; init; }

    /// <summary>Of the Trials that ended in the period, the share whose project has a License; null when none did and none ran.</summary>
    public double? Conversion { get; init; }

    /// <summary>How many Trials <see cref="Conversion"/> is taken over.</summary>
    public int ConversionOf { get; init; }

    public double? ConversionBefore { get; init; }

    /// <summary>No Trial ended in the period, so <see cref="Conversion"/> is the share licensed so far.</summary>
    public bool ConversionIsSoFar { get; init; }

    public int[] MadePerBucket { get; init; } = [];

    public int[] LicensedPerBucket { get; init; } = [];

    public int[] ProjectsTotal { get; init; } = [];

    public int[] LicensedTotal { get; init; } = [];

    public int[] TeamsWithLicenseTotal { get; init; } = [];

    public double?[] ConversionAt { get; init; } = [];

    public int[] InProductionTotal { get; init; } = [];

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

    /// <summary>
    /// The figures of a period, counted as the server will: a day is a day of the viewer, a month is one of the viewer's
    /// calendar (the Persian one in Persian), and nothing after today is known.
    /// </summary>
    /// <param name="bucket">The bars or points to count by, when not the period's own.</param>
    public static GrowthReport Compute(GrowthSampleWorld world, GrowthPeriod period, Calendar calendar, GrowthBucket? bucket = null)
    {
        var today = world.Today;
        var projects = world.Projects;
        var firstDay = projects.Length is 0 ? today : projects.Min(project => project.MadeOn);

        var (shownBucket, starts, partial) = Buckets(period, today, firstDay, calendar, bucket);
        var length = Days(period);
        var from = period is GrowthPeriod.All ? DateOnly.MinValue : today.AddDays(1 - length);
        var before = period is GrowthPeriod.All ? DateOnly.MinValue : from.AddDays(-length);

        bool InPeriod(DateOnly day) => day >= from && day <= today;
        bool InBefore(DateOnly day) => day >= before && day < from;
        GrowthChange Change(IEnumerable<DateOnly> days) => new(days.Count(InPeriod), period is GrowthPeriod.All ? 0 : days.Count(InBefore));

        var licensed = projects.Where(project => project.LicensedOn is not null).ToArray();
        var firstLicenseOfTeam = licensed.Where(project => project.TeamId is not null)
                                         .GroupBy(project => project.TeamId!.Value)
                                         .ToDictionary(group => group.Key, group => group.Min(project => project.LicensedOn!.Value));
        var live = licensed.Where(project => project.InProductionOn is not null).Select(project => project.InProductionOn!.Value).ToArray();
        var trials = projects.Where(project => project.TrialStartedOn is not null).ToArray();

        int BucketOf(DateOnly day)
        {
            for (var index = starts.Length - 1; index >= 0; index--)
            {
                if (day >= starts[index])
                    return index;
            }

            return -1;
        }

        int[] PerBucket(IEnumerable<DateOnly> days)
        {
            var counts = new int[starts.Length];

            foreach (var day in days.Where(day => day <= today))
            {
                if (BucketOf(day) is var index and >= 0)
                {
                    counts[index]++;
                }
            }

            return counts;
        }

        DateOnly EndOf(int index) => index == starts.Length - 1 ? today : starts[index + 1].AddDays(-1);

        int[] TotalAt(IEnumerable<DateOnly> days)
        {
            var sorted = days.Order().ToArray();

            return [.. Enumerable.Range(0, starts.Length).Select(index => CountUpTo(sorted, EndOf(index)))];
        }

        double? ConversionAsOf(DateOnly day)
        {
            var ended = trials.Where(trial => trial.TrialEndsOn <= day).ToArray();

            return ended.Length is 0 ? null : ended.Count(trial => trial.LicensedOn <= day) / (double)ended.Length;
        }

        // Trials that ended in the period; with none, the share licensed so far.
        var endedInPeriod = trials.Where(trial => InPeriod(trial.TrialEndsOn!.Value)).ToArray();
        var endedBefore = trials.Where(trial => InBefore(trial.TrialEndsOn!.Value)).ToArray();
        var isSoFar = endedInPeriod.Length is 0;
        double? conversion = isSoFar
            ? trials.Length is 0 ? null : trials.Count(trial => trial.LicensedOn is not null) / (double)trials.Length
            : endedInPeriod.Count(trial => trial.LicensedOn is not null) / (double)endedInPeriod.Length;
        double? conversionBefore = isSoFar || endedBefore.Length is 0 || period is GrowthPeriod.All
            ? null
            : endedBefore.Count(trial => trial.LicensedOn is not null) / (double)endedBefore.Length;

        var daysToLicense = trials.Where(trial => trial.LicensedOn is { } on && InPeriod(on))
                                  .Select(trial => trial.LicensedOn!.Value.DayNumber - trial.TrialStartedOn!.Value.DayNumber)
                                  .Order()
                                  .ToArray();

        var projectsOfTeams = licensed.Where(project => project.TeamId is not null)
                                      .GroupBy(project => project.TeamId!.Value)
                                      .Select(group => group.Count())
                                      .ToArray();

        return new()
        {
            Today = today,
            FirstDay = firstDay,
            Period = period,
            Bucket = shownBucket,
            BucketStarts = starts,
            LastBucketIsPartial = partial,
            Projects = projects.Length,
            LicensedProjects = licensed.Length,
            TeamsWithLicense = firstLicenseOfTeam.Count,
            PeopleInLicensedTeams = firstLicenseOfTeam.Keys.Sum(team => world.Teams[team].Members),
            Accounts = world.Accounts.Count(day => day <= today),
            InProduction = live.Length,
            ProjectsMade = Change(projects.Select(project => project.MadeOn)),
            ProjectsLicensed = Change(licensed.Select(project => project.LicensedOn!.Value)),
            NewTeamsWithLicense = Change(firstLicenseOfTeam.Values),
            NewAccounts = Change(world.Accounts),
            NewInProduction = Change(live),
            Conversion = conversion,
            ConversionOf = isSoFar ? trials.Length : endedInPeriod.Length,
            ConversionBefore = conversionBefore,
            ConversionIsSoFar = isSoFar,
            MadePerBucket = PerBucket(projects.Select(project => project.MadeOn)),
            LicensedPerBucket = PerBucket(licensed.Select(project => project.LicensedOn!.Value)),
            ProjectsTotal = TotalAt(projects.Select(project => project.MadeOn)),
            LicensedTotal = TotalAt(licensed.Select(project => project.LicensedOn!.Value)),
            TeamsWithLicenseTotal = TotalAt(firstLicenseOfTeam.Values),
            ConversionAt = [.. Enumerable.Range(0, starts.Length).Select(index => ConversionAsOf(EndOf(index)))],
            InProductionTotal = TotalAt(live),
            TrialsLicensed = trials.Count(trial => trial.LicensedOn is not null),
            TrialsRunning = trials.Count(trial => trial.LicensedOn is null && trial.TrialEndsOn > today.AddDays(7)),
            TrialsEndingThisWeek = trials.Count(trial => trial.LicensedOn is null && trial.TrialEndsOn > today && trial.TrialEndsOn <= today.AddDays(7)),
            TrialsEndedUnlicensed = trials.Count(trial => trial.LicensedOn is null && trial.TrialEndsOn <= today),
            DaysToLicense =
            [
                daysToLicense.Count(days => days is 0),
                daysToLicense.Count(days => days is >= 1 and <= 7),
                daysToLicense.Count(days => days is >= 8 and <= 14),
                daysToLicense.Count(days => days is >= 15 and <= TrialsDays),
                daysToLicense.Count(days => days > TrialsDays)
            ],
            MedianDaysToLicense = Median(daysToLicense),
            TeamsByProjects =
            [
                projectsOfTeams.Count(count => count is 1),
                projectsOfTeams.Count(count => count is 2),
                projectsOfTeams.Count(count => count is >= 3 and <= 5),
                projectsOfTeams.Count(count => count >= 6)
            ],
            ProjectsPerTeam = projectsOfTeams.Length is 0 ? null : projectsOfTeams.Average(),
            MoreThanOneShare = projectsOfTeams.Length is 0 ? null : projectsOfTeams.Count(count => count > 1) / (double)projectsOfTeams.Length
        };
    }

    private const int TrialsDays = GrowthSamples.TrialDays;

    /// <summary>
    /// The bars of a period: days for a week or a month, rolling weeks that end today for a quarter, the calendar's months for
    /// a year; All picks by how long there has been data.
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

        // A year is the calendar's last twelve months, this one included.
        var from = period switch
        {
            GrowthPeriod.All => firstDay,
            GrowthPeriod.Year => DateOnly.FromDateTime(calendar.AddMonths(MonthStart(today, calendar).ToDateTime(TimeOnly.MinValue), -11)),
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
                var month = MonthStart(from, calendar);

                while (month <= today)
                {
                    months.Add(month);
                    month = DateOnly.FromDateTime(calendar.AddMonths(month.ToDateTime(TimeOnly.MinValue), 1));
                }

                var next = DateOnly.FromDateTime(calendar.AddMonths(months[^1].ToDateTime(TimeOnly.MinValue), 1));

                return (bucket, [.. months], next.AddDays(-1) > today);
        }
    }

    private static DateOnly MonthStart(DateOnly day, Calendar calendar)
    {
        var date = day.ToDateTime(TimeOnly.MinValue);

        return DateOnly.FromDateTime(calendar.ToDateTime(calendar.GetYear(date), calendar.GetMonth(date), 1, 0, 0, 0, 0));
    }

    private static int CountUpTo(DateOnly[] sorted, DateOnly day)
    {
        var index = Array.BinarySearch(sorted, day.AddDays(1));
        index = index < 0 ? ~index : index;

        while (index > 0 && sorted[index - 1] > day)
        {
            index--;
        }

        while (index < sorted.Length && sorted[index] <= day)
        {
            index++;
        }

        return index;
    }

    private static double? Median(int[] sorted)
    {
        if (sorted.Length is 0)
            return null;

        var middle = sorted.Length / 2;

        return sorted.Length % 2 is 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }
}
