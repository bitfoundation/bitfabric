using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// A project's Scans tab (bitfabric-internal/specs/scanners.md), in the Operations look of the other tabs: each kind with
/// its grade, score or days left and when and why it ran; every finding of the latest scans ranked urgent first, each with
/// what it means, why it matters, the fix, a prompt for the customer's AI agent and "This is wrong"; one prompt that fixes
/// them all; the past runs; and when bit Fabric scans, the deployment setting in the home page's words. While scans wait or
/// run, the tab looks again every few seconds until they're done.
/// </summary>
public partial class ProjectScans
{
    private const string FixEverythingKey = "$all";

    /// <summary>How often the tab looks again while a scan waits or runs, and for how long at most.</summary>
    private static readonly TimeSpan pollEvery = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan pollFor = TimeSpan.FromMinutes(5);

    [Parameter, EditorRequired] public Guid MonitoringId { get; set; }

    [AutoInject] private IProjectScansController scansController = default!;
    [AutoInject] private Clipboard clipboard = default!;

    private ProjectScansDto? scans;
    private ProjectScanDto[]? history;

    /// <summary>The run of the past runs whose findings are open, with them.</summary>
    private ProjectScanDto? openedRun;

    private ProjectScanKind? kindFilter;
    private string? copied;
    private bool isStarting;
    private bool isSavingSettings;
    private bool isPolling;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();
        await Load();
    }

    private async Task Load()
    {
        scans = await scansController.GetScans(MonitoringId, CurrentCancellationToken);
        history = await scansController.GetHistory(new() { MonitoringId = MonitoringId }, CurrentCancellationToken);

        if (scans.Pending.Length > 0)
        {
            _ = PollWhilePending();
        }
    }

    private async Task RunNow()
    {
        isStarting = true;

        try
        {
            scans = await scansController.RunNow(new() { MonitoringId = MonitoringId }, CurrentCancellationToken);
            SnackBarService.Success(Localizer[nameof(AppStrings.MonitoringScansStarted)]);
        }
        finally
        {
            isStarting = false;
        }

        _ = PollWhilePending();
    }

    /// <summary>Looks again until nothing waits or runs, so a scan's result shows without a refresh.</summary>
    private async Task PollWhilePending()
    {
        if (isPolling)
            return;

        isPolling = true;

        try
        {
            var until = DateTimeOffset.UtcNow + pollFor;

            while (scans?.Pending.Length > 0 && DateTimeOffset.UtcNow < until)
            {
                await Task.Delay(pollEvery, CurrentCancellationToken);

                var pendingBefore = scans.Pending.Length;
                scans = await scansController.GetScans(MonitoringId, CurrentCancellationToken);

                if (scans.Pending.Length < pendingBefore)
                {
                    history = await scansController.GetHistory(new() { MonitoringId = MonitoringId }, CurrentCancellationToken);
                }

                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // The tab was left.
        }
        catch (Exception exp)
        {
            ExceptionHandler.Handle(exp);
        }
        finally
        {
            isPolling = false;
        }
    }

    private async Task SaveSettings(Action<ProjectScanSettingsDto> change)
    {
        if (scans is null)
            return;

        isSavingSettings = true;

        try
        {
            change(scans.Settings);
            scans.Settings = await scansController.SaveSettings(scans.Settings, CurrentCancellationToken);
            SnackBarService.Success(Localizer[nameof(AppStrings.MonitoringSaved)]);
        }
        catch
        {
            // Refused, the tab goes back to what the server holds.
            scans.Settings = await scansController.GetSettings(MonitoringId, CurrentCancellationToken);
            throw;
        }
        finally
        {
            isSavingSettings = false;
        }
    }

    private async Task ReportWrong(ProjectScanFindingDto finding)
    {
        await scansController.ReportWrong(new() { FindingId = finding.Id }, CurrentCancellationToken);

        finding.IsReportedWrong = true;
        SnackBarService.Success(Localizer[nameof(AppStrings.MonitoringScanReportedWrong)]);
    }

    private async Task CopyPrompt(ProjectScanKind kind, ProjectScanFindingDto finding)
    {
        await clipboard.WriteText(ScanPrompts.For(scans?.Address, kind, finding));
        copied = finding.Id.ToString();
        SnackBarService.Success(Localizer[nameof(AppStrings.MonitoringScanPromptCopied)]);
    }

    private async Task CopyFixEverything()
    {
        await clipboard.WriteText(ScanPrompts.ForAll(scans?.Address, AllFindings()));
        copied = FixEverythingKey;
        SnackBarService.Success(Localizer[nameof(AppStrings.MonitoringScanPromptCopied)]);
    }

    private async Task ToggleRun(ProjectScanDto run)
    {
        openedRun = openedRun?.Id == run.Id ? null : await scansController.GetScan(run.Id, CurrentCancellationToken);
    }

    private ProjectScanDto? LatestOf(ProjectScanKind kind) => scans?.Latest.FirstOrDefault(scan => scan.Kind == kind);

    private ProjectScanDto? PendingOf(ProjectScanKind kind) => scans?.Pending.FirstOrDefault(scan => scan.Kind == kind);

    /// <summary>Every finding of the latest scans, most urgent first, then in the order the kinds show.</summary>
    private IEnumerable<(ProjectScanKind Kind, ProjectScanFindingDto Finding)> AllFindings()
        => (scans?.Latest ?? [])
            .Where(scan => scan.Outcome is ProjectScanOutcome.Completed)
            .SelectMany(scan => scan.Findings.Select(finding => (scan.Kind, finding)))
            .OrderBy(item => item.finding.Severity)
            .ThenBy(item => Array.IndexOf(ScanFindings.AvailableKinds, item.Kind));

    private static IEnumerable<(ScanFindingSeverity Severity, int Count)> Counts(ProjectScanDto scan)
        => new[] { (ScanFindingSeverity.Urgent, scan.UrgentCount), (ScanFindingSeverity.Important, scan.ImportantCount), (ScanFindingSeverity.Improvement, scan.ImprovementCount) }
           .Where(item => item.Item2 > 0);

    private string ValueHelp(ProjectScanKind kind) => kind switch
    {
        ProjectScanKind.Security => Localizer[nameof(AppStrings.MonitoringScanGradeHelp)],
        ProjectScanKind.DomainAndCertificate => Localizer[nameof(AppStrings.MonitoringScanDaysLeftHelp)],
        ProjectScanKind.Performance or ProjectScanKind.Accessibility => Localizer[nameof(AppStrings.MonitoringScanLowestScoreHelp)],
        _ => Localizer[nameof(AppStrings.MonitoringScanScoreHelp)]
    };

    /// <summary>
    /// One of the Core Web Vitals the performance card shows, with Google's thresholds: good up to the first, needs
    /// improvement up to the second, poor beyond. The abbreviations are the metrics' own names in every language.
    /// </summary>
    private sealed record Vital(string Abbreviation, string FullName, string NameKey, Func<ProjectScanPageDto, double?> Of, double GoodUpTo, double PoorOver, Func<ProjectScans, double, string> Format);

    private static readonly Vital[] vitals =
    [
        new("LCP", "Largest Contentful Paint", nameof(AppStrings.MonitoringScanLcpName), page => page.LargestContentfulPaintMs, 2500, 4000, (tab, ms) => tab.Seconds(ms)),
        new("CLS", "Cumulative Layout Shift", nameof(AppStrings.MonitoringScanClsName), page => page.CumulativeLayoutShift, 0.1, 0.25, (_, shift) => shift.ToString("0.00", CultureInfo.CurrentCulture)),
        new("TBT", "Total Blocking Time", nameof(AppStrings.MonitoringScanTbtName), page => page.TotalBlockingTimeMs, 200, 600, (tab, ms) => tab.Localizer[nameof(AppStrings.AppHomeMs), Math.Round(ms).ToString("0", CultureInfo.CurrentCulture)]),
        new("FCP", "First Contentful Paint", nameof(AppStrings.MonitoringScanFcpName), page => page.FirstContentfulPaintMs, 1800, 3000, (tab, ms) => tab.Seconds(ms))
    ];

    private string Seconds(double milliseconds) => Localizer[nameof(AppStrings.MonitoringDurationSeconds), (milliseconds / 1000).ToString("0.0", CultureInfo.CurrentCulture)];

    private static string VitalTone(double? value, Vital vital) => value switch
    {
        null => "tone-nodata",
        _ when value <= vital.GoodUpTo => "tone-ok",
        _ when value <= vital.PoorOver => "tone-degraded",
        _ => "tone-down"
    };

    /// <summary>What the tone says, in words, for those who don't see colors.</summary>
    private string? VitalVerdict(double? value, Vital vital) => value switch
    {
        null => null,
        _ when value <= vital.GoodUpTo => Localizer[nameof(AppStrings.MonitoringScanVitalGood)].Value,
        _ when value <= vital.PoorOver => Localizer[nameof(AppStrings.MonitoringScanVitalNeedsImprovement)].Value,
        _ => Localizer[nameof(AppStrings.MonitoringScanVitalPoor)].Value
    };

    /// <summary>Lighthouse's own bands: 90 and up is good, under 50 poor.</summary>
    private static string ScoreTone(int? score) => score switch
    {
        null => "tone-nodata",
        >= 90 => "tone-ok",
        >= 50 => "tone-degraded",
        _ => "tone-down"
    };

    private string DeviceName(ProjectScanDevice device) => device is ProjectScanDevice.Phone
        ? Localizer[nameof(AppStrings.MonitoringScanPhone)]
        : Localizer[nameof(AppStrings.MonitoringScanComputer)];

    /// <summary>Minutes until "Run scans now" may be pressed again; null when it may now.</summary>
    private int? RunAgainInMinutes => scans?.CanRunAgainAt is { } at && at > DateTimeOffset.UtcNow ? (int)Math.Ceiling((at - DateTimeOffset.UtcNow).TotalMinutes) : null;

    private string LocalTime(DateTimeOffset at, string format) => TimeZoneService.ToLocalTime(at).ToString(format, CultureInfo.CurrentUICulture);
}
