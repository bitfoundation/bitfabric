[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Bit.Fabric.Workbench.Infrastructure.Services.WorkbenchBoardCatalog))]

namespace Bit.Fabric.Workbench.Infrastructure.Services;

/// <summary>
/// A board: a page an agent builds in the Workbench when something is quicker to see than to read, such as how the project
/// ships or options compared side by side (the boards skill, .github/agents/boards.agent.md). It opens at /boards/{Id} and
/// is listed on /boards, newest first, under its <paramref name="Kind"/>. <paramref name="Tone"/> colors its card: primary,
/// secondary, tertiary, info, success or warning.
/// </summary>
public sealed record WorkbenchBoard(string Id, string TitleKey, string SummaryKey, string IconName, Type ComponentType, DateOnly Updated, string Tone = "primary", WorkbenchBoardKind Kind = WorkbenchBoardKind.HowItWorks);

/// <summary>What a board shows, which the boards are grouped by, in this order.</summary>
public enum WorkbenchBoardKind
{
    /// <summary>How parts connect: an architecture, a flow, a deployment, how a part of the project works.</summary>
    HowItWorks,

    /// <summary>Options weighed against each other: providers, libraries, approaches, plans.</summary>
    Comparison,

    /// <summary>A plan with its steps.</summary>
    Plan,

    /// <summary>What was found or done.</summary>
    Report
}

/// <summary>
/// The boards the Workbench shows. A new board is added here, in <see cref="Build"/>, and shows up while the Workbench runs,
/// as hot reload has the list built again (<see cref="ClearCache"/>).
/// </summary>
public static class WorkbenchBoardCatalog
{
    private static IReadOnlyList<WorkbenchBoard>? boards;

    public static IReadOnlyList<WorkbenchBoard> Boards => boards ??= [.. Build().OrderByDescending(board => board.Updated)];

    /// <summary>The boards by kind, each kind's newest first, leaving out the kinds no board has.</summary>
    public static IEnumerable<(WorkbenchBoardKind Kind, List<WorkbenchBoard> Boards)> Groups =>
        Boards.GroupBy(board => board.Kind).OrderBy(group => group.Key).Select(group => (group.Key, group.ToList()));

    /// <summary>The name of a kind's group.</summary>
    public static string TitleKeyOf(WorkbenchBoardKind kind) => $"BoardKind{kind}";

    /// <summary>
    /// What the Boards page and the home page suggest asking for under "Try asking": one prompt for each kind of board the
    /// page's lead names (a flow drawn, options side by side, numbers to try), none of them for a board <see cref="Build"/>
    /// already has.
    /// </summary>
    public static readonly string[] PromptKeys = [nameof(WorkbenchStrings.BoardsPromptFlow), nameof(WorkbenchStrings.BoardsPromptCompare), nameof(WorkbenchStrings.BoardsPromptNumbers)];

    public static WorkbenchBoard? Find(string? id) => Boards.FirstOrDefault(board => string.Equals(board.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Called by hot reload after it applies an edit (<see cref="System.Reflection.Metadata.MetadataUpdateHandlerAttribute"/>).</summary>
    public static void ClearCache(Type[]? updatedTypes) => boards = null;

    // None yet: an agent adds its boards here.
    private static IEnumerable<WorkbenchBoard> Build() => [];
}
