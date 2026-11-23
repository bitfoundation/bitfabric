using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using static Bit.Fabric.Server.Api.Features.Monitoring.Services.ProjectSlaCalculator;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// The SLA formula (bitfabric-internal/specs/monitoring.md, SLA reports) on hand-made incidents, periods and windows: a
/// plain unit test, since it is a pure function of what bit Fabric stored about a month. November 2026 has 43,200 minutes.
/// </summary>
[TestClass, TestCategory("UnitTest")]
public partial class ProjectSlaCalculatorTests
{
    private const double NovemberMinutes = 30 * 24 * 60;

    private static readonly DateTimeOffset november = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset december = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void ADownHour_Should_CountInFull()
    {
        var result = Count(November() with { Incidents = [Incident(ProjectHealth.Down, november.AddDays(9).AddHours(10), TimeSpan.FromHours(1))] });

        Assert.AreEqual(NovemberMinutes, result.App.CountedMinutes);
        Assert.AreEqual(60, result.App.DownMinutes);
        Assert.AreEqual(100 * (NovemberMinutes - 60) / NovemberMinutes, result.App.Availability(0.3, 0)!.Value, 1e-9);
    }

    [TestMethod]
    public void ADegradedHour_Should_CountAsAPartialOutage_ThirtyPercentByDefault()
    {
        var start = november.AddDays(3).AddHours(8);
        var result = Count(November() with
        {
            Incidents = [Incident(ProjectHealth.Degraded, start, TimeSpan.FromHours(1))],
            Periods =
            [
                Period("smtp", HealthCheckStatus.Healthy, november.AddDays(-10), start),
                Period("smtp", HealthCheckStatus.Degraded, start, start.AddHours(1)),
                Period("smtp", HealthCheckStatus.Healthy, start.AddHours(1), null),
                Period("AppDbContext", HealthCheckStatus.Healthy, november.AddDays(-10), null)
            ]
        });

        Assert.AreEqual(0, result.App.DownMinutes);
        Assert.AreEqual(60, result.App.PartialMinutes);

        var settings = new ProjectSlaSettingsDto();
        Assert.AreEqual(0.3, settings.PartialWeight, "A partial outage counts 30% down.");
        Assert.AreEqual(0, settings.DegradedWeight);
        Assert.AreEqual(18, result.App.Weighted(settings.PartialWeight, settings.DegradedWeight), 1e-9);
        Assert.AreEqual(100 * (NovemberMinutes - 18) / NovemberMinutes, result.App.Availability(settings.PartialWeight, settings.DegradedWeight)!.Value, 1e-9);

        // The owner's weight, printed with the figure.
        Assert.AreEqual(100 * (NovemberMinutes - 60) / NovemberMinutes, result.App.Availability(1, 0)!.Value, 1e-9);

        var smtp = result.Parts.Single(part => part.Key == "smtp").Figure;
        Assert.AreEqual(60, smtp.PartialMinutes, "A part reporting Degraded is partly down.");
        Assert.AreEqual(0, smtp.DownMinutes);
        Assert.AreEqual(0, result.Parts.Single(part => part.Key == "AppDbContext").Figure.PartialMinutes);
    }

    [TestMethod]
    public void APartsRow_Should_CountItsFailures_AndTheAppBeingDown()
    {
        var failing = november.AddDays(5);
        var appDown = november.AddDays(20);
        var result = Count(November() with
        {
            Incidents = [Incident(ProjectHealth.Down, appDown, TimeSpan.FromMinutes(30))],
            Periods =
            [
                Period("AppDbContext", HealthCheckStatus.Healthy, november.AddDays(-1), failing),
                Period("AppDbContext", HealthCheckStatus.Unhealthy, failing, failing.AddMinutes(45)),
                Period("AppDbContext", HealthCheckStatus.Healthy, failing.AddMinutes(45), null),
                // A part the app added mid-month counts from then on.
                Period("paymentGateway", HealthCheckStatus.Healthy, november.AddDays(15), null)
            ]
        });

        var database = result.Parts.Single(part => part.Key == "AppDbContext").Figure;
        Assert.AreEqual(45 + 30, database.DownMinutes, "Its own failure, and the app down, when no part of it can be used.");
        Assert.AreEqual(NovemberMinutes, database.CountedMinutes);

        var payments = result.Parts.Single(part => part.Key == "paymentGateway").Figure;
        Assert.AreEqual(15 * 24 * 60, payments.CountedMinutes);
        Assert.AreEqual(30, payments.DownMinutes);
    }

    [TestMethod]
    public void Maintenance_Should_BeLeftOut_WeeklyOnesToo_WithTheIncidentsThatBeganInIt()
    {
        var oneOff = new ProjectMaintenanceWindow { Title = "Moving the database", StartsAt = november.AddDays(10), EndsAt = november.AddDays(10).AddHours(2) };
        // Every Sunday 02:00 to 03:00 UTC, from 2026-10-25 on: November has five of them (1, 8, 15, 22, 29).
        var weekly = new ProjectMaintenanceWindow { Title = "Backups", StartsAt = new(2026, 10, 25, 2, 0, 0, TimeSpan.Zero), EndsAt = new(2026, 10, 25, 3, 0, 0, TimeSpan.Zero), RepeatsWeekly = true };

        var result = Count(November() with
        {
            Maintenance = [oneOff, weekly],
            Incidents =
            [
                // Down inside the window: no alert, and not held against the app.
                Incident(ProjectHealth.Down, oneOff.StartsAt.AddMinutes(30), TimeSpan.FromMinutes(30), duringMaintenance: true),
                // Began in a weekly window and ran 30 minutes past it: left out as a whole.
                Incident(ProjectHealth.Degraded, new(2026, 11, 8, 2, 30, 0, TimeSpan.Zero), TimeSpan.FromHours(1), duringMaintenance: true)
            ]
        });

        Assert.AreEqual(0, result.App.DownMinutes);
        Assert.AreEqual(0, result.App.PartialMinutes);
        Assert.AreEqual(120 + 5 * 60 + 30, result.MaintenanceMinutes);
        Assert.AreEqual(NovemberMinutes - result.MaintenanceMinutes, result.App.CountedMinutes);
        Assert.AreEqual(100, result.App.Availability(0.3, 0));
        Assert.HasCount(6, result.Maintenance, "Each occurrence is listed in the report.");
        Assert.AreEqual(120, result.Maintenance.Single(occurrence => occurrence.Window == oneOff).CountedOutMinutes);
    }

    [TestMethod]
    public void AMonthInTehran_Should_RunFromItsMidnight()
    {
        var tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        var from = ProjectSlaMonths.StartOf(2026, 11, tehran);
        var to = ProjectSlaMonths.StartOf(2026, 12, tehran);

        // Tehran is UTC+3:30 all year (Iran keeps no daylight saving time since 2022).
        Assert.AreEqual(new DateTimeOffset(2026, 10, 31, 20, 30, 0, TimeSpan.Zero), from);
        Assert.AreEqual(new DateTimeOffset(2026, 11, 30, 20, 30, 0, TimeSpan.Zero), to);
        Assert.AreEqual((2026, 11), ProjectSlaMonths.MonthOf(new DateTimeOffset(2026, 10, 31, 21, 0, 0, TimeSpan.Zero), tehran), "Already November in Tehran.");

        // An hour down across the boundary: its first half was in October there, its second in November.
        Inputs Month(DateTimeOffset monthFrom, DateTimeOffset monthTo) => new()
        {
            From = monthFrom,
            To = monthTo,
            Now = monthTo.AddDays(3),
            WatchedSince = monthFrom.AddMonths(-2),
            Readings = [new(monthFrom.AddMonths(-2), monthTo.AddDays(3))],
            Incidents = [Incident(ProjectHealth.Down, new(2026, 10, 31, 20, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1))]
        };

        var novemberThere = Count(Month(from, to));
        var octoberThere = Count(Month(ProjectSlaMonths.StartOf(2026, 10, tehran), from));

        Assert.AreEqual(30, novemberThere.App.DownMinutes);
        Assert.AreEqual(30, octoberThere.App.DownMinutes);
        Assert.AreEqual(NovemberMinutes, novemberThere.App.CountedMinutes);

        // The same hour in UTC's November: wholly in October.
        Assert.AreEqual(0, Count(Month(november, december)).App.DownMinutes);
    }

    [TestMethod]
    public void TimeWithoutAReading_AndBeforeTheProjectWasAdded_Should_NotCount()
    {
        var added = november.AddDays(14);
        var reconnecting = november.AddDays(20);
        var result = Count(November() with
        {
            WatchedSince = added,
            // Nothing read for two hours while the approval needed reconnecting.
            Readings = [new(added, reconnecting), new(reconnecting.AddHours(2), december)],
            Incidents = [Incident(ProjectHealth.Down, reconnecting.AddHours(1), TimeSpan.FromHours(2))]
        });

        Assert.AreEqual(14 * 24 * 60, result.NotWatchedMinutes);
        Assert.AreEqual(120, result.NoDataMinutes);
        Assert.AreEqual(16 * 24 * 60 - 120, result.App.CountedMinutes);
        Assert.AreEqual(60, result.App.DownMinutes, "Only the hour bit Fabric could see.");
    }

    [TestMethod]
    public void TheErrorBudget_Should_CoverTheWholeMonth_WhileItRuns()
    {
        var now = november.AddDays(10);
        var result = Count(November() with
        {
            Now = now,
            Readings = [new(november.AddMonths(-1), now)],
            Maintenance = [new() { Title = "Planned", StartsAt = november.AddDays(25), EndsAt = november.AddDays(25).AddHours(4) }]
        });

        Assert.AreEqual(10 * 24 * 60, result.App.CountedMinutes, "So far.");
        Assert.AreEqual(NovemberMinutes - 240, result.ExpectedMinutes, "The whole month, less the maintenance already planned.");
        Assert.IsEmpty(result.Maintenance, "A window that hasn't begun isn't listed as left out yet.");

        // 99.9% of the month allows 43 minutes of downtime.
        Assert.AreEqual(42.96, result.ExpectedMinutes * (100 - 99.9) / 100, 1e-6);
    }

    [TestMethod]
    public void ACreditTier_Should_BeTheLowestTheMonthIsBelow()
    {
        ProjectSlaCreditDto[] tiers = [new() { BelowPercent = 99.9, CreditPercent = 10 }, new() { BelowPercent = 99, CreditPercent = 25 }, new() { BelowPercent = 95, CreditPercent = 50 }];

        Assert.IsNull(CreditOf(99.95, tiers));
        Assert.AreEqual(10, CreditOf(99.5, tiers)!.CreditPercent);
        Assert.AreEqual(25, CreditOf(98, tiers)!.CreditPercent);
        Assert.AreEqual(50, CreditOf(90, tiers)!.CreditPercent);
        Assert.IsNull(CreditOf(null, tiers), "A month nothing counted in owes nothing.");
    }

    /// <summary>November 2026 in UTC, watched from October and read the whole time, a few days after it ended.</summary>
    private static Inputs November() => new()
    {
        From = november,
        To = december,
        Now = december.AddDays(3),
        WatchedSince = november.AddMonths(-1),
        Readings = [new(november.AddMonths(-1), december.AddDays(3))]
    };

    private static ProjectIncident Incident(ProjectHealth impact, DateTimeOffset startedAt, TimeSpan lasted, bool duringMaintenance = false) => new()
    {
        Id = Guid.NewGuid(),
        Impact = impact,
        StartedAt = startedAt,
        EndedAt = startedAt + lasted,
        DuringMaintenance = duringMaintenance
    };

    private static ProjectCheckPeriod Period(string key, HealthCheckStatus status, DateTimeOffset from, DateTimeOffset? to) => new()
    {
        Id = Guid.NewGuid(),
        CheckKey = key,
        Status = status,
        StartedAt = from,
        EndedAt = to
    };
}
