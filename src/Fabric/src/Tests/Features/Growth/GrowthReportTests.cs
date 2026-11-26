using System.Globalization;
using Bit.Fabric.Shared.Features.Growth;
using Bit.Fabric.Shared.Features.Growth.Dtos;

namespace Bit.Fabric.Tests.Features.Growth;

/// <summary>
/// The growth page's figures built from a day-by-day count (bitfabric-internal/specs/growth.md): a period against the
/// same length of time before it, Trial → License over the Trials that ended, where every Trial is now, the days to a
/// License, and the bars of a period in the viewer's calendar.
/// </summary>
[TestClass, TestCategory("UnitTest")]
public class GrowthReportTests
{
    private static readonly DateOnly today = new(2026, 11, 26);

    private static readonly Calendar gregorian = CultureInfo.InvariantCulture.Calendar;

    [TestMethod]
    public void AMonth_Should_CountItsDays_AgainstTheMonthBefore()
    {
        var data = Data(
            Day(-40, made: 4),
            Day(-20, made: 10, licensed: 3, teams: 2, accounts: 5),
            Day(0, made: 2, licensed: 1));

        var report = GrowthReport.Compute(data, GrowthPeriod.Month, gregorian);

        Assert.AreEqual(16, report.Projects, "Every project made up to today.");
        Assert.AreEqual(new GrowthChange(12, 4), report.ProjectsMade, "The last 30 days, and the 30 before them.");
        Assert.AreEqual(4, report.LicensedProjects);
        Assert.AreEqual(2, report.TeamsWithLicense);
        Assert.AreEqual(5, report.Accounts);
        Assert.AreEqual(GrowthBucket.Day, report.Bucket);
        Assert.HasCount(30, report.BucketStarts);
        Assert.AreEqual(today, report.BucketStarts[^1]);
        Assert.AreEqual(10, report.MadePerBucket[^21], "The bar of the day 20 days ago.");
        Assert.AreEqual(14, report.ProjectsTotal[^21], "The running total counts the days before the period too.");
        Assert.AreEqual(16, report.ProjectsTotal[^1]);
        Assert.AreEqual(30, report.PeriodDays);
    }

    /// <summary>The agent's decision of 2026-11-26: a Trial still running counts neither way.</summary>
    [TestMethod]
    public void TrialToLicense_Should_BeTakenOverTheTrialsThatEnded_InThePeriod()
    {
        var data = Data(
            Day(-45, ended: 10, endedLicensed: 2),
            Day(-10, ended: 8, endedLicensed: 4, converted: 4),
            Day(-2, ended: 2, endedLicensed: 1, converted: 1),
            Day(12, ended: 50, endedLicensed: 5),
            Day(0, made: 1));

        var report = GrowthReport.Compute(data, GrowthPeriod.Month, gregorian);

        Assert.AreEqual(10, report.ConversionOf);
        Assert.AreEqual(5, report.ConversionLicensed);
        Assert.AreEqual(0.5, report.Conversion!.Value, 1e-9);
        Assert.AreEqual(0.2, report.ConversionBefore!.Value, 1e-9, "The 10 that ended in the 30 days before, 2 of them licensed.");
        Assert.IsFalse(report.ConversionIsSoFar);

        Assert.AreEqual(12, report.TrialsLicensed, "Every Trial whose project holds a License, the running ones too.");
        Assert.AreEqual(45, report.TrialsRunning, "Unlicensed, ending after next week.");
        Assert.AreEqual(0, report.TrialsEndingThisWeek);
        Assert.AreEqual(13, report.TrialsEndedUnlicensed);
        Assert.AreEqual(70, report.Trials);
    }

    [TestMethod]
    public void TrialToLicense_Should_BeTheShareSoFar_BeforeAnyTrialEnded()
    {
        var data = Data(Day(0, made: 3, licensed: 1), Day(5, ended: 2, endedLicensed: 1), Day(30, ended: 1));

        var report = GrowthReport.Compute(data, GrowthPeriod.Week, gregorian);

        Assert.IsTrue(report.ConversionIsSoFar);
        Assert.AreEqual(3, report.ConversionOf);
        Assert.AreEqual(1, report.ConversionLicensed);
        Assert.AreEqual(1, report.TrialsEndingThisWeek, "Ending within the week, with no License yet.");
        Assert.IsNull(report.ConversionBefore);
    }

    [TestMethod]
    public void DaysToALicense_Should_FallIntoTheTrialsBuckets_WithTheirMedian()
    {
        var data = Data(Day(0, made: 1));
        data.LicenseDelays =
        [
            new() { Day = today.AddDays(-1), Days = 0, Count = 2 },
            new() { Day = today.AddDays(-2), Days = 5, Count = 1 },
            new() { Day = today.AddDays(-3), Days = 20, Count = 3 },
            new() { Day = today.AddDays(-4), Days = 45, Count = 1 },
            new() { Day = today.AddDays(-90), Days = 9, Count = 50 }
        ];

        var report = GrowthReport.Compute(data, GrowthPeriod.Month, gregorian);

        Assert.AreSequenceEqual(new[] { 2, 1, 0, 3, 1 }, report.DaysToLicense, "Licenses outside the period don't count.");
        Assert.AreEqual(20, report.MedianDaysToLicense, "The 4th of 7.");
    }

    [TestMethod]
    public void AYear_Should_BeTheCalendarsLastTwelveMonths_TheLastOneSoFar()
    {
        var data = Data(Day(-400, made: 1), Day(-100, made: 2), Day(0, made: 3));

        var report = GrowthReport.Compute(data, GrowthPeriod.Year, gregorian);

        Assert.AreEqual(GrowthBucket.Month, report.Bucket);
        Assert.HasCount(12, report.BucketStarts);
        Assert.AreEqual(new DateOnly(2025, 12, 1), report.BucketStarts[0]);
        Assert.AreEqual(new DateOnly(2026, 11, 1), report.BucketStarts[^1]);
        Assert.IsTrue(report.LastBucketIsPartial, "November isn't over.");
        Assert.AreEqual(3, report.MadePerBucket[^1]);
    }

    /// <summary>A Persian reader's months are Persian months: Azar 1405 began around 22 November 2026, not on the 1st.</summary>
    [TestMethod]
    public void AYear_Should_FollowThePersianCalendar_ForAPersianReader()
    {
        var data = Data(Day(-10, made: 1), Day(-3, made: 2));
        var persian = new PersianCalendar();
        var now = today.ToDateTime(TimeOnly.MinValue);

        var report = GrowthReport.Compute(data, GrowthPeriod.Year, persian);

        Assert.AreEqual(DateOnly.FromDateTime(persian.ToDateTime(persian.GetYear(now), persian.GetMonth(now), 1, 0, 0, 0, 0)), report.BucketStarts[^1]);
        Assert.AreNotEqual(new DateOnly(2026, 11, 1), report.BucketStarts[^1]);
        Assert.AreEqual(2, report.MadePerBucket[^1], "Made on 23 November, in Azar.");
        Assert.AreEqual(1, report.MadePerBucket[^2], "Made on 16 November, in Aban.");
    }

    [TestMethod]
    public void AChange_Should_BeAPercentage_AMultiple_OrNew()
    {
        var data = Data(Day(-40, made: 2, accounts: 10), Day(-5, made: 8, accounts: 12), Day(-1, teams: 1));

        var report = GrowthReport.Compute(data, GrowthPeriod.Month, gregorian);

        Assert.AreEqual(new GrowthChange(8, 2), report.ProjectsMade);
        Assert.AreEqual(new GrowthChange(12, 10), report.NewAccounts);
        Assert.AreEqual(new GrowthChange(1, 0), report.NewTeamsWithLicense);
        Assert.IsTrue(report.HasBefore);
        Assert.IsFalse(GrowthReport.Compute(data, GrowthPeriod.All, gregorian).HasBefore);
    }

    [TestMethod]
    public void Teams_Should_BeCountedByTheirProjects()
    {
        var data = Data(Day(0, made: 1));
        data.TeamsByProjects = [6, 2, 1, 1];
        data.TeamProjects = 6 + 4 + 4 + 7;

        var report = GrowthReport.Compute(data, GrowthPeriod.Month, gregorian);

        Assert.AreEqual(21 / 10.0, report.ProjectsPerTeam!.Value, 1e-9);
        Assert.AreEqual(0.4, report.MoreThanOneShare!.Value, 1e-9);
    }

    [TestMethod]
    public void NoProject_Should_LeaveThePageEmpty()
    {
        var report = GrowthReport.Compute(new GrowthDataDto { Today = today }, GrowthPeriod.Month, gregorian);

        Assert.IsTrue(report.IsEmpty);
        Assert.IsNull(report.Conversion);
        Assert.IsNull(report.MedianDaysToLicense);
        Assert.AreEqual(today, report.FirstDay);
    }

    [TestMethod]
    public void TheRunningTotalSinceTheStart_Should_BeDrawnAPointAWeek_EndingToday()
    {
        var data = Data(Day(-200, made: 1), Day(0, made: 1));

        var report = GrowthReport.Compute(data, GrowthPeriod.All, gregorian, GrowthBucket.Week);

        Assert.AreEqual(GrowthBucket.Week, report.Bucket);
        Assert.AreEqual(today.AddDays(-6), report.BucketStarts[^1]);
        Assert.AreEqual(2, report.ProjectsTotal[^1]);
        Assert.AreEqual(0, report.EmptyLead);
    }

    private static GrowthDataDto Data(params GrowthDayDto[] days) => new() { Today = today, Days = [.. days] };

    private static GrowthDayDto Day(int offset, int made = 0, int licensed = 0, int teams = 0, int accounts = 0, int ended = 0, int endedLicensed = 0, int converted = 0) => new()
    {
        Day = today.AddDays(offset),
        ProjectsMade = made,
        ProjectsLicensed = licensed,
        TeamsLicensed = teams,
        AccountsCreated = accounts,
        TrialsEnded = ended,
        TrialsEndedLicensed = endedLicensed,
        TrialsConverted = converted
    };
}
