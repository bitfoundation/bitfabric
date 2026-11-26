using Bit.Fabric.Server.Api.Features.Monitoring;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Server.Api.Infrastructure.Data;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Tests.Features.Identity;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// What the approved designs showed and the app lacked: the cards' 30 days, disconnecting, the whole
/// history as CSV and JSON, the weekly scans email, and their tools on /mcp, from a fake customer app's rounds.
/// </summary>
public partial class ProjectMonitoringTests
{
    [TestMethod]
    public async Task TheCardsDays_Should_ComeFromTheRollups_DrawnInTheViewersTimeZone()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IMonitoringsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        var tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tehran).DateTime);

        // A quarter past midnight two days ago in Tehran, which is still the day before in UTC: 3 of its 15 rounds found the
        // app down. Noon five days ago, 4 of 15 found a part failing.
        var downAt = ProjectHistoryDays.StartOf(today.AddDays(-2), tehran).AddMinutes(15);
        var degradedAt = ProjectHistoryDays.StartOf(today.AddDays(-5), tehran).AddHours(12);

        await using (var scope = server.ApiApp.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = await TenantOf(dbContext, project.Id);

            await dbContext.ProjectRollups.AddRangeAsync([
                new ProjectRollup { MonitoringId = project.Id, TenantId = tenantId, BucketStart = downAt, Region = "Germany", CheckKey = ProjectRollup.ReportKey, Count = 15, FailedCount = 3, TimedCount = 12, TotalMs = 1200, MaxMs = 150 },
                new ProjectRollup { MonitoringId = project.Id, TenantId = tenantId, BucketStart = degradedAt, Region = "Germany", CheckKey = ProjectRollup.ReportKey, Count = 15, DegradedCount = 4, TimedCount = 15, TotalMs = 1500, MaxMs = 150 }]);
            await dbContext.SaveChangesAsync(TestContext.CancellationToken);
        }

        var inTehran = (await projects.GetMonitorings("Asia/Tehran", TestContext.CancellationToken)).Single();

        Assert.HasCount(30, inTehran.Last30Days);
        Assert.AreEqual(today, inTehran.Last30Days[^1].Date, "Today last, in the viewer's zone.");
        Assert.AreEqual(today.AddDays(-29), inTehran.Last30Days[0].Date);

        var down = inTehran.Last30Days.Single(day => day.Date == today.AddDays(-2));
        Assert.AreEqual(ProjectDayTone.Down, down.Tone);
        Assert.AreEqual(3, down.DownMinutes, "A round a minute.");
        Assert.AreEqual(80, down.Uptime!.Value, 0.001);

        var degraded = inTehran.Last30Days.Single(day => day.Date == today.AddDays(-5));
        Assert.AreEqual(ProjectDayTone.Degraded, degraded.Tone);
        Assert.AreEqual(4, degraded.DegradedMinutes);
        Assert.AreEqual(100, degraded.Uptime!.Value, 0.001, "A part failing isn't the app down.");

        // Today has the round of connecting; the days before the project and the ones without a round have no data.
        Assert.AreEqual(ProjectDayTone.Ok, inTehran.Last30Days[^1].Tone, "The check that only misses its settings is no outage.");
        Assert.AreEqual(ProjectDayTone.NoData, inTehran.Last30Days[0].Tone);
        Assert.IsNull(inTehran.Last30Days[0].Uptime);
        Assert.HasCount(27, inTehran.Last30Days.Where(day => day.Tone is ProjectDayTone.NoData));

        // The card's uptime is the same days' rounds: 3 down of 31.
        Assert.AreEqual(100.0 * 28 / 31, inTehran.Uptime30Days!.Value, 0.001);

        // The same rounds in UTC: the quarter past Tehran's midnight is the evening before.
        var inUtc = (await projects.GetMonitorings("UTC", TestContext.CancellationToken)).Single();
        var downInUtc = inUtc.Last30Days.Single(day => day.Tone is ProjectDayTone.Down);
        Assert.AreEqual(DateOnly.FromDateTime(downAt.UtcDateTime), downInUtc.Date);
        Assert.AreNotEqual(down.Date, downInUtc.Date);

        // A zone this server doesn't know, or none, is UTC.
        var unknown = (await projects.GetMonitorings("Not/AZone", TestContext.CancellationToken)).Single();
        Assert.AreSequenceEqual(inUtc.Last30Days.Select(day => (day.Date, day.Tone)).ToArray(), unknown.Last30Days.Select(day => (day.Date, day.Tone)).ToArray());
    }

    [TestMethod]
    public async Task Disconnecting_Should_ForgetTheApproval_ReadNothing_AndKeepTheHistory()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IMonitoringsController>();
        var incidents = client.GetController<IProjectIncidentsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        customerApp.Fail("smtp", "The mail server didn't answer within 5 s.");
        await Probe(server, project.Id);
        await Probe(server, project.Id);
        var incident = (await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single();

        // Someone else's team can't.
        await using (var stranger = await SignInWithATeamOfTheirOwn(server))
        {
            await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => stranger.GetController<IMonitoringsController>().Disconnect(project.Id, TestContext.CancellationToken));
        }

        var disconnected = await projects.Disconnect(project.Id, TestContext.CancellationToken);

        Assert.AreEqual(ProjectConnectionStatus.Disconnected, disconnected.ConnectionStatus);
        Assert.AreEqual(ProjectHealth.NoData, disconnected.Health);
        Assert.IsNull((await incidents.GetSettings(project.Id, TestContext.CancellationToken)).ApprovedBy, "The approval is gone.");

        await using (var scope = server.ApiApp.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.IsFalse(await dbContext.ProjectConnections.IgnoreQueryFilters().AnyAsync(connection => connection.MonitoringId == project.Id, TestContext.CancellationToken), "No token is kept.");
            Assert.IsNull((await dbContext.ProjectIncidents.IgnoreQueryFilters().SingleAsync(row => row.Id == incident.Id, TestContext.CancellationToken)).NextEscalationAt,
                          "Nobody is woken for an app nobody watches.");
        }

        // A round already under way reads nothing, and tells nobody an approval was lost: the team let it go.
        await Probe(server, project.Id);

        var card = (await projects.GetMonitorings(null, TestContext.CancellationToken)).Single();
        Assert.AreEqual(ProjectConnectionStatus.Disconnected, card.ConnectionStatus);

        await using (var scope = server.ApiApp.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.IsFalse(await dbContext.ProjectAlerts.IgnoreQueryFilters().AnyAsync(alert => alert.MonitoringId == project.Id && alert.Kind == ProjectAlertKind.ConnectionLost, TestContext.CancellationToken));
        }

        var history = await incidents.GetHistory(new() { MonitoringId = project.Id, From = DateTimeOffset.UtcNow.AddDays(-1), To = DateTimeOffset.UtcNow }, TestContext.CancellationToken);
        Assert.Contains(period => period.CheckKey == "smtp", history.Periods, "The history stays.");
        Assert.HasCount(1, history.Incidents);

        // Reconnecting is approving again, which the add page does for the same address.
        var reconnected = await Connect(projects, customerApp, "customer.example");
        Assert.AreEqual(project.Id, reconnected.Id);
        Assert.AreNotEqual(ProjectConnectionStatus.Disconnected, reconnected.ConnectionStatus);
    }

    [TestMethod]
    public async Task TheWholeHistory_Should_BeExported_AsCsvAndJson_InTheProjectsTimeZone_ForItsTeamOnly()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IMonitoringsController>();
        var incidents = client.GetController<IProjectIncidentsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        var settings = await incidents.GetSettings(project.Id, TestContext.CancellationToken);
        settings.TimeZoneId = "Asia/Tehran";
        await incidents.SaveSettings(settings, TestContext.CancellationToken);

        // An incident with the team's update, a deployment and a maintenance window.
        customerApp.Fail("smtp", "The mail server didn't answer within 5 s.");
        await Probe(server, project.Id);
        await Probe(server, project.Id);
        var incident = await incidents.Acknowledge((await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single().Id, TestContext.CancellationToken);
        await incidents.PostUpdate(new() { IncidentId = incident.Id, Phase = ProjectIncidentPhase.Identified, Text = "=1+1, the mail provider is down.", Version = incident.Version }, TestContext.CancellationToken);

        customerApp.Report.ApplicationVersion = "1.0.1+bbbb222";
        customerApp.Report.StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await Probe(server, project.Id);

        var now = DateTimeOffset.UtcNow;
        await incidents.AddMaintenanceWindow(new() { MonitoringId = project.Id, Title = "Moving the database", StartsAt = now.AddDays(1), EndsAt = now.AddDays(1).AddHours(1), RepeatsWeekly = true }, TestContext.CancellationToken);

        // More periods than a page, all from one round, as when every part fails at once: no page may lose or repeat one.
        int periodCount;
        await using (var scope = server.ApiApp.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = await TenantOf(dbContext, project.Id);
            var oneRound = new DateTimeOffset(now.UtcDateTime.Date.AddDays(-3), TimeSpan.Zero).AddHours(9);

            await dbContext.ProjectCheckPeriods.AddRangeAsync(Enumerable.Range(0, ProjectHistoryExport.PageSize + 10).Select(index => new ProjectCheckPeriod
            {
                Id = Guid.CreateVersion7(),
                MonitoringId = project.Id,
                TenantId = tenantId,
                CheckKey = $"part{index}",
                Title = $"Part {index}",
                Status = HealthCheckStatus.Unhealthy,
                StartedAt = oneRound,
                EndedAt = oneRound.AddMinutes(10)
            }), TestContext.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.CancellationToken);

            periodCount = await dbContext.ProjectCheckPeriods.IgnoreQueryFilters().CountAsync(period => period.MonitoringId == project.Id, TestContext.CancellationToken);
        }

        // CSV: one file, a row a record.
        using var csvResponse = await client.HttpClient.GetAsync($"{IMonitoringsController.ExportHistoryUri}/{project.Id}/csv", TestContext.CancellationToken);
        Assert.IsNotNull(csvResponse.Content.Headers.ContentType);
        Assert.AreEqual("text/csv", csvResponse.Content.Headers.ContentType.MediaType);
        Assert.IsNotNull(csvResponse.Content.Headers.ContentDisposition);
        Assert.AreEqual($"Customer-app-history-{DateTime.UtcNow:yyyy-MM-dd}.csv", csvResponse.Content.Headers.ContentDisposition.FileNameStar);

        var lines = (await csvResponse.Content.ReadAsStringAsync(TestContext.CancellationToken)).TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(ProjectHistoryExport.CsvHeader, lines[0]);

        var periodRows = lines.Where(line => line.StartsWith("period,", StringComparison.Ordinal)).ToArray();
        Assert.HasCount(periodCount, periodRows);
        Assert.HasCount(periodCount, periodRows.Select(row => row.Split(',')[1]).Distinct(), "Each period once, across the pages.");

        // Every time in the project's zone with its offset, and in UTC alongside: the same instant.
        var seeded = periodRows.First(row => row.Contains(",part0,", StringComparison.Ordinal)).Split(',');
        Assert.AreEqual("Asia/Tehran", seeded[3]);
        Assert.EndsWith("+03:30", seeded[4]);
        Assert.EndsWith("Z", seeded[5]);
        Assert.AreEqual(DateTimeOffset.Parse(seeded[4], CultureInfo.InvariantCulture), DateTimeOffset.Parse(seeded[5], CultureInfo.InvariantCulture));
        Assert.AreEqual("10", seeded[8], "Minutes it lasted.");

        Assert.Contains(line => line.StartsWith($"incident,{incident.Id},", StringComparison.Ordinal) && line.Contains("Email not working properly", StringComparison.Ordinal), lines);
        Assert.Contains(line => line.StartsWith("update,", StringComparison.Ordinal) && line.Contains($",{incident.Id},", StringComparison.Ordinal) && line.Contains("\"'=1+1, the mail provider is down.\"", StringComparison.Ordinal), lines,
                        "A team's text is quoted and never read as a formula.");
        Assert.Contains(line => line.StartsWith("deployment,", StringComparison.Ordinal) && line.Contains("1.0.1+bbbb222", StringComparison.Ordinal), lines);
        Assert.Contains(line => line.StartsWith("maintenance,", StringComparison.Ordinal) && line.Contains(",weekly,Moving the database,", StringComparison.Ordinal), lines);

        // JSON: the same records, grouped.
        using var jsonResponse = await client.HttpClient.GetAsync($"{IMonitoringsController.ExportHistoryUri}/{project.Id}/json", TestContext.CancellationToken);
        Assert.IsNotNull(jsonResponse.Content.Headers.ContentType);
        Assert.AreEqual("application/json", jsonResponse.Content.Headers.ContentType.MediaType);
        using var json = JsonDocument.Parse(await jsonResponse.Content.ReadAsStringAsync(TestContext.CancellationToken));
        var root = json.RootElement;

        Assert.AreEqual("Asia/Tehran", root.GetProperty("project").GetProperty("timeZone").GetString());
        Assert.AreEqual(periodCount, root.GetProperty("periods").GetArrayLength());

        var exported = root.GetProperty("incidents").EnumerateArray().Single();
        Assert.AreEqual(incident.Id, exported.GetProperty("id").GetGuid());
        Assert.AreEqual("Degraded", exported.GetProperty("impact").GetString());
        Assert.EndsWith("+03:30", exported.GetProperty("startedAt").GetString());
        Assert.EndsWith("Z", exported.GetProperty("startedAtUtc").GetString());
        Assert.Contains(update => update.GetProperty("text").GetString() == "=1+1, the mail provider is down.", exported.GetProperty("updates").EnumerateArray().ToArray());

        Assert.Contains(start => start.GetProperty("kind").GetString() == "deployment" && start.GetProperty("applicationVersion").GetString() == "1.0.1+bbbb222",
                        root.GetProperty("deploymentsAndRestarts").EnumerateArray().ToArray());
        Assert.IsTrue(root.GetProperty("maintenanceWindows").EnumerateArray().Single().GetProperty("repeatsWeekly").GetBoolean());

        // Scoped to the team like every other endpoint, and only these two formats.
        await using var stranger = await SignInWithATeamOfTheirOwn(server);
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => stranger.HttpClient.GetAsync($"{IMonitoringsController.ExportHistoryUri}/{project.Id}/csv", TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ResourceNotFoundException>(() => stranger.HttpClient.GetAsync($"{IMonitoringsController.ExportHistoryUri}/{project.Id}/json", TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<BadRequestException>(() => client.HttpClient.GetAsync($"{IMonitoringsController.ExportHistoryUri}/{project.Id}/xml", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task TheWeeklyScansEmail_Should_GoToEachMemberOnceAWeek_InTheirLanguage_AndNotOnceTurnedOff()
    {
        var customerApp = new FakeCustomerApp();
        customerApp.OpenPaths.Add("/hangfire");
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IMonitoringsController>(), customerApp, "customer.example");
        await RunQueuedScans(server, project.Id);

        // A second member of the team, who last used bit Fabric in Persian.
        var colleague = await TestUsers.SignUp(server, TestContext.CancellationToken);
        await using (var colleaguesClient = server.CreateAppClient())
        {
            await colleaguesClient.AuthManager.SignIn(new() { Email = colleague, Password = TestUsers.Password }, TestContext.CancellationToken);
        }

        Guid tenantId;
        await using (var scope = server.ApiApp.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            tenantId = await TenantOf(dbContext, project.Id);
            var colleagueId = await dbContext.Users.Where(user => user.Email == colleague).Select(user => user.Id).SingleAsync(TestContext.CancellationToken);

            await dbContext.TenantMemberships.AddAsync(new() { Id = Guid.CreateVersion7(), TenantId = tenantId, UserId = colleagueId, AcceptedOn = DateTimeOffset.UtcNow }, TestContext.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.CancellationToken);
            await dbContext.UserSessions.Where(session => session.UserId == colleagueId).ExecuteUpdateAsync(setters => setters.SetProperty(session => session.CultureName, "fa-IR"), TestContext.CancellationToken);
        }

        var held = server.ApiApp.Services.GetRequiredService<HeldEmails>();
        HeldEmail[] ScansEmails() => [.. held.All.Where(email => email.Subject.StartsWith("Test team:", StringComparison.Ordinal))];
        var now = DateTimeOffset.UtcNow;

        // Two runs at once, as on two servers: one email a member between them.
        var sent = await Task.WhenAll(SendScansEmails(server, tenantId, now), SendScansEmails(server, tenantId, now));
        Assert.AreEqual(2, sent.Sum());

        var owners = ScansEmails().Single(email => email.To != colleague);
        Assert.AreEqual("Test team: this week's scans", owners.Subject);
        Assert.Contains("Customer app", owners.Body);
        Assert.Contains("customer.example", owners.Body);
        Assert.Contains("The background jobs dashboard (/hangfire) is open to everyone", owners.Body, "The urgent findings, in plain words from their keys.");
        Assert.Contains($"/en-US/projects/{project.Id}?tab=scans", owners.Body, "Each project with a link to its Scans tab.");

        var colleagues = ScansEmails().Single(email => email.To == colleague);
        Assert.AreEqual("Test team: اسکن‌های این هفته", colleagues.Subject, "In the recipient's language.");
        Assert.Contains("dir=\"rtl\"", colleagues.Body);
        Assert.Contains($"/fa-IR/projects/{project.Id}?tab=scans", colleagues.Body);

        // However often the job runs in a week, nobody gets a second one: sign-in codes go out through the same email
        // service, which must never look like a flood to the mail provider.
        Assert.AreEqual(0, await SendScansEmails(server, tenantId, now));
        Assert.HasCount(2, ScansEmails());

        // Turned off on the Scans tab, the project is in no email: next week the team gets none.
        var scans = client.GetController<IProjectScansController>();
        var scanSettings = await scans.GetSettings(project.Id, TestContext.CancellationToken);
        Assert.IsTrue(scanSettings.WeeklyEmail, "On until someone turns it off.");
        scanSettings.WeeklyEmail = false;
        Assert.IsFalse((await scans.SaveSettings(scanSettings, TestContext.CancellationToken)).WeeklyEmail);

        Assert.AreEqual(0, await SendScansEmails(server, tenantId, now.AddDays(7)));
        Assert.HasCount(2, ScansEmails());

        // On again, the next week has its email: one a week, not one ever.
        scanSettings = await scans.GetSettings(project.Id, TestContext.CancellationToken);
        scanSettings.WeeklyEmail = true;
        await scans.SaveSettings(scanSettings, TestContext.CancellationToken);

        Assert.AreEqual(2, await SendScansEmails(server, tenantId, now.AddDays(7)));
        Assert.HasCount(4, ScansEmails());
    }

    [TestMethod]
    public async Task TheLeftoversTools_Should_GiveTheCardsDays_AndChangeTheScanSettings_OnMcp()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IMonitoringsController>(), customerApp, "customer.example");

        await using var mcp = await ConnectToMcp(server, await AccessTokenOf(client));

        var tools = await mcp.ListToolsAsync(cancellationToken: TestContext.CancellationToken);
        var setScanSettings = tools.Single(tool => tool.Name == "SetProjectScanSettings");
        Assert.IsFalse(setScanSettings.ProtocolTool.Annotations!.ReadOnlyHint);
        Assert.Contains("confirmed", setScanSettings.Description, "The model asks the user first.");
        Assert.DoesNotContain(tool => tool.Name.Contains("Export", StringComparison.Ordinal) || tool.Name.Contains("Disconnect", StringComparison.Ordinal), tools,
                              "The export is a file for the team's hands, and disconnecting stays on the page.");

        var list = await Call(mcp, "GetProjects", new() { ["timeZone"] = "Asia/Tehran" });
        Assert.AreEqual("Asia/Tehran", list.GetProperty("daysIn").GetString());
        Assert.AreEqual(0, list.GetProperty("projects").EnumerateArray().Single().GetProperty("last30DaysWithProblems").GetArrayLength(), "Nothing went wrong yet.");

        var saved = await Call(mcp, "SetProjectScanSettings", new() { ["project"] = "customer.example", ["weeklyEmail"] = false });
        Assert.IsFalse(saved.GetProperty("weeklyEmail").GetBoolean());
        Assert.IsTrue(saved.GetProperty("weeklyScans").GetBoolean(), "What it wasn't told to change stays.");

        Assert.IsFalse((await client.GetController<IProjectScansController>().GetSettings(project.Id, TestContext.CancellationToken)).WeeklyEmail, "The Scans tab sees it.");
    }

    private async Task<int> SendScansEmails(AppTestServer server, Guid tenantId, DateTimeOffset at)
    {
        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProjectScansEmailJobRunner>().SendTo(tenantId, at, TestContext.CancellationToken);
    }

    private async Task<Guid> TenantOf(AppDbContext dbContext, Guid monitoringId)
        => await dbContext.Monitorings.IgnoreQueryFilters().Where(project => project.Id == monitoringId).Select(project => project.TenantId).SingleAsync(TestContext.CancellationToken);
}
