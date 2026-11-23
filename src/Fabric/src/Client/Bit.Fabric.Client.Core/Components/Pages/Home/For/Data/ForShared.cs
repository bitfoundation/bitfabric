using Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;

/// <summary>
/// What every page of /for says the same way: its title and lead around the
/// agent's name, the lines of the files the agents share, the skills, the two steps every start has and the last one, and the
/// questions every page asks. An agent's class says only what is its own. bit Fabric's words stay within the claims table of
/// bitfabric-internal/.agents/product-marketing.md: no .NET or C#, never "free" for the whole thing, "a complete identity solution", and nothing
/// that says building takes months.
/// </summary>
public static class ForShared
{
    /// <summary>The day every source was read, for the JSON-LD's dateModified.</summary>
    public static readonly DateOnly CheckedOn = new(2026, 11, 21);

    /// <summary>The skill a first feature is asked of, named in the last step of every page.</summary>
    public const string FirstSkill = "ai-dlc";

    /// <summary>A fact and the ids of the sources it rests on.</summary>
    public static CompareFact F(string text, params string[] sources) => new(text, sources);

    public static string PageTitle(IStringLocalizer<AppStrings> l, string name) => l[nameof(AppStrings.ForPageTitle), name];

    public static string Title(IStringLocalizer<AppStrings> l, string name) => l[nameof(AppStrings.ForTitle), name];

    /// <summary>The lead every page opens with, then the line that is the agent's own.</summary>
    public static string Lead(IStringLocalizer<AppStrings> l, string name, string own) => $"{l[nameof(AppStrings.ForLeadStart), name]} {own}";

    public static string ReadsTitle(IStringLocalizer<AppStrings> l, string name) => l[nameof(AppStrings.ForReadsTitle), name];

    public static string ReadsIntro(IStringLocalizer<AppStrings> l, string name) => l[nameof(AppStrings.ForReadsIntro), name];

    /// <summary>AGENTS.md, which every project carries and most agents read.</summary>
    public static AgentLine AgentsMd(IStringLocalizer<AppStrings> l, params string[] sources)
        => new(AgentLineKind.Instructions, "AGENTS.md", l[nameof(AppStrings.ForLineAgentsMd)], sources);

    /// <summary>The project's skills, in the folder this agent reads them from.</summary>
    public static AgentLine SkillsLine(IStringLocalizer<AppStrings> l, string folder, params string[] sources)
        => new(AgentLineKind.Skills, folder, l[nameof(AppStrings.ForLineSkills)], sources);

    /// <summary>The project's tools, in the file this agent reads them from.</summary>
    public static AgentLine ToolsLine(IStringLocalizer<AppStrings> l, string file, params string[] sources)
        => new(AgentLineKind.Tools, file, l[nameof(AppStrings.ForLineTools)], sources);

    /// <summary>How a skill is called in an agent with a <see cref="AgentPage.SkillCall"/> format, such as /ai-dlc.</summary>
    public static string Call(string format, string skill) => string.Format(CultureInfo.InvariantCulture, format, skill);

    /// <summary>The last step of every page: asking for the first feature, by the skill's call or, where the agent picks skills by their description, in words.</summary>
    public static AgentStep Ask(IStringLocalizer<AppStrings> l, string? callFormat)
        => callFormat is null
            ? new(l[nameof(AppStrings.ForStepAskTitle)], l[nameof(AppStrings.ForStepAskPlainText), FirstSkill], [])
            : new(l[nameof(AppStrings.ForStepAskTitle)], l[nameof(AppStrings.ForStepAskText), Call(callFormat, FirstSkill)], [], Call(callFormat, FirstSkill));

    /// <summary>The two steps every start has, before the agent's own: the questions, then the one line.</summary>
    public static AgentStep[] FirstSteps(IStringLocalizer<AppStrings> l) =>
    [
        new(l[nameof(AppStrings.ForStepQuestionsTitle)], l[nameof(AppStrings.ForStepQuestionsText)], []),
        new(l[nameof(AppStrings.ForStepLineTitle)], l[nameof(AppStrings.ForStepLineText)], [])
    ];

    /// <summary>
    /// The skills every page shows, by their names in the project, each with its icon, title and line. What each does is bit
    /// Fabric's own, so it rests on no source; how it is called is the agent's (See <see cref="AgentPage.SkillCall"/>).
    /// </summary>
    public static (string Skill, string Icon, string Title, string Text)[] Skills(IStringLocalizer<AppStrings> l) =>
    [
        (FirstSkill, BitIconName.People, l[nameof(AppStrings.ForSkillAiDlcTitle)], l[nameof(AppStrings.ForSkillAiDlcText)]),
        ("workbench", BitIconName.Design, l[nameof(AppStrings.ForSkillWorkbenchTitle)], l[nameof(AppStrings.ForSkillWorkbenchText)]),
        ("deploy", BitIconName.Deploy, l[nameof(AppStrings.ForSkillDeployTitle)], l[nameof(AppStrings.ForSkillDeployText)]),
        ("port", BitIconName.Download, l[nameof(AppStrings.ForSkillPortTitle)], l[nameof(AppStrings.ForSkillPortText)]),
        ("troubleshoot", BitIconName.Bug, l[nameof(AppStrings.ForSkillTroubleshootTitle)], l[nameof(AppStrings.ForSkillTroubleshootText)]),
        ("rebrand", BitIconName.Color, l[nameof(AppStrings.ForSkillRebrandTitle)], l[nameof(AppStrings.ForSkillRebrandText)])
    ];

    /// <summary>Whether the visitor can move to another agent later: yes, the files sit side by side.</summary>
    public static CompareQuestion Switch(IStringLocalizer<AppStrings> l)
        => new(l[nameof(AppStrings.ForQuestionSwitch)], l[nameof(AppStrings.ForAnswerSwitch)], []);

    /// <summary>What bit Fabric sees, in the honest sentence of bitfabric-internal/specs/product.md (Q27).</summary>
    public static CompareQuestion Sees(IStringLocalizer<AppStrings> l)
        => new(l[nameof(AppStrings.ForQuestionSees)], l[nameof(AppStrings.ForAnswerSees)], []);
}
