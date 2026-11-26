using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// The sample world of the monitoring pages: a team's apps, each with the report entries a bit app really has, 90 days of
/// history, incidents, scans, an SLA, a status page and alerts. Everything is placed relative to the current minute and
/// drawn from a stable hash, so every run tells the same story: an online shop that is down with nobody on it yet, a notes
/// app whose email fails while someone is on it, an invoicing SaaS in maintenance, a team's tasks app that needs
/// reconnecting, a deliveries app just connected, your own app healthy, a writing app with a cache in the way and parts
/// not configured, and an inventory app someone disconnected. An agency watches six more.
/// </summary>
public static partial class MonitoringSamples
{
    /// <summary>The projects of the "some" view, in the order the server lists them: failing ones first.</summary>
    public static readonly string[] SomeKeys = ["brightcart", "fieldnote", "ledgerline", "crewboard", "parcelpoint", "yourapp", "inkpad", "stockroom"];

    /// <summary>The team, who alerts reach: one without a confirmed phone number and one without a confirmed email.</summary>
    public static readonly SampleMember[] Team =
    [
        new(MonitoringNoise.IdOf("member:maya"), "Maya Chen", "maya@yourteam.com", "+1 415 555 0134"),
        new(MonitoringNoise.IdOf("member:daniel"), "Daniel Ortiz", "daniel@yourteam.com", null),
        new(MonitoringNoise.IdOf("member:sara"), "Sara Rahimi", "sara@yourteam.com", "+44 20 7946 0958"),
        new(MonitoringNoise.IdOf("member:leo"), "Leo Fischer", null, null)
    ];

    /// <summary>The members whose devices allow no notification from bit Fabric.</summary>
    private static readonly HashSet<string> withoutDevice = ["Sara Rahimi"];

    public static SampleMember Member(string firstName) => Team.First(member => member.Name.StartsWith(firstName, StringComparison.Ordinal));

    /// <summary>The current minute, which every sample time is placed from.</summary>
    public static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return new(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);
    }

    public static List<SampleProject> Create(DateTimeOffset now) =>
    [
        Brightcart(now),
        Fieldnote(now),
        Ledgerline(now),
        Crewboard(now),
        Parcelpoint(now),
        YourApp(now),
        Inkpad(now),
        Stockroom(now),
        .. Agency(now)
    ];

    // ---------------------------------------------------------------- Building blocks

    private static readonly string[] usualParts =
    [
        "binStorage", "AppDbContext", "hangfire", "appCertificate", "StackExchange.Redis_redis-persistent", "userProfileImages",
        "cloudflare", "backup", "smtp", "emailDelivery", "sms", "firebase", "apns", "aiChat", "aiEmbedding"
    ];

    private static SampleProject NewProject(DateTimeOffset now, string key, string name, string host, string timeZoneId, int watchedDays,
        string? serverHost = null, bool hasIcon = false, string templateVersion = "11.0.0", int baseMs = 180)
    {
        var address = $"https://{host}";
        var deployedCommit = Commit(key, 0);

        var project = new SampleProject
        {
            Key = key,
            Id = MonitoringNoise.IdOf($"project:{key}"),
            Name = name,
            Address = address,
            ServerAddress = serverHost is null ? null : $"https://{serverHost}",
            TemplateVersion = templateVersion,
            IconId = hasIcon ? MonitoringIcons.IdOf(key) : null,
            // A project connected today was connected a few minutes ago.
            CreatedOn = watchedDays is 0 ? now.AddMinutes(-3) : now.AddDays(-watchedDays).AddHours(-MonitoringNoise.Between(key, 1, 1, 20)),
            CheckedAt = now.AddSeconds(-MonitoringNoise.Between(key, 2, 8, 50)),
            BaseMs = baseMs,
            Locations = [.. NetworkLocations.Defaults],
            ApplicationVersion = $"{templateVersion}+{deployedCommit}",
            TimeZoneId = timeZoneId,
            ApprovedBy = $"maya@{(host.StartsWith("app.", StringComparison.Ordinal) ? host[4..] : host)}",
            ApprovedOn = now.AddDays(-watchedDays),
            StatusPage = new() { Slug = key, Languages = [MonitoringTexts.MainCulture] }
        };

        project.Sla = new() { MonitoringId = project.Id };
        project.ScanSettings = new() { MonitoringId = project.Id };
        project.StatusPage.MonitoringId = project.Id;

        return project;
    }

    /// <summary>A short commit hash, the same for a project and an index on every run.</summary>
    public static string Commit(string key, int index)
        => string.Concat(Enumerable.Range(0, 7).Select(i => "0123456789abcdef"[MonitoringNoise.Between($"{key}:commit:{index}", i, 0, 15)]));

    /// <summary>Healthy entries of the report, each with the time it usually takes.</summary>
    private static void AddParts(SampleProject project, DateTimeOffset now, params string[] keys)
    {
        foreach (var key in keys)
        {
            var part = MonitoringParts.Find(key);
            var ms = Math.Max(0, (int)Math.Round(part.TypicalMs * (0.7 + 0.6 * MonitoringNoise.Of($"{project.Key}:{key}", 0))));
            var behind = MonitoringParts.Behind.GetValueOrDefault(key);

            project.Checks.Add(new()
            {
                Key = key,
                Title = part.Title,
                IconName = part.IconName,
                About = part.About,
                Provider = behind.Provider,
                Host = behind.Host,
                Model = behind.Model,
                Category = part.Category,
                Tags = [$"{HealthCheckCategories.TagPrefix}{part.Category}"],
                Status = HealthCheckStatus.Healthy,
                DurationMs = ms,
                TimeoutMs = part.TimeoutMs,
                VerifiedAt = part.CacheMinutes is { } minutes ? now.AddMinutes(-MonitoringNoise.Between($"{project.Key}:{key}", 1, 0, minutes - 1)).AddSeconds(-MonitoringNoise.Between($"{project.Key}:{key}", 2, 5, 55)) : null
            });
        }
    }

    /// <summary>An entry that only misses its settings: a setup finding, reported Degraded outside development.</summary>
    private static void AddNotConfigured(SampleProject project, string key)
    {
        var part = MonitoringParts.Find(key);

        project.Checks.Add(new()
        {
            Key = key,
            Title = part.Title,
            IconName = part.IconName,
            About = part.About,
            Category = part.Category,
            Tags = [$"{HealthCheckCategories.TagPrefix}{part.Category}"],
            Status = HealthCheckStatus.Degraded,
            Description = $"Not configured. Set {MonitoringParts.Settings.GetValueOrDefault(key, key)} in the app's settings.",
            IsNotConfigured = true
        });
    }

    /// <summary>An entry that fails now, since a time, with what the app says about it, and its open period.</summary>
    private static void Fail(SampleProject project, string key, HealthCheckStatus status, DateTimeOffset since, string? description, int? durationMs = null)
    {
        var check = project.Checks.First(check => check.Key == key);
        check.Status = status;
        check.Since = since;
        check.Description = description;

        if (durationMs is { } ms)
        {
            check.DurationMs = ms;
        }

        project.Periods.Add(new() { CheckKey = key, Title = check.Title, Status = status, StartedAt = since, Description = description });
    }

    private static void Period(SampleProject project, string key, HealthCheckStatus status, DateTimeOffset from, DateTimeOffset to, string? description = null)
        => project.Periods.Add(new() { CheckKey = key, Title = project.TitleOf(key), Status = status, StartedAt = from, EndedAt = to, Description = description });

    private static void Deploy(SampleProject project, DateTimeOffset at, int commitIndex)
    {
        project.Starts.Add(new() { At = at, IsDeploy = true, ApplicationVersion = $"{project.TemplateVersion ?? "11.0.0"}+{Commit(project.Key, commitIndex)}" });
    }

    private static void Restart(SampleProject project, DateTimeOffset at, int commitIndex)
    {
        project.Starts.Add(new() { At = at, IsDeploy = false, ApplicationVersion = $"{project.TemplateVersion ?? "11.0.0"}+{Commit(project.Key, commitIndex)}" });
    }

    private static ProjectMaintenanceWindowDto Window(SampleProject project, string title, DateTimeOffset startsAt, TimeSpan length, bool weekly = false)
    {
        var window = new ProjectMaintenanceWindowDto
        {
            Id = MonitoringNoise.IdOf($"{project.Key}:window:{title}"),
            MonitoringId = project.Id,
            Title = title,
            StartsAt = startsAt,
            EndsAt = startsAt + length,
            RepeatsWeekly = weekly,
            Version = 1
        };

        project.Maintenance.Add(window);
        return window;
    }

    private static void Route(SampleProject project, int step, int delayMinutes, ProjectAlertChannel channel, SampleMember? member = null, string? address = null, ProjectWebhookFormat? format = null)
    {
        project.Routes.Add(new()
        {
            Id = MonitoringNoise.IdOf($"{project.Key}:route:{project.Routes.Count}"),
            Step = step,
            DelayMinutes = step is 0 ? 0 : delayMinutes,
            Channel = channel,
            UserId = member?.UserId,
            UserName = member?.Name,
            Address = address,
            WebhookFormat = format
        });
    }

    /// <summary>
    /// An incident bit Fabric opened by itself, titled and updated in every language, with the alerts its routes sent: step 0
    /// at once, a later step once its delay passed with nobody on it, and everyone reached once it was over.
    /// </summary>
    private static SampleIncident AutoIncident(SampleProject project, string id, ProjectHealth impact, DateTimeOffset start, DateTimeOffset? end,
        string[] keys, DateTimeOffset? afterDeployAt = null, bool duringMaintenance = false, SampleMember? acknowledgedBy = null, TimeSpan? acknowledgedAfter = null, DateTimeOffset? now = null)
    {
        var partTitles = keys.Select(project.TitleOf).ToArray();
        var subject = partTitles.Length is 0 ? project.Name : null;

        var incident = new SampleIncident
        {
            Id = MonitoringNoise.IdOf($"{project.Key}:incident:{id}"),
            Kind = ProjectIncidentKind.Incident,
            Impact = impact,
            StartedAt = start,
            EndedAt = end,
            CheckKeys = keys,
            IsAutomatic = true,
            AfterDeployAt = afterDeployAt,
            DuringMaintenance = duringMaintenance,
            Title = MonitoringTexts.OfBitFabric(localizer =>
            {
                var parts = subject ?? MonitoringWords.Parts(localizer, partTitles);
                return keys.Length is 0
                    ? localizer[nameof(AppStrings.MonitoringIncidentTitleNotAnswering), parts]
                    : impact is ProjectHealth.Down
                        ? localizer[nameof(AppStrings.MonitoringIncidentTitleStoppedWorking), parts]
                        : localizer[nameof(AppStrings.MonitoringIncidentTitleNotWorkingProperly), parts];
            })
        };

        incident.Updates.Add(new()
        {
            At = start,
            Phase = ProjectIncidentPhase.Investigating,
            Text = MonitoringTexts.OfBitFabric(localizer =>
            {
                var parts = subject ?? MonitoringWords.Parts(localizer, partTitles);
                return afterDeployAt is { } deployedAt
                    ? localizer[nameof(AppStrings.MonitoringIncidentOpenedAfterDeploy), parts, MonitoringWords.Duration(localizer, start - deployedAt)]
                    : localizer[nameof(AppStrings.MonitoringIncidentOpened), parts];
            })
        });

        if (acknowledgedBy is not null)
        {
            incident.AcknowledgedBy = acknowledgedBy.Name;
            incident.AcknowledgedAt = start + (acknowledgedAfter ?? TimeSpan.FromMinutes(4));
        }

        if (end is { } ended)
        {
            incident.Updates.Insert(0, new()
            {
                At = ended,
                Phase = ProjectIncidentPhase.Resolved,
                Text = MonitoringTexts.OfBitFabric(localizer => localizer[nameof(AppStrings.MonitoringIncidentResolvedUpdate), subject ?? MonitoringWords.Parts(localizer, partTitles), MonitoringWords.Duration(localizer, ended - start)])
            });

            incident.AgentPrompt = $"bit Fabric saw the app at {project.Address} fail ({string.Join(", ", keys.Length is 0 ? ["$home"] : keys)}) from {start:u} to {ended:u}. Read the app's logs and traces around that time, find the cause, and propose a fix.";
        }

        if (duringMaintenance is false)
        {
            AddAlerts(project, incident, now ?? end ?? start);
        }

        project.Incidents.Add(incident);
        return incident;
    }

    /// <summary>The alerts an incident's routes sent, newest first, as far as they had come by <paramref name="until"/>.</summary>
    private static void AddAlerts(SampleProject project, SampleIncident incident, DateTimeOffset until)
    {
        List<ProjectAlertDto> alerts = [];
        var reached = new List<ProjectAlertRouteDto>();
        var elapsed = TimeSpan.Zero;

        foreach (var step in project.Routes.GroupBy(route => route.Step).OrderBy(step => step.Key))
        {
            elapsed += TimeSpan.FromMinutes(step.First().DelayMinutes);
            var sentAt = incident.StartedAt + elapsed + TimeSpan.FromSeconds(20);

            // A later step goes out only if nobody is on it yet and the incident is still open by then.
            if (step.Key > 0 && ((incident.AcknowledgedAt is { } ackAt && ackAt <= sentAt) || (incident.EndedAt is { } endedAt && endedAt <= sentAt)))
                break;

            if (sentAt > until)
                break;

            foreach (var route in step)
            {
                alerts.Add(Alert(route, step.Key is 0 ? ProjectAlertKind.Opened : ProjectAlertKind.Escalated, sentAt));
                reached.Add(route);
            }
        }

        if (incident.EndedAt is { } ended && ended <= until)
        {
            alerts.AddRange(reached.Select(route => Alert(route, ProjectAlertKind.Resolved, ended.AddSeconds(15))));
        }

        incident.Alerts.AddRange(alerts.OrderByDescending(alert => alert.SentAt));
    }

    /// <summary>One alert as the route's channel delivers it, failing the way it would for that member or webhook.</summary>
    public static ProjectAlertDto Alert(ProjectAlertRouteDto route, ProjectAlertKind kind, DateTimeOffset sentAt)
    {
        var member = Team.FirstOrDefault(member => member.UserId == route.UserId);

        var (target, problem, statusCode) = route.Channel switch
        {
            ProjectAlertChannel.Push => (member?.Name, member is not null && withoutDevice.Contains(member.Name) ? ProjectAlertProblem.NoDevice : (ProjectAlertProblem?)null, (int?)null),
            ProjectAlertChannel.Email => (member?.Email ?? member?.Name, member?.Email is null ? ProjectAlertProblem.NoConfirmedEmail : null, null),
            ProjectAlertChannel.Sms => (member?.PhoneNumber ?? member?.Name, member?.PhoneNumber is null ? ProjectAlertProblem.NoConfirmedPhoneNumber : null, null),
            _ => (Uri.TryCreate(route.Address, UriKind.Absolute, out var uri) ? uri.Host : route.Address,
                  route.Address?.Contains("/old-", StringComparison.Ordinal) is true ? ProjectAlertProblem.WebhookRefused
                  : route.Address?.Contains("/timeout", StringComparison.Ordinal) is true ? ProjectAlertProblem.Failed : null,
                  route.Address?.Contains("/old-", StringComparison.Ordinal) is true ? 410 : null)
        };

        return new() { SentAt = sentAt, Kind = kind, Channel = route.Channel, Target = target, Succeeded = problem is null, Problem = problem, ProblemStatusCode = statusCode };
    }

    /// <summary>The newest reading of each location the project is checked from, with the data center it ran in.</summary>
    private static void AddReadings(SampleProject project, DateTimeOffset now, params string[] noAnswerFrom)
    {
        foreach (var location in project.Locations)
        {
            var colos = Colos.GetValueOrDefault(location, ["FRA"]);
            var index = Array.IndexOf(NetworkLocations.All.Select(l => l.Code).ToArray(), location);

            project.Readings.Add(new()
            {
                Location = location,
                Colo = colos[MonitoringNoise.Between($"{project.Key}:colo", index, 0, colos.Length - 1)],
                At = now.AddSeconds(-MonitoringNoise.Between($"{project.Key}:reading", index, 5, 55)),
                ResponseMs = noAnswerFrom.Contains(location) ? null : (int)Math.Round(project.BaseMs * (0.8 + 0.5 * MonitoringNoise.Of($"{project.Key}:reading-ms", index)) + RegionOffsets.GetValueOrDefault(location, 40))
            });
        }
    }

    /// <summary>The data centers a location's readings run in: a location is a hint Cloudflare follows as best it can.</summary>
    public static readonly Dictionary<string, string[]> Colos = new()
    {
        ["wnam"] = ["SJC", "LAX", "SEA"],
        ["enam"] = ["IAD", "EWR", "ATL"],
        ["weur"] = ["AMS", "CDG", "LHR"],
        ["eeur"] = ["WAW", "OTP", "VIE"],
        ["apac"] = ["SIN", "HKG"],
        ["apac-ne"] = ["NRT", "ICN"],
        ["apac-se"] = ["SIN", "BKK", "KUL"],
        ["oc"] = ["SYD", "MEL"]
    };

    /// <summary>How much further a location is than bit Fabric's own server in Germany, in milliseconds.</summary>
    public static readonly Dictionary<string, int> RegionOffsets = new()
    {
        ["Germany"] = 0,
        ["wnam"] = 140,
        ["enam"] = 85,
        ["weur"] = 12,
        ["eeur"] = 24,
        ["apac"] = 170,
        ["apac-ne"] = 210,
        ["apac-se"] = 190,
        ["oc"] = 260
    };

    /// <summary>A few short blips over the watched days, each a part slow for some minutes, so the history isn't flat.</summary>
    private static void AddBlips(SampleProject project, DateTimeOffset now, int count, params string[] keys)
    {
        var watchedDays = Math.Max(1, (int)(now - project.CreatedOn).TotalDays);

        for (var i = 0; i < count; i++)
        {
            var daysAgo = 1 + MonitoringNoise.Between($"{project.Key}:blip-day", i, 0, Math.Max(0, Math.Min(88, watchedDays - 2)));
            var start = now.Date.AddDays(-daysAgo).AddMinutes(MonitoringNoise.Between($"{project.Key}:blip-minute", i, 60, 1380));
            var key = keys[MonitoringNoise.Between($"{project.Key}:blip-key", i, 0, keys.Length - 1)];
            var minutes = MonitoringNoise.Between($"{project.Key}:blip-length", i, 2, 14);

            Period(project, key, HealthCheckStatus.Degraded, new DateTimeOffset(start, TimeSpan.Zero), new DateTimeOffset(start, TimeSpan.Zero).AddMinutes(minutes), "Took longer than usual to answer.");
        }
    }

    private static DateTimeOffset DaysAgo(DateTimeOffset now, int days, int hour, int minute)
        => new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(-days).AddHours(hour).AddMinutes(minute);

    /// <summary>The next time a weekday comes at a time of day, in UTC, from now.</summary>
    private static DateTimeOffset Next(DateTimeOffset now, DayOfWeek day, int hour, int minute)
    {
        var date = now.UtcDateTime.Date;
        var at = new DateTimeOffset(date, TimeSpan.Zero).AddHours(hour).AddMinutes(minute);

        while (at.DayOfWeek != day || at <= now)
        {
            at = at.AddDays(1);
        }

        return at;
    }
}
