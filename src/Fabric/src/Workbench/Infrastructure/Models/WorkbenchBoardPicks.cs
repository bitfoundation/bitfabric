using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Bit.Fabric.Workbench.Infrastructure.Models;

/// <summary>
/// What the reader picked on a board, as its file holds it (WorkbenchBoardFiles): the board's store as it was last saved, and
/// the picks the reader submitted as final, which stay as they were sent while the reader goes on changing the board.
/// </summary>
/// <param name="Board">The board's id, as in /boards/{id}.</param>
/// <param name="SavedAt">When the board's store was last saved.</param>
/// <param name="State">The board's store as it was last saved, by the names of its fields; null until it is first saved.</param>
/// <param name="Submitted">The picks the reader submitted last; null until they submit.</param>
/// <param name="StoreVersion">The store's own version (its [BlexStore(Version = n)]), 0 when it has none.</param>
public sealed record WorkbenchBoardPicks([property: JsonPropertyOrder(-2)] string Board,
                                         DateTimeOffset SavedAt,
                                         JsonObject? State,
                                         WorkbenchBoardSubmission? Submitted,
                                         int StoreVersion = 0)
{
    /// <summary>
    /// Whether the reader submitted the board as it is now, with nothing changed since; written into the file for the agent. Only
    /// what a save holds is compared, as the submitted state also holds what the store keeps on the screen alone.
    /// </summary>
    [JsonPropertyOrder(-1)]
    public bool IsSubmitted => Submitted is not null
                               && (State is null || Submitted.State is { } submitted && State.All(pair => JsonNode.DeepEquals(pair.Value, submitted[pair.Key])));
}

/// <summary>Picks the reader submitted: when, by whom, and the board's store as it was then.</summary>
public sealed record WorkbenchBoardSubmission(DateTimeOffset At, string By, JsonObject? State);
