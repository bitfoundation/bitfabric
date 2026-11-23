using Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;

/// <summary>
/// bitfabric.co/for: the eight agents a project is ready for, in a table of what each reads on its own (each cell from the
/// agent's own documentation), then a card for each agent's page and for the two models' pages. The table is built from the
/// agents' own classes, so a fact corrected there is corrected here. Agents that only read AGENTS.md get no page: the hub's
/// first question says how they start (agents.md lists them).
/// </summary>
public static class ForHub
{
    /// <summary>The agents, in the order the home page names them, then Junie and Devin Desktop.</summary>
    public static AgentPage[] Agents(IStringLocalizer<AppStrings> l) =>
    [
        CopilotAgent.Build(l),
        CodexAgent.Build(l),
        ClaudeCodeAgent.Build(l),
        GeminiCliAgent.Build(l),
        AntigravityAgent.Build(l),
        CursorAgent.Build(l),
        JunieAgent.Build(l),
        DevinAgent.Build(l)
    ];

    /// <summary>The models' pages: one on the visitor's computer, and the five from China.</summary>
    public static AgentPage[] Models(IStringLocalizer<AppStrings> l) =>
    [
        LocalModels.Build(l),
        ChineseModels.Build(l)
    ];

    /// <summary>Every page under the hub, in the order its cards show them.</summary>
    public static AgentPage[] Pages(IStringLocalizer<AppStrings> l) => [.. Agents(l), .. Models(l)];

    public static AgentPage Build(IStringLocalizer<AppStrings> l) => new(PageUrls.For, l[nameof(AppStrings.ForHubName)])
    {
        PageTitle = l[nameof(AppStrings.ForHubPageTitle)],
        Description = l[nameof(AppStrings.ForHubDescription)],
        Title = l[nameof(AppStrings.ForHubTitle)],
        Lead = l[nameof(AppStrings.ForHubLead)],
        Questions =
        [
            new(l[nameof(AppStrings.ForQuestionNotListed)], l[nameof(AppStrings.ForAnswerNotListed)], ["AGENTSMD"]),
            new(l[nameof(AppStrings.ForQuestionWhich)], l[nameof(AppStrings.ForAnswerWhich)], []),
            ForShared.Switch(l),
            ForShared.Sees(l)
        ]
    };

    /// <summary>A row of the hub's table: an agent, then what it reads in each column, with the sources each cell rests on.</summary>
    public sealed record MatrixRow(AgentPage Agent, MatrixCell Instructions, MatrixCell Skills, MatrixCell Call, MatrixCell Tools);

    /// <summary>A cell: the files it names, or a line where it names none, and its sources.</summary>
    public sealed record MatrixCell(string[] Files, string? Text, string[] Sources);

    public static MatrixRow[] Matrix(IStringLocalizer<AppStrings> l, AgentPage[] agents) =>
    [
        .. agents.Select(agent => new MatrixRow(
            agent,
            CellOf(agent, AgentLineKind.Instructions),
            CellOf(agent, AgentLineKind.Skills),
            agent.SkillCall is { } call
                ? new([ForShared.Call(call, ForShared.FirstSkill)], null, agent.SkillLine.Sources)
                : new([], l[nameof(AppStrings.ForCallByDescription)], agent.SkillLine.Sources),
            agent.Lines.Any(line => line.Kind is AgentLineKind.Tools) || agent.ToolsNote is null
                ? CellOf(agent, AgentLineKind.Tools)
                : new([], agent.ToolsNote.Text, agent.ToolsNote.Sources)))
    ];

    /// <summary>
    /// Every source the hub cites, in the order it first cites them: the table's, row by row and cell by cell, then the
    /// questions'. The page numbers them so.
    /// </summary>
    public static string[] SourceIds(AgentPage hub, MatrixRow[] matrix) =>
    [
        .. matrix.SelectMany(row => new[] { row.Instructions, row.Skills, row.Call, row.Tools }).SelectMany(cell => cell.Sources)
            .Concat(hub.Questions.SelectMany(question => question.Sources))
            .Distinct()
    ];

    private static MatrixCell CellOf(AgentPage agent, AgentLineKind kind)
    {
        var lines = agent.Lines.Where(line => line.Kind == kind).ToArray();
        return new([.. lines.Select(line => line.Label)], null, [.. lines.SelectMany(line => line.Sources).Distinct()]);
    }
}
