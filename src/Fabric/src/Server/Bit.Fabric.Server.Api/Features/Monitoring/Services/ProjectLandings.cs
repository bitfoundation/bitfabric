using System.Collections.Concurrent;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// The page each project's front door lands on, such as <c>/en-US/</c> for the template's <c>/</c>, as the last round found
/// it. Later rounds, here and on the bitplatform Network, ask for that page straight away, so a round times one request
/// (bitfabric-internal/specs/monitoring.md, Response time); a round that is sent somewhere else again learns the new page.
/// It lives in memory: after a restart, the first round of each project finds it again.
/// </summary>
public class ProjectLandings
{
    private readonly ConcurrentDictionary<Guid, (string FrontDoor, string Landing)> landings = new();

    /// <summary>What to time for the project: the page its front door lands on once a round found it, the front door until then.</summary>
    public string AddressOf(Project project)
    {
        var frontDoor = ProjectProber.FrontDoorOf(project);

        // A project whose address changed starts again from its new front door.
        return landings.TryGetValue(project.Id, out var known) && known.FrontDoor == frontDoor ? known.Landing : frontDoor;
    }

    public void Learn(Project project, Uri landing) => landings[project.Id] = (ProjectProber.FrontDoorOf(project), landing.GetLeftPart(UriPartial.Path));
}
