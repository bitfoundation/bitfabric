namespace Bit.Fabric.Workbench.Components.Pages.Growth.Data;

/// <summary>
/// A sample project as bit Fabric keeps one: when it was made, its Trial, its first License, the team that holds the
/// License, and when monitoring started watching it in production, linked by the project's id.
/// </summary>
public sealed record GrowthSampleProject(DateOnly MadeOn, DateOnly? TrialStartedOn, DateOnly? LicensedOn, int? TeamId, DateOnly? InProductionOn = null)
{
    public DateOnly? TrialEndsOn => TrialStartedOn?.AddDays(GrowthSamples.TrialDays);
}

/// <summary>A sample team: the day it was made and how many people are in it.</summary>
public sealed record GrowthSampleTeam(DateOnly MadeOn, int Members);

/// <summary>
/// Everything the page counts, made up: the projects, the teams that hold their Licenses (by the index a project names),
/// and every account's sign-up day.
/// </summary>
public sealed record GrowthSampleWorld(DateOnly Today, GrowthSampleProject[] Projects, GrowthSampleTeam[] Teams, DateOnly[] Accounts)
{
    public static GrowthSampleWorld Empty(DateOnly today) => new(today, [], [], []);
}

/// <summary>
/// Made-up sample worlds, the same on every run: months of growth, a first day, and a slow last week. Nothing here is a
/// real figure.
/// </summary>
public static class GrowthSamples
{
    public const int TrialDays = Bit.Fabric.Shared.Features.Growth.GrowthReport.TrialDays;

    public const string HistorySwitch = "history";

    public const string Months = "months";

    public const string FirstDay = "first-day";

    public const string SlowWeek = "slow-week";

    /// <summary>The sample's today, fixed so the page looks the same on every run.</summary>
    public static readonly DateOnly Today = new(2026, 11, 26);

    public static GrowthSampleWorld Build(string? history) => history switch
    {
        FirstDay => BuildFirstDay(),
        SlowWeek => BuildMonths(slowLastWeek: true),
        _ => BuildMonths(slowLastWeek: false)
    };

    /// <summary>The day bit Fabric started: three projects, one licensed the same day by a team of two.</summary>
    private static GrowthSampleWorld BuildFirstDay()
    {
        GrowthSampleProject[] projects =
        [
            new(Today, Today, Today, 0),
            new(Today, Today, null, null),
            new(Today, Today, null, null)
        ];

        return new(Today, projects, [new(Today, 2)], [Today, Today, Today]);
    }

    /// <summary>
    /// Fourteen months: one or two projects a day at first and around twenty now, fewer at weekends; about two Trials in five
    /// get a License, many of them after the reminders start on day 14; some teams license several projects, and about one
    /// licensed project in five goes live with monitoring watching it.
    /// </summary>
    private static GrowthSampleWorld BuildMonths(bool slowLastWeek)
    {
        const int days = 420;

        var random = new Random(2611);
        var first = Today.AddDays(1 - days);
        var projects = new List<GrowthSampleProject>();
        var teams = new List<GrowthSampleTeam>();
        var projectsOfTeam = new List<int>();

        for (var index = 0; index < days; index++)
        {
            var day = first.AddDays(index);
            var progress = index / (double)(days - 1);
            var rate = 1.2 + 21 * Math.Pow(progress, 2.4);

            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                rate *= 0.55;
            }

            if (slowLastWeek && Today.DayNumber - day.DayNumber < 7)
            {
                rate *= 0.4;
            }

            var made = (int)Math.Round(rate * (0.7 + random.NextDouble() * 0.6));

            for (var count = 0; count < made; count++)
            {
                projects.Add(MakeProject(day, random, teams, projectsOfTeam));
            }
        }

        // Every member of a team has an account, and a third as many again signed up for something else, like a look around.
        var accounts = new List<DateOnly>();

        foreach (var team in teams)
        {
            accounts.AddRange(Enumerable.Repeat(team.MadeOn, team.Members));
        }

        for (var count = accounts.Count / 3; count > 0; count--)
        {
            accounts.Add(first.AddDays(random.Next(days)));
        }

        return new(Today, [.. projects], [.. teams], [.. accounts]);
    }

    private static GrowthSampleProject MakeProject(DateOnly day, Random random, List<GrowthSampleTeam> teams, List<int> projectsOfTeam)
    {
        // A few get their License without a Trial: an older project, or bit new without a connection.
        var hasTrial = random.NextDouble() >= 0.04;
        var licensedOn = hasTrial is false ? day : random.NextDouble() < 0.42 ? day.AddDays(DaysToLicense(random)) : (DateOnly?)null;

        if (licensedOn is null || licensedOn > Today)
            return new(day, day, null, null);

        var team = PickTeam(licensedOn.Value, random, teams, projectsOfTeam);
        var liveOn = random.NextDouble() < 0.22 ? licensedOn.Value.AddDays(random.Next(5, 60)) : (DateOnly?)null;

        return new(day, hasTrial ? day : null, licensedOn, team, liveOn <= Today ? liveOn : null);
    }

    /// <summary>Few on the first days, more once the reminders start on day 14, and some after the Trial ended.</summary>
    private static int DaysToLicense(Random random)
    {
        var roll = random.NextDouble();

        return roll switch
        {
            < 0.15 => 0,
            < 0.42 => random.Next(1, 8),
            < 0.55 => random.Next(8, 15),
            < 0.88 => random.Next(15, 31),
            _ => random.Next(31, 80)
        };
    }

    /// <summary>A returning team a quarter of the time, those with more projects likelier; otherwise a new team of one to four.</summary>
    private static int PickTeam(DateOnly licensedOn, Random random, List<GrowthSampleTeam> teams, List<int> projectsOfTeam)
    {
        if (teams.Count > 0 && random.NextDouble() < 0.27)
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var candidate = random.Next(teams.Count);

                if (teams[candidate].MadeOn <= licensedOn && random.NextDouble() < 0.35 + projectsOfTeam[candidate] * 0.15)
                {
                    projectsOfTeam[candidate]++;
                    return candidate;
                }
            }
        }

        var members = random.NextDouble() switch { < 0.62 => 1, < 0.85 => 2, < 0.95 => 3, _ => 4 };

        teams.Add(new(licensedOn.AddDays(-random.Next(0, 3)), members));
        projectsOfTeam.Add(1);

        return teams.Count - 1;
    }
}
