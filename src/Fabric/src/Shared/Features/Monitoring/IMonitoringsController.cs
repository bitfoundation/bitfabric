using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Shared.Features.Monitoring;

/// <summary>
/// The current tenant's monitored projects: adding one through its own app's approval, and reading how each is doing
/// (bitfabric-internal/specs/monitoring.md). Every action needs a selected tenant, since a project belongs to a team, never
/// to the fallback tenant.
/// </summary>
[Route("api/v1/[controller]/[action]/"), AuthorizedApi]
public interface IMonitoringsController : IAppController
{
    /// <summary>
    /// A project's whole history as a file, at <c>{ExportHistoryUri}/{monitoringId}/csv</c> or <c>.../json</c>
    /// (bitfabric-internal/specs/monitoring.md, Health history is the customer's): every period of each part, incident with
    /// its updates, deployment and restart, and maintenance window, in the project's time zone with UTC alongside. It
    /// downloads a file, so it is called with the HttpClient directly rather than through the generated proxy, which speaks
    /// json only.
    /// </summary>
    public const string ExportHistoryUri = "api/v1/Monitorings/ExportHistory";

    /// <summary>
    /// Every project of the tenant, failing ones first, with its last 30 days drawn in <paramref name="timeZone"/>: the
    /// viewer's IANA or Windows zone id, UTC when it is missing or unknown.
    /// </summary>
    [HttpGet("{?timeZone}")]
    Task<List<MonitoringDto>> GetMonitorings(string? timeZone, CancellationToken cancellationToken);

    [HttpGet("{id}")]
    Task<MonitoringDetailsDto> GetMonitoring(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The prompt the project's Troubleshoot button hands to the customer's own AI, in English: why the environment needs
    /// reconnecting and what to check, or what fails in the app and how to look into it (bitfabric-internal/specs/monitoring.md,
    /// Monitoring keeps watching when the approval breaks).
    /// </summary>
    [HttpGet("{id}")]
    Task<TroubleshootPromptDto> GetTroubleshootPrompt(Guid id, CancellationToken cancellationToken);

    /// <summary>The project's application map: every part its last report names, with how it's doing now and over 24 hours.</summary>
    [HttpGet("{id}")]
    Task<ProjectMapDto> GetMap(Guid id, CancellationToken cancellationToken);

    /// <summary>How long each part took over a span of time: every round when zoomed in, the 15 minute rollups beyond.</summary>
    [HttpPost]
    Task<ProjectPerformanceDto> GetPerformance(ProjectPerformanceRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Reads what the app at the address says about itself: where its server is (<c>/.well-known/bit-app.json</c>), its
    /// name (its web app manifest) and whether that server serves a health report bit Fabric can read. Reads only what any
    /// visitor can.
    /// </summary>
    [HttpPost]
    Task<AddressProbeResultDto> ProbeAddress(ProbeAddressRequestDto request, CancellationToken cancellationToken);

    /// <summary>Prepares the approval in the customer's app and returns the address of its page.</summary>
    [HttpPost]
    Task<StartConnectResponseDto> StartConnect(StartConnectRequestDto request, CancellationToken cancellationToken);

    /// <summary>Takes the customer app's answer, and on approval adds the project (or reconnects it) and reads it once.</summary>
    [HttpPost]
    Task<MonitoringDto> CompleteConnect(CompleteConnectRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Picks the bitplatform Network's locations that time the project's home page every minute, besides bit Fabric's own
    /// server in Germany, which reads its health report (bitfabric-internal/specs/monitoring.md, Germany, plus the
    /// locations the customer picks).
    /// </summary>
    [HttpPost]
    Task<MonitoringDto> SetLocations(SetProjectLocationsRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets bit Fabric's approval in the app and stops reading it, keeping its history, until someone of the team
    /// reconnects it (bitfabric-internal/specs/monitoring.md, Settings). bit Fabric's sign-in in the app ends when it
    /// expires, or at once from the app's own Settings, Sessions.
    /// </summary>
    [HttpPost("{id}")]
    Task<MonitoringDto> Disconnect(Guid id, CancellationToken cancellationToken);

    /// <summary>Stops watching the project and deletes its history.</summary>
    [HttpDelete("{id}")]
    Task Delete(Guid id, CancellationToken cancellationToken);
}
