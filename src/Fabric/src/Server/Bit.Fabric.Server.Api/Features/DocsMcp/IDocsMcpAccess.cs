using Bit.Fabric.Server.Api.Features.Licensing;

namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// Decides whether /docs-mcp answers the project an agent connected for: the <c>project</c> query of its address,
/// <c>https://api.bitfabric.co/docs-mcp?project=&lt;projectId&gt;</c>. Asked on every tool call and when a session starts,
/// from the request's own scope, so an implementation may read the database: <see cref="DocsMcpLicenseAccess"/> asks the
/// project's license.
/// </summary>
public interface IDocsMcpAccess
{
    /// <param name="projectId">The project query value, or null when the address names none.</param>
    /// <param name="cancellationToken">Ends the decision.</param>
    ValueTask<DocsMcpAccessDecision> Decide(string? projectId, CancellationToken cancellationToken);
}

/// <param name="Refusal">Null when the tools answer; otherwise what the agent is told instead, in words it can pass on to the user.</param>
public sealed record DocsMcpAccessDecision(string? Refusal)
{
    public static readonly DocsMcpAccessDecision Allowed = new((string?)null);

    public bool IsAllowed => Refusal is null;
}

/// <summary>
/// Answers a project while its trial or license lasts. The address in a project's MCP settings carries its id from bit.json
/// as <c>project=</c>, which bit new writes; bit license gives an older project its id.
/// </summary>
public sealed class DocsMcpLicenseAccess(IProjectLicenseStatus licenses) : IDocsMcpAccess
{
    public async ValueTask<DocsMcpAccessDecision> Decide(string? projectId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return new("This documentation server answers bit Fabric projects, and its address names none. Add the project's id from bit.json " +
                       "to the address in the project's MCP settings as &project=<projectId>, or run `bit license` in the project: it gives the " +
                       "project its id and the link that gets it a license, free.");

        if (await licenses.IsLicensed(projectId, cancellationToken))
            return DocsMcpAccessDecision.Allowed;

        return new("This project's trial or license has ended, or bit Fabric doesn't know its id. Run `bit license` in the project: it " +
                   "prints the link that gets the project a license, free, and the documentation answers again once it has one.");
    }
}
