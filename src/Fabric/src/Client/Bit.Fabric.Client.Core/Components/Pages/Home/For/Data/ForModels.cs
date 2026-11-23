using Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;

/// <summary>What a line of an agent's page is about: the column of the hub's table it fills.</summary>
public enum AgentLineKind
{
    /// <summary>The project's instructions: AGENTS.md, or the agent's own file that brings it in.</summary>
    Instructions,

    /// <summary>The project's skills, or the agent's own form of them, such as Copilot's custom agents or Gemini CLI's commands.</summary>
    Skills,

    /// <summary>The project's tools (MCP servers).</summary>
    Tools,

    /// <summary>On a model's page, an agent the model works in, and how.</summary>
    Agent
}

/// <summary>
/// One line of what an agent finds: on an agent's page, a file or folder every project ships that the agent reads on its own,
/// with what it gives the agent and the agent's own documentation that says it reads it; on a model's page, an agent the
/// model works in, how, and the command to type where there is one.
/// </summary>
public sealed record AgentLine(AgentLineKind Kind, string Label, string Text, string[] Sources, string? Code = null);

/// <summary>A step of starting: its title, a line, its sources if it states a fact about another product, and what to type where there is something.</summary>
public sealed record AgentStep(string Title, string Text, string[] Sources, string? Code = null);

/// <summary>What the agent or the model costs: the amount, a line on it, and the page its price is read from.</summary>
public sealed record AgentCost(string? Value, string Text, string[] Sources);

/// <summary>A model on the models' page: its name, who makes it, how it is sold, with its price where its maker lists one.</summary>
public sealed record AgentModel(string Name, string Maker, string? Value, string Text, string[] Sources);

/// <summary>
/// Everything one page of /for says, built from the facts of its own class (See CopilotAgent and the others next to it): correct a fact there, and the page, the hub's table and card, the JSON-LD and
/// the page's list of sources follow. ForPage renders every page alike, so a page differs from another only by what is here.
/// </summary>
public sealed record AgentPage(string Url, string Name)
{
    /// <summary>The page's title in the browser and in search results.</summary>
    public string PageTitle { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>The page's one h1.</summary>
    public string Title { get; init; } = string.Empty;

    public string Lead { get; init; } = string.Empty;

    /// <summary>The line on the hub's card that leads here.</summary>
    public string CardLine { get; init; } = string.Empty;

    public string CardIcon { get; init; } = BitIconName.Robot;

    /// <summary>The agent's own address, for the JSON-LD; null for what isn't one product, such as a model on your computer.</summary>
    public string? ProductUrl { get; init; }

    /// <summary>What the agent does well, said right under the title.</summary>
    public CompareFact DoesWell { get; init; } = new(string.Empty, []);

    /// <summary>The title of the band of lines: what the agent finds in the project, or where a model plugs in.</summary>
    public string LinesTitle { get; init; } = string.Empty;

    public string LinesIntro { get; init; } = string.Empty;

    public AgentLine[] Lines { get; init; } = [];

    /// <summary>Where the agent takes its tools when the project doesn't ship them for it, such as an app's own MCP manager; null when it does.</summary>
    public CompareFact? ToolsNote { get; init; }

    /// <summary>How a skill is called, as a format of its name: "/{0}" or "${0}"; null where the agent picks one by its description.</summary>
    public string? SkillCall { get; init; }

    /// <summary>How the agent calls a skill, in a line, said over the skills band.</summary>
    public CompareFact SkillLine { get; init; } = new(string.Empty, []);

    /// <summary>The steps of starting after the two every page shares (the questions, then the one line), the last of them asking for the first feature.</summary>
    public AgentStep[] Steps { get; init; } = [];

    public AgentCost Cost { get; init; } = new(null, string.Empty, []);

    /// <summary>A band of its own for the models a page is about, with its title and line; null for an agent's page.</summary>
    public (string Title, string Intro, AgentModel[] Models)? Models { get; init; }

    public CompareQuestion[] Questions { get; init; } = [];

    /// <summary>
    /// Every source the page cites, in the order the page first cites them: what it does well, its lines and tools, how it
    /// calls a skill, its steps, what it costs, its models, then its questions. The page numbers them so.
    /// </summary>
    public string[] SourceIds =>
    [
        .. DoesWell.Sources
            .Concat(Lines.SelectMany(line => line.Sources))
            .Concat(ToolsNote?.Sources ?? [])
            .Concat(SkillLine.Sources)
            .Concat(Steps.SelectMany(step => step.Sources))
            .Concat(Cost.Sources)
            .Concat(Models?.Models.SelectMany(model => model.Sources) ?? [])
            .Concat(Questions.SelectMany(question => question.Sources))
            .Distinct()
    ];
}
