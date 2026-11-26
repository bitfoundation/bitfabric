[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Boilerplate.Workbench.Infrastructure.Services.WorkbenchBoardCatalog))]

namespace Boilerplate.Workbench.Infrastructure.Services;

/// <summary>
/// A board: a page an agent builds in the Workbench when something is quicker to see than to read, such as how the project
/// ships or options compared side by side (the boards skill, .github/agents/boards.agent.md). It opens at /boards/{Id} and
/// is listed on /boards, newest first. <paramref name="Tone"/> colors its card: primary, secondary, tertiary, info,
/// success or warning.
/// </summary>
public sealed record WorkbenchBoard(string Id, string TitleKey, string SummaryKey, string IconName, Type ComponentType, DateOnly Updated, string Tone = "primary");

/// <summary>
/// The boards the Workbench shows. A new board is added here, in <see cref="Build"/>, and shows up while the Workbench runs,
/// as hot reload has the list built again (<see cref="ClearCache"/>).
/// </summary>
public static class WorkbenchBoardCatalog
{
    private static IReadOnlyList<WorkbenchBoard>? boards;

    public static IReadOnlyList<WorkbenchBoard> Boards => boards ??= [.. Build().OrderByDescending(board => board.Updated)];

    public static WorkbenchBoard? Find(string? id) => Boards.FirstOrDefault(board => string.Equals(board.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Called by hot reload after it applies an edit (<see cref="System.Reflection.Metadata.MetadataUpdateHandlerAttribute"/>).</summary>
    public static void ClearCache(Type[]? updatedTypes) => boards = null;

    // Two examples come with the project, to show what a board can be; the agents' own boards go next to them. Boards of
    // the same day keep the order they are listed in.
    private static IEnumerable<WorkbenchBoard> Build() =>
    [
        new("payments", nameof(WorkbenchStrings.PaymentsBoardTitle), nameof(WorkbenchStrings.PaymentsBoardSummary), BitIconName.PaymentCard, typeof(PaymentsBoard), new(2026, 11, 25), "success"),
        new("deploy", nameof(WorkbenchStrings.DeployBoardTitle), nameof(WorkbenchStrings.DeployBoardSummary), BitIconName.Rocket, typeof(DeployBoard), new(2026, 11, 25), "primary")
    ];
}
