namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// Decides whether /docs-mcp answers the project an agent connected for: the <c>project</c> query of its address,
/// <c>https://api.bitfabric.co/docs-mcp?project=&lt;projectId&gt;</c>. Asked on every tool call and when a session starts,
/// from the request's own scope, so an implementation may read the database. <see cref="DocsMcpOpenAccess"/>, the one
/// registered, answers everyone; a check of the project's license replaces it in the service collection.
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

/// <summary>Answers every project, and a connection that names none.</summary>
public sealed class DocsMcpOpenAccess : IDocsMcpAccess
{
    public ValueTask<DocsMcpAccessDecision> Decide(string? projectId, CancellationToken cancellationToken) => ValueTask.FromResult(DocsMcpAccessDecision.Allowed);
}
