using Bit.Fabric.Shared.Features.Growth.Dtos;

namespace Bit.Fabric.Workbench.Components.Pages.Growth.Data;

/// <summary>
/// A sample world counted the way the server counts bit Fabric's own (GrowthService): projects made, first Licenses, first
/// Licenses of teams, accounts, projects first watched in production, and Trials ending and converting, per day; the
/// days to a License; and the teams by their projects.
/// </summary>
public static class GrowthSampleData
{
    public static GrowthDataDto Count(GrowthSampleWorld world)
    {
        var days = new Dictionary<DateOnly, GrowthDayDto>();

        GrowthDayDto Day(DateOnly day) => days.TryGetValue(day, out var found) ? found : days[day] = new() { Day = day };

        foreach (var project in world.Projects)
        {
            Day(project.TrialStartedOn ?? project.LicensedOn ?? project.MadeOn).ProjectsMade++;

            if (project.LicensedOn is { } licensedOn)
            {
                Day(licensedOn).ProjectsLicensed++;
            }

            if (project.InProductionOn is { } liveOn)
            {
                Day(liveOn).InProduction++;
            }

            if (project.TrialEndsOn is { } endsOn)
            {
                var ended = Day(endsOn);
                ended.TrialsEnded++;

                if (project.LicensedOn is { } licensed)
                {
                    ended.TrialsEndedLicensed++;
                    Day(licensed > endsOn ? licensed : endsOn).TrialsConverted++;
                }
            }
        }

        foreach (var account in world.Accounts)
        {
            Day(account).AccountsCreated++;
        }

        var licensedByTeam = world.Projects.Where(project => project.LicensedOn is not null && project.TeamId is not null)
                                           .GroupBy(project => project.TeamId!.Value)
                                           .ToArray();

        foreach (var team in licensedByTeam)
        {
            Day(team.Min(project => project.LicensedOn!.Value)).TeamsLicensed++;
        }

        var counts = licensedByTeam.Select(team => team.Count()).ToArray();

        return new()
        {
            Today = world.Today,
            Days = [.. days.Values.OrderBy(day => day.Day)],
            LicenseDelays = [.. world.Projects.Where(project => project.TrialStartedOn is not null && project.LicensedOn is not null)
                                              .GroupBy(project => (Day: project.LicensedOn!.Value, Days: project.LicensedOn!.Value.DayNumber - project.TrialStartedOn!.Value.DayNumber))
                                              .Select(group => new GrowthLicenseDelayDto { Day = group.Key.Day, Days = group.Key.Days, Count = group.Count() })],
            TeamsByProjects = [counts.Count(count => count is 1), counts.Count(count => count is 2), counts.Count(count => count is >= 3 and <= 5), counts.Count(count => count >= 6)],
            TeamProjects = counts.Sum(),
            PeopleInLicensedTeams = licensedByTeam.Sum(team => world.Teams[team.Key].Members)
        };
    }
}
