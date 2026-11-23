using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Shared.Features.Monitoring;

/// <summary>A project's status page as its team runs it (bitfabric-internal/specs/monitoring.md, Status page), through the current tenant.</summary>
[Route("api/v1/[controller]/[action]/"), AuthorizedApi]
public interface IStatusPagesController : IAppController
{
    /// <summary>The page's settings; a project that has none gets its first, unpublished, with a component per kind of part.</summary>
    [HttpGet("{projectId}")]
    Task<StatusPageSettingsDto> GetStatusPage(Guid projectId, CancellationToken cancellationToken);

    [HttpPost]
    Task<StatusPageSettingsDto> SaveStatusPage(StatusPageSettingsDto settings, CancellationToken cancellationToken);

    /// <summary>Asks Cloudflare for SaaS for the host name and its certificate; the customer then adds a CNAME record.</summary>
    [HttpPost]
    Task<StatusPageSettingsDto> ConnectCustomHostname(ConnectCustomHostnameRequestDto request, CancellationToken cancellationToken);

    /// <summary>Reads the host name's state again from Cloudflare.</summary>
    [HttpPost("{projectId}")]
    Task<StatusPageSettingsDto> RefreshCustomHostname(Guid projectId, CancellationToken cancellationToken);

    [HttpPost("{projectId}")]
    Task<StatusPageSettingsDto> DisconnectCustomHostname(Guid projectId, CancellationToken cancellationToken);

    /// <summary>A new incident or announcement, or an update to one, in the languages the team wrote it in.</summary>
    [HttpPost]
    Task<ProjectIncidentDto> Post(StatusPostRequestDto request, CancellationToken cancellationToken);

    /// <summary>An AI draft of a text in other languages, for the team to review; never posted as it is.</summary>
    [HttpPost]
    Task<Dictionary<string, string>> DraftTranslations(DraftTranslationsRequestDto request, CancellationToken cancellationToken);
}

/// <summary>The public side of status pages: anonymous, and readable from the customer's own host name.</summary>
[Route("api/v1/[controller]/[action]/")]
public interface IPublicStatusController : IAppController
{
    /// <summary>A published page by its slug, in <paramref name="language"/>, a culture name, or in its main language.</summary>
    [HttpGet("{slug}{?language}")]
    Task<PublicStatusDto> Get(string slug, string? language, CancellationToken cancellationToken);

    /// <summary>The slug of the page a customer's host name serves, for Server.Web to route that host to it; null for none.</summary>
    [HttpGet("{hostname}")]
    Task<string?> SlugOf(string hostname, CancellationToken cancellationToken);

    [HttpPost]
    Task Follow(FollowStatusPageRequestDto request, CancellationToken cancellationToken);

    /// <summary>Stops following, by the browser's push endpoint.</summary>
    [HttpPost]
    Task Unfollow(FollowStatusPageRequestDto request, CancellationToken cancellationToken);
}
