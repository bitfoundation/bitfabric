using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Growth.Dtos;

namespace Bit.Fabric.Server.Api.Features.Growth;

/// <summary>
/// The growth page's counts (bitfabric-internal/specs/growth.md), every team's together, each one a <c>GROUP BY</c> in the
/// database by the day of the viewer's time zone: no row of a project, a team or an account leaves it. A project is a
/// codebase, known by the id in its bit.json, whichever teams hold it. A Trial bit CLI asked for from CI (its <c>Ci</c>
/// set) counts in no Trial figure, and makes a project only once that codebase holds a License, as a demo of bit
/// Boilerplate does. A day is <c>TimeZoneInfo.ConvertTimeBySystemTimeZoneId</c>'s, which PostgreSQL runs as <c>AT TIME
/// ZONE</c>. Read by <see cref="GrowthController"/>.
/// </summary>
public partial class GrowthService
{
    [AutoInject] private AppDbContext dbContext = default!;

    [AutoInject] private TimeProvider timeProvider = default!;

    public async Task<GrowthDataDto> GetData(string? timeZone, CancellationToken cancellationToken)
    {
        var (zone, tz) = Zone(timeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).DateTime);

        var trials = dbContext.ProjectTrials.AsNoTracking();
        var licenses = dbContext.IssuedLicenses.IgnoreQueryFilters().AsNoTracking();

        // Each codebase's first License, whichever team holds it.
        var firstLicenses = licenses.Where(license => license.Project!.BitProjectId != null)
                                    .GroupBy(license => license.Project!.BitProjectId!.Value)
                                    .Select(group => new { Id = group.Key, On = group.Min(license => license.IssuedOn) });

        var licensedIds = firstLicenses.Select(first => first.Id);

        var counted = trials.Where(trial => trial.Ci == null);

        var days = new Dictionary<DateOnly, GrowthDayDto>();

        GrowthDayDto Day(DateTime date)
        {
            var day = DateOnly.FromDateTime(date);

            return days.TryGetValue(day, out var found) ? found : days[day] = new() { Day = day };
        }

        // Projects made: by their Trial, and those licensed with none by their first License.
        foreach (var made in await trials.Where(trial => trial.Ci == null || licensedIds.Contains(trial.Id))
                                         .GroupBy(trial => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(trial.IssuedOn.UtcDateTime, tz).Date)
                                         .Select(group => new { group.Key, Count = group.Count() })
                                         .ToListAsync(cancellationToken))
        {
            Day(made.Key).ProjectsMade += made.Count;
        }

        foreach (var made in await firstLicenses.Where(first => trials.Any(trial => trial.Id == first.Id) == false)
                                                .GroupBy(first => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(first.On.UtcDateTime, tz).Date)
                                                .Select(group => new { group.Key, Count = group.Count() })
                                                .ToListAsync(cancellationToken))
        {
            Day(made.Key).ProjectsMade += made.Count;
        }

        foreach (var licensed in await firstLicenses.GroupBy(first => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(first.On.UtcDateTime, tz).Date)
                                                    .Select(group => new { group.Key, Count = group.Count() })
                                                    .ToListAsync(cancellationToken))
        {
            Day(licensed.Key).ProjectsLicensed = licensed.Count;
        }

        foreach (var team in await licenses.GroupBy(license => license.TenantId)
                                           .Select(group => group.Min(license => license.IssuedOn))
                                           .GroupBy(on => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(on.UtcDateTime, tz).Date)
                                           .Select(group => new { group.Key, Count = group.Count() })
                                           .ToListAsync(cancellationToken))
        {
            Day(team.Key).TeamsLicensed = team.Count;
        }

        foreach (var accounts in await dbContext.Users.AsNoTracking()
                                                .GroupBy(user => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(user.CreatedOn.UtcDateTime, tz).Date)
                                                .Select(group => new { group.Key, Count = group.Count() })
                                                .ToListAsync(cancellationToken))
        {
            Day(accounts.Key).AccountsCreated = accounts.Count;
        }

        // A licensed codebase in production from the day monitoring first watched a production environment of it.
        foreach (var live in await dbContext.Monitorings.IgnoreQueryFilters().AsNoTracking()
                                            .Where(monitoring => monitoring.Environment == ProjectMonitoring.Production
                                                && monitoring.Project!.BitProjectId != null
                                                && licensedIds.Contains(monitoring.Project.BitProjectId!.Value))
                                            .GroupBy(monitoring => monitoring.Project!.BitProjectId!.Value)
                                            .Select(group => group.Min(monitoring => monitoring.CreatedOn))
                                            .GroupBy(on => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(on.UtcDateTime, tz).Date)
                                            .Select(group => new { group.Key, Count = group.Count() })
                                            .ToListAsync(cancellationToken))
        {
            Day(live.Key).InProduction = live.Count;
        }

        foreach (var ended in await counted.GroupBy(trial => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(trial.ExpiresOn.UtcDateTime, tz).Date)
                                           .Select(group => new { group.Key, Count = group.Count(), Licensed = group.Count(trial => licensedIds.Contains(trial.Id)) })
                                           .ToListAsync(cancellationToken))
        {
            var day = Day(ended.Key);
            day.TrialsEnded = ended.Count;
            day.TrialsEndedLicensed = ended.Licensed;
        }

        var licensedTrials = counted.Join(firstLicenses, trial => trial.Id, first => first.Id, (trial, first) => new { Trial = trial.IssuedOn, Ends = trial.ExpiresOn, Licensed = first.On });

        foreach (var converted in await licensedTrials.Select(pair => pair.Ends > pair.Licensed ? pair.Ends : pair.Licensed)
                                                      .GroupBy(on => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(on.UtcDateTime, tz).Date)
                                                      .Select(group => new { group.Key, Count = group.Count() })
                                                      .ToListAsync(cancellationToken))
        {
            Day(converted.Key).TrialsConverted = converted.Count;
        }

        var delays = await licensedTrials.GroupBy(pair => new { Trial = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(pair.Trial.UtcDateTime, tz).Date, Licensed = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(pair.Licensed.UtcDateTime, tz).Date })
                                         .Select(group => new { group.Key.Trial, group.Key.Licensed, Count = group.Count() })
                                         .ToListAsync(cancellationToken);

        // How many projects each team licensed: counts only, never which team.
        var teamProjects = await licenses.GroupBy(license => license.TenantId)
                                         .Select(group => group.Select(license => license.ProjectId).Distinct().Count())
                                         .ToListAsync(cancellationToken);

        var people = await dbContext.TenantMemberships.IgnoreQueryFilters().AsNoTracking()
                                    .Where(membership => membership.AcceptedOn != null && licenses.Any(license => license.TenantId == membership.TenantId))
                                    .Select(membership => membership.UserId)
                                    .Distinct()
                                    .CountAsync(cancellationToken);

        return new()
        {
            Today = today,
            Days = [.. days.Values.OrderBy(day => day.Day)],
            LicenseDelays = [.. delays.Select(delay => new GrowthLicenseDelayDto
            {
                Day = DateOnly.FromDateTime(delay.Licensed),
                Days = DateOnly.FromDateTime(delay.Licensed).DayNumber - DateOnly.FromDateTime(delay.Trial).DayNumber,
                Count = delay.Count
            })],
            TeamsByProjects =
            [
                teamProjects.Count(count => count is 1),
                teamProjects.Count(count => count is 2),
                teamProjects.Count(count => count is >= 3 and <= 5),
                teamProjects.Count(count => count >= 6)
            ],
            TeamProjects = teamProjects.Sum(),
            PeopleInLicensedTeams = people
        };
    }

    /// <summary>The viewer's zone and its IANA name, which PostgreSQL knows; UTC for one that isn't a zone.</summary>
    private static (TimeZoneInfo Zone, string Iana) Zone(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone) || TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var zone) is false)
            return (TimeZoneInfo.Utc, "UTC");

        if (zone.HasIanaId)
            return (zone, zone.Id);

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? (zone, iana) : (TimeZoneInfo.Utc, "UTC");
    }
}
