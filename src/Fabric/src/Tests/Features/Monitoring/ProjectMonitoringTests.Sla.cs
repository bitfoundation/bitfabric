using System.Text;
using Bit.Fabric.Server.Api.Infrastructure.Data;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>The SLA tab's settings, a month's report and its CSV and PDF (bitfabric-internal/specs/monitoring.md, SLA reports).</summary>
public partial class ProjectMonitoringTests
{
    [TestMethod]
    public async Task TheSlaSettings_Should_StartFromTheDefaults_AndBeSavedAsTheOwnerSetsThem()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var sla = client.GetController<IProjectSlaController>();
        var project = await Connect(client.GetController<IMonitoringsController>(), customerApp, "customer.example");

        var settings = await sla.GetSettings(project.Id, TestContext.CancellationToken);
        Assert.AreEqual(99.9, settings.TargetPercent);
        Assert.AreEqual(0.3, settings.PartialWeight, "A partial outage counts 30% down unless the owner says otherwise.");
        Assert.AreEqual(0, settings.DegradedWeight);
        Assert.IsEmpty(settings.Credits, "No contract's credits until the owner enters theirs.");

        settings.TargetPercent = 99.95;
        settings.PartialWeight = 0.5;
        settings.Credits = [new() { BelowPercent = 99, CreditPercent = 25 }, new() { BelowPercent = 99.95, CreditPercent = 10 }];
        var saved = await sla.SaveSettings(settings, TestContext.CancellationToken);

        Assert.AreEqual(99.95, saved.TargetPercent);
        Assert.AreEqual(0.5, saved.PartialWeight);
        Assert.AreSequenceEqual(new[] { 99.95, 99 }, saved.Credits.Select(credit => credit.BelowPercent).ToArray(), "Highest threshold first.");
        Assert.AreEqual(99.95, (await sla.GetSettings(project.Id, TestContext.CancellationToken)).TargetPercent);

        // Saved again from a stale copy: someone else changed them since.
        settings.TargetPercent = 99.5;
        settings.Version = saved.Version;
        await sla.SaveSettings(settings, TestContext.CancellationToken);
        await Assert.ThrowsExactlyAsync<ConflictException>(() => sla.SaveSettings(settings, TestContext.CancellationToken));

        var current = await sla.GetSettings(project.Id, TestContext.CancellationToken);
        current.PartialWeight = 1.5;
        await Assert.ThrowsExactlyAsync<BadRequestException>(() => sla.SaveSettings(current, TestContext.CancellationToken));
        current.PartialWeight = 0.3;
        current.TargetPercent = 100;
        await Assert.ThrowsExactlyAsync<BadRequestException>(() => sla.SaveSettings(current, TestContext.CancellationToken));
    }

    /// <summary>
    /// Last month in Tehran, made by hand in the database: read the whole month, an hour down, an hour degraded, two hours of
    /// maintenance with a failure inside them, and a part's periods; then the same month as CSV and as PDF.
    /// </summary>
    [TestMethod]
    public async Task AMonthsReport_Should_CountItsIncidentsPartsAndMaintenance_AndBeExported()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var sla = client.GetController<IProjectSlaController>();
        var incidents = client.GetController<IProjectIncidentsController>();
        var project = await Connect(client.GetController<IMonitoringsController>(), customerApp, "customer.example");

        var tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        var (year, month) = ProjectSlaMonths.MonthOf(DateTimeOffset.UtcNow, tehran);
        (year, month) = ProjectSlaMonths.Add(year, month, -1);
        var from = ProjectSlaMonths.StartOf(year, month, tehran);
        var next = ProjectSlaMonths.Add(year, month, 1);
        var to = ProjectSlaMonths.StartOf(next.Year, next.Month, tehran);
        var monthMinutes = (to - from).TotalMinutes;

        var down = from.AddDays(3).AddHours(10);
        var degraded = from.AddDays(7).AddHours(14);
        var maintenance = from.AddDays(12);

        await using (var scope = server.ApiApp.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = await dbContext.Monitorings.IgnoreQueryFilters().Where(p => p.Id == project.Id).Select(p => p.TenantId).SingleAsync(TestContext.CancellationToken);

            // Added two months ago, in Tehran's time zone, and read every quarter of last month.
            await dbContext.Monitorings.IgnoreQueryFilters().Where(p => p.Id == project.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.CreatedOn, from.AddMonths(-1)).SetProperty(p => p.TimeZoneId, "Asia/Tehran"), TestContext.CancellationToken);

            await dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO "ProjectRollups" ("MonitoringId", "BucketStart", "Region", "CheckKey", "TenantId", "Count", "FailedCount", "DegradedCount", "TimedCount", "TotalMs", "MaxMs")
                SELECT {project.Id}, bucket, 'Germany', {ProjectRollupKeys.Report}, {tenantId}, 15, 0, 0, 15, 1500, 100
                FROM generate_series({from}::timestamptz, {to}::timestamptz - INTERVAL '15 minutes', INTERVAL '15 minutes') AS bucket
                ON CONFLICT DO NOTHING
                """, TestContext.CancellationToken);

            await dbContext.ProjectIncidents.AddRangeAsync(
            [
                new() { Id = Guid.NewGuid(), MonitoringId = project.Id, TenantId = tenantId, IsAutomatic = true, Impact = ProjectHealth.Down, StartedAt = down, EndedAt = down.AddHours(1), CheckKeys = ["AppDbContext"] },
                new() { Id = Guid.NewGuid(), MonitoringId = project.Id, TenantId = tenantId, IsAutomatic = true, Impact = ProjectHealth.Degraded, StartedAt = degraded, EndedAt = degraded.AddHours(1), CheckKeys = ["smtp"] },
                new() { Id = Guid.NewGuid(), MonitoringId = project.Id, TenantId = tenantId, IsAutomatic = true, Impact = ProjectHealth.Down, StartedAt = maintenance.AddMinutes(20), EndedAt = maintenance.AddMinutes(50), CheckKeys = ["AppDbContext"], DuringMaintenance = true }
            ], TestContext.CancellationToken);

            await dbContext.ProjectCheckPeriods.AddRangeAsync(
            [
                new() { Id = Guid.NewGuid(), MonitoringId = project.Id, TenantId = tenantId, CheckKey = "smtp", Title = "Email", Status = HealthCheckStatus.Healthy, StartedAt = from.AddMonths(-1), EndedAt = degraded },
                new() { Id = Guid.NewGuid(), MonitoringId = project.Id, TenantId = tenantId, CheckKey = "smtp", Title = "Email", Status = HealthCheckStatus.Degraded, StartedAt = degraded, EndedAt = degraded.AddHours(1) },
                new() { Id = Guid.NewGuid(), MonitoringId = project.Id, TenantId = tenantId, CheckKey = "smtp", Title = "Email", Status = HealthCheckStatus.Healthy, StartedAt = degraded.AddHours(1), EndedAt = to.AddDays(1) }
            ], TestContext.CancellationToken);

            await dbContext.SaveChangesAsync(TestContext.CancellationToken);
        }

        await incidents.AddMaintenanceWindow(new() { MonitoringId = project.Id, Title = "Moving the database", StartsAt = maintenance, EndsAt = maintenance.AddHours(2) }, TestContext.CancellationToken);

        var report = await sla.GetReport(new() { MonitoringId = project.Id, Year = year, Month = month }, TestContext.CancellationToken);

        Assert.AreEqual("Asia/Tehran", report.TimeZoneId);
        Assert.AreEqual(from, report.From, "A month starts at midnight in the project's time zone.");
        Assert.IsFalse(report.IsCurrent);
        Assert.AreEqual(monthMinutes - 120, report.App.CountedMinutes, 1e-6, "The maintenance window is left out, with the failure inside it.");
        Assert.AreEqual(60, report.App.DownMinutes, 1e-6);
        Assert.AreEqual(60, report.App.PartialMinutes, 1e-6);
        Assert.AreEqual(78, report.App.WeightedMinutes, 1e-6, "An hour down and 30% of an hour degraded.");
        Assert.AreEqual(100 * (monthMinutes - 120 - 78) / (monthMinutes - 120), report.App.Availability!.Value, 1e-9);
        Assert.AreEqual(120, report.MaintenanceMinutes, 1e-6);
        Assert.AreEqual("Moving the database", report.Maintenance.Single().Title);

        var email = report.Parts.Single(part => part.Key == "smtp");
        Assert.AreEqual("Email", email.Title);
        Assert.AreEqual(60, email.PartialMinutes, 1e-6);
        Assert.AreEqual(60, email.DownMinutes, 1e-6, "The app's hour down took email down with it.");

        Assert.HasCount(12, report.Months);
        Assert.IsTrue(report.Months[^1].IsCurrent);
        Assert.AreEqual(report.App.Availability, report.Months[^2].Availability);
        Assert.IsNull(report.Months[0].Availability, "A month before the project was added.");

        // 99.9% of the month allows a thousandth of its counted minutes, and 78 of them are spent.
        Assert.AreEqual((monthMinutes - 120) * 0.1 / 100, report.BudgetMinutes, 1e-6);
        Assert.AreEqual(0, report.LeftMinutes);

        // The month as CSV: a row for the app and one per part, each with its weights.
        var csv = await client.HttpClient.GetAsync($"{IProjectSlaController.ExportCsvUri}/{project.Id}/{year}/{month}", TestContext.CancellationToken);
        Assert.IsNotNull(csv.Content.Headers.ContentType);
        Assert.AreEqual("text/csv", csv.Content.Headers.ContentType.MediaType);
        var lines = (await csv.Content.ReadAsStringAsync(TestContext.CancellationToken)).TrimStart('\uFEFF').Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("month,time_zone,part_key,part,availability_percent", lines[0]);
        Assert.StartsWith($"{year:D4}-{month:D2},Asia/Tehran,{ProjectRollupKeys.Report},Customer app,", lines[1]);
        Assert.Contains(line => line.Contains(",smtp,Email,", StringComparison.Ordinal) && line.Contains(",0.3,0,", StringComparison.Ordinal), lines);
        Assert.EndsWith($"-sla-{year:D4}-{month:D2}.csv", csv.Content.Headers.ContentDisposition?.FileNameStar ?? csv.Content.Headers.ContentDisposition?.FileName ?? "");

        // The month as a PDF, in English, in Persian right to left, and in Hindi and Chinese with the fonts the report brings.
        foreach (var culture in new[] { "en-US", "fa-IR", "hi-IN", "zh-CN" })
        {
            var pdf = await client.HttpClient.GetAsync($"{IProjectSlaController.ExportPdfUri}/{project.Id}/{year}/{month}?culture={culture}", TestContext.CancellationToken);
            Assert.IsNotNull(pdf.Content.Headers.ContentType);
            Assert.AreEqual("application/pdf", pdf.Content.Headers.ContentType.MediaType);
            var bytes = await pdf.Content.ReadAsByteArrayAsync(TestContext.CancellationToken);
            Assert.AreEqual("%PDF", Encoding.ASCII.GetString(bytes, 0, 4), culture);
            Assert.IsGreaterThan(10_000, bytes.Length, culture);
        }

        // Only the last 12 months, the rollups that say when the app was read being kept 13.
        await Assert.ThrowsExactlyAsync<BadRequestException>(() => sla.GetReport(new() { MonitoringId = project.Id, Year = year - 2, Month = month }, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AnotherTeam_Should_SeeNoneOfAProjectsSla()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var owner = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(owner.GetController<IMonitoringsController>(), customerApp, "customer.example");
        var report = await owner.GetController<IProjectSlaController>().GetReport(new() { MonitoringId = project.Id }, TestContext.CancellationToken);
        Assert.IsTrue(report.IsCurrent);

        await using var stranger = await SignInWithATeamOfTheirOwn(server);
        var strangersSla = stranger.GetController<IProjectSlaController>();

        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => strangersSla.GetSettings(project.Id, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => strangersSla.SaveSettings(new() { MonitoringId = project.Id }, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => strangersSla.GetReport(new() { MonitoringId = project.Id }, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => stranger.HttpClient.GetAsync($"{IProjectSlaController.ExportPdfUri}/{project.Id}/{report.Year}/{report.Month}", TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => stranger.HttpClient.GetAsync($"{IProjectSlaController.ExportCsvUri}/{project.Id}/{report.Year}/{report.Month}", TestContext.CancellationToken));
    }
}
