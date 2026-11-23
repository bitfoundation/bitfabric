using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>What a project's latest scans gave: Security's grade and points, the three scores and the days its certificate has left; no SEO score is one that runs right now.</summary>
public sealed record ScanProfile(string Grade, int SecurityScore, int Performance, int? Seo, int Accessibility, int DomainDays);

public static partial class MonitoringSamples
{
    /// <summary>The latest run of every kind, finished <paramref name="daysAgo"/> days ago, the one before it, and with <paramref name="history"/> six weeks of them.</summary>
    private static void AddScans(SampleProject project, DateTimeOffset now, ScanProfile profile, int daysAgo = 2, bool history = false)
    {
        var finishedAt = now.AddDays(-daysAgo).AddHours(-MonitoringNoise.Between(project.Key, 30, 1, 9));
        var deploy = project.ScanSettings.ScanAfterDeployments ? project.Starts.LastOrDefault(start => start.IsDeploy && start.At < finishedAt) : null;
        var trigger = deploy is null ? ProjectScanTrigger.Weekly : ProjectScanTrigger.Deployment;
        var weeks = history ? 6 : 1;
        project.ScanProfile = profile;

        foreach (var kind in ScanFindings.AvailableKinds)
        {
            var isRunning = kind is ProjectScanKind.Seo && profile.Seo is null;

            // The latest first, then each earlier week's.
            for (var week = isRunning ? 1 : 0; week <= weeks; week++)
            {
                var at = finishedAt.AddDays(-7 * week).AddMinutes(Array.IndexOf(ScanFindings.AvailableKinds, kind) * 2);
                var drift = week is 0 ? 0 : MonitoringNoise.Between($"{project.Key}:{kind}:drift", week, -6, 4);
                var outcome = history && week is 3 && kind is ProjectScanKind.Performance ? ProjectScanOutcome.Failed
                            : history && at > now.AddDays(-18) && at < now.AddDays(-16) ? ProjectScanOutcome.Unreachable
                            : ProjectScanOutcome.Completed;

                project.ScanRuns.Add(Scan(project, kind, week is 0 ? trigger : ProjectScanTrigger.Weekly, at, outcome, profile, drift, week is 0 ? deploy?.ApplicationVersion : null, week));
            }

            if (isRunning)
            {
                project.PendingScans.Add(new() { Id = MonitoringNoise.IdOf($"{project.Key}:pending:{kind}"), ProjectId = project.Id, Kind = kind, Trigger = ProjectScanTrigger.Manual, RequestedAt = now.AddMinutes(-3), StartedAt = now.AddMinutes(-2) });
            }
        }

        if (history)
        {
            // Someone ran them by hand once, after a fix.
            var manualAt = now.AddDays(-11).AddHours(-3);
            project.ScanRuns.Add(Scan(project, ProjectScanKind.Security, ProjectScanTrigger.Manual, manualAt, ProjectScanOutcome.Completed, profile, 0, null, 9));
        }

        project.ScanRuns.Sort((a, b) => Nullable.Compare(b.FinishedAt, a.FinishedAt));
    }

    /// <summary>Every kind waiting for its turn, the first <paramref name="running"/> of them already running.</summary>
    public static void AddPendingScans(SampleProject project, DateTimeOffset now, DateTimeOffset requestedAt, ProjectScanTrigger trigger, int running)
    {
        project.PendingScans.Clear();

        foreach (var (kind, index) in ScanFindings.AvailableKinds.Select((kind, index) => (kind, index)))
        {
            project.PendingScans.Add(new()
            {
                Id = MonitoringNoise.IdOf($"{project.Key}:pending:{kind}:{requestedAt.UtcTicks}"),
                ProjectId = project.Id,
                Kind = kind,
                Trigger = trigger,
                RequestedAt = requestedAt,
                StartedAt = index < running ? requestedAt.AddSeconds(20) : null
            });
        }
    }

    /// <summary>A finished scan of one kind, with its score, verdict, pages and findings.</summary>
    public static ProjectScanDto Scan(SampleProject project, ProjectScanKind kind, ProjectScanTrigger trigger, DateTimeOffset finishedAt, ProjectScanOutcome outcome, ScanProfile profile, int drift, string? applicationVersion, int index)
    {
        var scan = new ProjectScanDto
        {
            Id = MonitoringNoise.IdOf($"{project.Key}:scan:{kind}:{finishedAt.UtcTicks}"),
            ProjectId = project.Id,
            Kind = kind,
            Trigger = trigger,
            ApplicationVersion = applicationVersion,
            RequestedAt = finishedAt.AddMinutes(-4),
            StartedAt = finishedAt.AddMinutes(-3),
            FinishedAt = finishedAt,
            Outcome = outcome
        };

        if (outcome is not ProjectScanOutcome.Completed)
            return scan;

        switch (kind)
        {
            case ProjectScanKind.Security:
                var grade = drift < -3 && profile.Grade is "A" ? "B" : profile.Grade;
                scan.Grade = grade;
                scan.Score = profile.SecurityScore + drift;
                scan.Verdict = grade is "A+" or "A" ? ProjectScanVerdict.Good : grade is "B" or "C" ? ProjectScanVerdict.NeedsWork : ProjectScanVerdict.Poor;
                break;
            case ProjectScanKind.DomainAndCertificate:
                var days = profile.DomainDays + (int)Math.Round((DateTimeOffset.UtcNow - finishedAt).TotalDays);
                scan.ExpiresAt = finishedAt.AddDays(days);
                var left = profile.DomainDays;
                scan.Verdict = left >= 30 ? ProjectScanVerdict.Good : left >= 14 ? ProjectScanVerdict.NeedsWork : ProjectScanVerdict.Poor;
                break;
            default:
                var score = Math.Clamp((kind switch
                {
                    ProjectScanKind.Performance => profile.Performance,
                    ProjectScanKind.Seo => profile.Seo ?? 83,
                    _ => profile.Accessibility
                }) + drift, 0, 100);
                scan.Score = score;
                scan.Verdict = score >= 90 ? ProjectScanVerdict.Good : score >= 50 ? ProjectScanVerdict.NeedsWork : ProjectScanVerdict.Poor;
                break;
        }

        if (kind is ProjectScanKind.Performance or ProjectScanKind.Accessibility)
        {
            scan.Pages = Pages(project, kind, scan.Score!.Value, finishedAt);
        }

        scan.Findings = Findings(project, kind, scan, profile);
        scan.UrgentCount = scan.Findings.Count(finding => finding.Severity is ScanFindingSeverity.Urgent);
        scan.ImportantCount = scan.Findings.Count(finding => finding.Severity is ScanFindingSeverity.Important);
        scan.ImprovementCount = scan.Findings.Count(finding => finding.Severity is ScanFindingSeverity.Improvement);

        return scan;
    }

    /// <summary>The home page and the pricing page, on a phone and a computer: the score is the lowest of them.</summary>
    private static ProjectScanPageDto[] Pages(SampleProject project, ProjectScanKind kind, int lowest, DateTimeOffset at)
    {
        string[] paths = ["/en-US/", "/en-US/pricing"];
        List<ProjectScanPageDto> pages = [];
        var seed = at.UtcTicks / TimeSpan.TicksPerMinute;

        foreach (var (path, pathIndex) in paths.Select((path, index) => (path, index)))
        {
            foreach (var device in new[] { ProjectScanDevice.Phone, ProjectScanDevice.Computer })
            {
                var i = pathIndex * 2 + (int)device;
                // The home page on a phone is the slowest, which is the scan's score.
                var score = i is 0 ? lowest : Math.Min(100, lowest + (device is ProjectScanDevice.Computer ? 6 : 1) + MonitoringNoise.Between($"{project.Key}:{kind}:page", seed + i, 0, 7));
                var page = new ProjectScanPageDto { Path = path, Device = device, Score = score };

                if (kind is ProjectScanKind.Performance)
                {
                    var factor = device is ProjectScanDevice.Phone ? 1 : 0.42;
                    var lcp = (int)Math.Round((1300 + (100 - score) * 68) * factor);
                    page.LargestContentfulPaintMs = lcp;
                    page.FirstContentfulPaintMs = (int)Math.Round(lcp * 0.52);
                    page.SpeedIndexMs = (int)Math.Round(lcp * 1.08);
                    page.TotalBlockingTimeMs = (int)Math.Round((100 - score) * 11 * factor);
                    page.CumulativeLayoutShift = Math.Round(0.01 + (100 - score) / 260.0 * MonitoringNoise.Of($"{project.Key}:cls", seed + i), 2);
                    page.TimeToFirstByteMs = project.BaseMs + MonitoringNoise.Between($"{project.Key}:ttfb", seed + i, 20, 160);
                }

                pages.Add(page);
            }
        }

        return [.. pages];
    }

    /// <summary>What a scan of this kind finds at its score, most urgent first.</summary>
    private static ProjectScanFindingDto[] Findings(SampleProject project, ProjectScanKind kind, ProjectScanDto scan, ScanProfile profile)
    {
        var host = new Uri(project.Address).Host;
        var domain = string.Join('.', host.Split('.').TakeLast(2));
        var score = scan.Score ?? 0;

        (string Key, ScanFindingSeverity Severity, string[] Parameters, string[] Where)[] found = kind switch
        {
            ProjectScanKind.Security => scan.Grade switch
            {
                "A+" => [],
                "A" => [(ScanFindings.ReferrerPolicyMissing, ScanFindingSeverity.Improvement, [], [project.Address])],
                "B" =>
                [
                    (ScanFindings.CspMissing, ScanFindingSeverity.Important, [], [project.Address]),
                    (ScanFindings.CookieNotHttpOnly, ScanFindingSeverity.Important, [".AspNetCore.Culture"], [project.Address]),
                    (ScanFindings.HstsShort, ScanFindingSeverity.Improvement, ["30"], [project.Address])
                ],
                _ =>
                [
                    (ScanFindings.HangfireOpen, ScanFindingSeverity.Urgent, [], [$"{project.ServerAddress ?? project.Address}/jobs"]),
                    (ScanFindings.DevMcpOpen, ScanFindingSeverity.Urgent, [], [$"{project.ServerAddress ?? project.Address}/mcp"]),
                    (ScanFindings.CspUnsafeInline, ScanFindingSeverity.Important, [], [project.Address]),
                    (ScanFindings.ServerVersionDisclosed, ScanFindingSeverity.Improvement, ["Server: Kestrel"], [project.Address])
                ]
            },
            ProjectScanKind.Performance => score switch
            {
                >= 90 => [(ScanFindings.ImagesTooBig, ScanFindingSeverity.Improvement, ["180"], ["/en-US/"]), (ScanFindings.FilesNotCached, ScanFindingSeverity.Improvement, ["12"], ["/en-US/", "/en-US/pricing"])],
                >= 70 =>
                [
                    (ScanFindings.LcpSlow, ScanFindingSeverity.Important, ["3.4"], ["/en-US/"]),
                    (ScanFindings.RenderBlocking, ScanFindingSeverity.Important, ["640"], ["/en-US/", "/en-US/pricing"]),
                    (ScanFindings.UnusedCode, ScanFindingSeverity.Improvement, ["210"], ["/en-US/"])
                ],
                _ =>
                [
                    (ScanFindings.LcpSlow, ScanFindingSeverity.Urgent, ["4.6"], ["/en-US/"]),
                    (ScanFindings.TbtHigh, ScanFindingSeverity.Important, ["720"], ["/en-US/"]),
                    (ScanFindings.PageTooHeavy, ScanFindingSeverity.Important, ["4200"], ["/en-US/", "/en-US/pricing"]),
                    (ScanFindings.LcpImageFoundLate, ScanFindingSeverity.Improvement, [], ["/en-US/"]),
                    (ScanFindings.ThirdPartyCode, ScanFindingSeverity.Improvement, ["9"], ["/en-US/"])
                ]
            },
            ProjectScanKind.Seo => score switch
            {
                >= 90 => [(ScanFindings.HreflangMissing, ScanFindingSeverity.Improvement, [], ["/en-US/"])],
                >= 80 =>
                [
                    (ScanFindings.SocialPreviewMissing, ScanFindingSeverity.Important, ["og:image"], ["/en-US/", "/en-US/pricing"]),
                    (ScanFindings.DescriptionLength, ScanFindingSeverity.Improvement, [], ["/en-US/pricing"]),
                    (ScanFindings.HreflangMissing, ScanFindingSeverity.Improvement, [], ["/en-US/"])
                ],
                _ =>
                [
                    (ScanFindings.DuplicateTitles, ScanFindingSeverity.Important, [project.Name], ["/en-US/", "/en-US/about", "/en-US/pricing"]),
                    (ScanFindings.DescriptionMissing, ScanFindingSeverity.Important, [], ["/en-US/about"]),
                    (ScanFindings.RobotsTxtNoSitemap, ScanFindingSeverity.Improvement, [], [$"{project.Address}/robots.txt"])
                ]
            },
            ProjectScanKind.Accessibility => score switch
            {
                >= 95 => [(ScanFindings.ContrastTooLow, ScanFindingSeverity.Important, ["3"], ["/en-US/pricing"])],
                >= 80 =>
                [
                    (ScanFindings.FieldsWithoutLabel, ScanFindingSeverity.Urgent, ["1"], ["/en-US/"]),
                    (ScanFindings.ButtonsWithoutName, ScanFindingSeverity.Important, ["2"], ["/en-US/", "/en-US/pricing"]),
                    (ScanFindings.TapTargetsTooSmall, ScanFindingSeverity.Improvement, ["5"], ["/en-US/"])
                ],
                _ =>
                [
                    (ScanFindings.ContrastTooLow, ScanFindingSeverity.Urgent, ["14"], ["/en-US/", "/en-US/pricing"]),
                    (ScanFindings.ImagesWithoutDescription, ScanFindingSeverity.Important, ["8"], ["/en-US/"]),
                    (ScanFindings.LinksWithoutName, ScanFindingSeverity.Important, ["3"], ["/en-US/"]),
                    (ScanFindings.StructureUnclear, ScanFindingSeverity.Improvement, ["6"], ["/en-US/pricing"])
                ]
            },
            _ => profile.DomainDays switch
            {
                >= 30 => [],
                >= 14 => [(ScanFindings.DomainExpiresSoon, ScanFindingSeverity.Important, [domain, $"{profile.DomainDays}"], [domain])],
                _ => [(ScanFindings.CertificateExpiresSoon, ScanFindingSeverity.Urgent, [host, $"{profile.DomainDays}"], [host])]
            }
        };

        return [.. found.Select((finding, index) => new ProjectScanFindingDto
        {
            Id = MonitoringNoise.IdOf($"{scan.Id}:finding:{index}"),
            Key = finding.Key,
            Severity = finding.Severity,
            Parameters = finding.Parameters,
            Where = finding.Where
        }).OrderBy(finding => finding.Severity)];
    }
}
