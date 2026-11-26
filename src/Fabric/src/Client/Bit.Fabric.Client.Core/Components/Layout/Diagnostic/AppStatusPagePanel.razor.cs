namespace Bit.Fabric.Client.Core.Components.Layout.Diagnostic;

/// <summary>
/// The Status tab in bit BevTools (<see cref="AppBevTools.StatusPanelId"/>): what the app's status page says now, and what
/// <see cref="StatusPageNotice"/> shows the user for it. It reads the summary the moment the tab opens, since BevTools
/// is opened on purpose, long after the app started. A plain component of the app that names no Bit.BevTools type, so it
/// may be public here while Bit.BevTools.wasm is lazily loaded (See <see cref="AppBevTools"/>).
/// </summary>
public partial class AppStatusPagePanel
{
    [AutoInject] private StatusPageService statusPageService = default!;

    private StatusPageRead? read;
    private bool reading;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        await Read();
    }

    private async Task Read()
    {
        if (statusPageService.IsEnabled is false)
            return;

        reading = true;

        try
        {
            read = await statusPageService.Read(CurrentCancellationToken) ?? read;
        }
        finally
        {
            reading = false;
        }
    }

    private StatusPageSummary? Summary => read?.Summary;

    /// <summary>What the app's notice shows for this summary now, null when it shows nothing.</summary>
    private StatusNotice? Notice => StatusNotice.From(Summary, TimeProvider.GetUtcNow());

    private StatusPageSummaryComponent[] Components => [.. (Summary?.Components ?? []).Where(component => component is not null && component.Group is not true)];

    private StatusPageSummaryIncident[] Incidents => [.. (Summary?.Incidents ?? []).Where(incident => incident is not null)];

    private StatusPageSummaryIncident[] Maintenances => [.. (Summary?.ScheduledMaintenances ?? []).Where(maintenance => maintenance is not null)];

    private string Headline => (statusPageService.IsEnabled, read) switch
    {
        (false, _) => "No status page",
        (true, null) => "Reading the status page…",
        (true, { Error: not null }) => "The status page didn't answer",
        _ => Summary?.Status?.Description ?? IndicatorText(Summary?.Status?.Indicator)
    };

    private string? Subline => read is null
        ? null
        : string.Join(" · ", new[] { Summary?.Page?.Name, $"read at {Time(read.At, "T")}" }.Where(part => string.IsNullOrWhiteSpace(part) is false));

    private string? NoticeText => Notice switch
    {
        null => "Nothing: the app's users see no notice.",
        { Kind: StatusNoticeKind.Maintenance, InProgress: true } => $"Maintenance under way: {Notice.Title ?? "untitled"}",
        { Kind: StatusNoticeKind.Maintenance } => $"Maintenance coming: {Notice.Title ?? "untitled"}",
        { Kind: StatusNoticeKind.Outage } => $"Outage: {Notice.Title ?? "untitled"}",
        _ => $"Partial outage: {Notice.Title ?? "untitled"}"
    };

    private BitColor IndicatorColor => (statusPageService.IsEnabled, read) switch
    {
        (false, _) or (true, null) => BitColor.SecondaryForeground,
        (true, { Error: not null }) => BitColor.Error,
        _ => ColorOf(Summary?.Status?.Indicator)
    };

    private string IndicatorIcon => (statusPageService.IsEnabled, read) switch
    {
        (false, _) => BitIconName.Info,
        (true, null) => BitIconName.Sync,
        (true, { Error: not null }) => BitIconName.PlugDisconnected,
        _ => Summary?.Status?.Indicator?.ToLowerInvariant() switch
        {
            "none" => BitIconName.CompletedSolid,
            "maintenance" => BitIconName.Repair,
            "minor" => BitIconName.Warning,
            _ => BitIconName.ErrorBadge
        }
    };

    private static BitColor ColorOf(string? indicator) => indicator?.ToLowerInvariant() switch
    {
        "none" => BitColor.Success,
        "minor" => BitColor.Warning,
        "major" or "critical" => BitColor.Error,
        "maintenance" => BitColor.Info,
        _ => BitColor.SecondaryForeground
    };

    private static string IndicatorText(string? indicator) => indicator?.ToLowerInvariant() switch
    {
        "none" => "All systems operational",
        "minor" => "Minor trouble",
        "major" => "Major outage",
        "critical" => "Critical outage",
        "maintenance" => "Under maintenance",
        _ => "No overall status given"
    };

    private static (BitColor Color, string Text) StateOf(string? componentStatus) => componentStatus?.ToLowerInvariant() switch
    {
        "operational" => (BitColor.Success, "Operational"),
        "degraded_performance" => (BitColor.Warning, "Degraded"),
        "partial_outage" => (BitColor.Warning, "Partial outage"),
        "major_outage" => (BitColor.Error, "Major outage"),
        "under_maintenance" => (BitColor.Info, "Maintenance"),
        _ => (BitColor.SecondaryForeground, componentStatus ?? "Unknown")
    };

    private static StatusPageSummaryUpdate? Latest(StatusPageSummaryIncident incident)
        => (incident.IncidentUpdates ?? []).Where(update => update is not null).OrderByDescending(update => update.DisplayAt ?? update.CreatedAt).FirstOrDefault();

    private string Time(DateTimeOffset at, string format = "g")
        => TimeZoneService.ToLocalTime(at).ToString(format, CultureInfo.CurrentCulture);

    private string? Time(DateTimeOffset? at) => at is { } value ? Time(value) : null;
}
