using System.Text;
using Bit.Fabric.Client.Core.Components.Pages.Home.Parts;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>A command as the page shows it: what it does for the visitor, in plain words, and the command itself.</summary>
public sealed record AppCliCommand(string Command, string Title, string Text);

/// <summary>A stage of a project's life, the commands that serve it, and what it is.</summary>
public sealed record AppCliStage(string Icon, string Title, string Text, AppCliCommand[] Commands);

/// <summary>A row of the list of every command: the command as <c>bit --help</c> names it, and one line on what it does.</summary>
public sealed record AppCliReference(string Command, string Text);

/// <summary>A way to install bit CLI: where, what it does, and the line to paste.</summary>
public sealed record AppCliInstall(string Icon, string Title, string Text, string Line);

/// <summary>One turn of the conversation the hero shows: what the visitor asks, the command their AI runs, and its answer.</summary>
public sealed record AppCliTurn(string Ask, string Command, string Reply);

/// <summary>A question <c>/cli</c> answers.</summary>
public sealed record AppCliQuestion(string Question, string Answer);

/// <summary>
/// What <c>/cli</c> says, written once: <see cref="CliPage"/> renders it, and <c>/cli.md</c> writes the same as Markdown for AI
/// agents (See <see cref="Markdown"/>), so the two can't say different things. bit CLI is the tool every bit Fabric project is
/// made and kept with, and the visitor's AI agent usually runs it for them. Every command and what it does is from bit CLI's
/// own source and guide (src/Cli in this repository): the page never names a command or an option it doesn't have.
/// </summary>
public static class AppCliGuide
{
    /// <summary>The page as Markdown, which Server.Web serves.</summary>
    public const string MarkdownUrl = "/cli.md";

    /// <summary>The package on nuget.org.</summary>
    public const string PackageUrl = "https://www.nuget.org/packages/Bit.Cli";

    /// <summary>bit CLI's source, in bit Fabric's own repository.</summary>
    public const string SourceUrl = "https://github.com/bitfoundation/bitfabric/tree/main/src/Cli";

    /// <summary>bit CLI's own guide to every command and option.</summary>
    public const string GuideUrl = "https://github.com/bitfoundation/bitfabric/blob/main/src/Cli/README.md";

    /// <summary>The license the source is under, and where it is.</summary>
    public const string License = "FCL-1.0-MIT";

    public const string LicenseUrl = "https://github.com/bitfoundation/bitfabric/blob/main/LICENSE.md";

    /// <summary>The line that verifies a build's attestation, as <c>bit about</c> prints it for the build it runs.</summary>
    public const string AttestationLine = "gh attestation verify <bit.dll> --repo bitfoundation/bitfabric";

    /// <summary>What lists every command.</summary>
    public const string HelpLine = "bit --help";

    /// <summary>The command that turns the error reports off.</summary>
    public const string TelemetryOffLine = "bit telemetry off";

    /// <summary>The installer the one line runs, without a project to create: it installs the SDK and bit CLI for the user.</summary>
    public static string WindowsInstallLine => $"powershell -c \"irm {NewProjectAnswers.InstallerHost}/install.ps1 | iex\"";

    public static string UnixInstallLine => $"curl -fsSL {NewProjectAnswers.InstallerHost}/install.sh | bash";

    /// <summary>The hero's conversation: the visitor asks in their own words, and their AI runs bit CLI.</summary>
    public static AppCliTurn[] Conversation(IStringLocalizer<AppStrings> l) =>
    [
        new(l[nameof(AppStrings.CliChatSetupAsk)], "bit setup https://github.com/you/your-app", l[nameof(AppStrings.CliChatSetupReply)]),
        new(l[nameof(AppStrings.CliChatRatingAsk)], "bit migration add AddProductRating", l[nameof(AppStrings.CliChatRatingReply)])
    ];

    /// <summary>What it does for the visitor, by the stage of the project it serves.</summary>
    public static AppCliStage[] Stages(IStringLocalizer<AppStrings> l) =>
    [
        new(BitIconName.Rocket, l[nameof(AppStrings.CliStageStart)], l[nameof(AppStrings.CliStageStartText)],
        [
            new("bit new", l[nameof(AppStrings.CliNewTitle)], l[nameof(AppStrings.CliNewText)]),
            new("bit setup", l[nameof(AppStrings.CliSetupTitle)], l[nameof(AppStrings.CliSetupText)]),
            new("bit run", l[nameof(AppStrings.CliRunTitle)], l[nameof(AppStrings.CliRunText)])
        ]),
        new(BitIconName.CloudUpload, l[nameof(AppStrings.CliStageLive)], l[nameof(AppStrings.CliStageLiveText)],
        [
            new("bit keys", l[nameof(AppStrings.CliKeysTitle)], l[nameof(AppStrings.CliKeysText)]),
            new("bit pipeline", l[nameof(AppStrings.CliPipelineTitle)], l[nameof(AppStrings.CliPipelineText)]),
            new("bit api check", l[nameof(AppStrings.CliApiTitle)], l[nameof(AppStrings.CliApiText)])
        ]),
        new(BitIconName.Sync, l[nameof(AppStrings.CliStageGrow)], l[nameof(AppStrings.CliStageGrowText)],
        [
            new("bit migration add", l[nameof(AppStrings.CliMigrationTitle)], l[nameof(AppStrings.CliMigrationText)]),
            new("bit translate", l[nameof(AppStrings.CliTranslateTitle)], l[nameof(AppStrings.CliTranslateText)]),
            new("bit diff", l[nameof(AppStrings.CliDiffTitle)], l[nameof(AppStrings.CliDiffText)])
        ])
    ];

    /// <summary>Why the visitor rarely types any of it: their AI knows when to run each command, what to do when one stops, and bit CLI keeps itself current.</summary>
    public static AppHomeTile[] YourAI(IStringLocalizer<AppStrings> l) =>
    [
        new(BitIconName.Robot, l[nameof(AppStrings.CliKnowsWhen)], l[nameof(AppStrings.CliKnowsWhenText)]),
        new(BitIconName.Lifesaver, l[nameof(AppStrings.CliStuckStep)], l[nameof(AppStrings.CliStuckStepText)]),
        new(BitIconName.SyncStatus, l[nameof(AppStrings.CliRightVersion)], l[nameof(AppStrings.CliRightVersionText)])
    ];

    /// <summary>Every command <c>bit --help</c> lists, in its order, one line each.</summary>
    public static AppCliReference[] Commands(IStringLocalizer<AppStrings> l) =>
    [
        new("bit new", l[nameof(AppStrings.CliRefNew)]),
        new("bit setup", l[nameof(AppStrings.CliRefSetup)]),
        new("bit run", l[nameof(AppStrings.CliRefRun)]),
        new("bit diff", l[nameof(AppStrings.CliRefDiff)]),
        new("bit doctor", l[nameof(AppStrings.CliRefDoctor)]),
        new("bit trust", l[nameof(AppStrings.CliRefTrust)]),
        new("bit clean", l[nameof(AppStrings.CliRefClean)]),
        new("bit rename", l[nameof(AppStrings.CliRefRename)]),
        new("bit remove", l[nameof(AppStrings.CliRefRemove)]),
        new("bit migration", l[nameof(AppStrings.CliRefMigration)]),
        new("bit api", l[nameof(AppStrings.CliRefApi)]),
        new("bit translate", l[nameof(AppStrings.CliRefTranslate)]),
        new("bit decode", l[nameof(AppStrings.CliRefDecode)]),
        new("bit keys", l[nameof(AppStrings.CliRefKeys)]),
        new("bit pipeline", l[nameof(AppStrings.CliRefPipeline)]),
        new("bit aot-profile", l[nameof(AppStrings.CliRefAotProfile)]),
        new("bit telemetry", l[nameof(AppStrings.CliRefTelemetry)]),
        new("bit update", l[nameof(AppStrings.CliRefUpdate)]),
        new("bit license", l[nameof(AppStrings.CliRefLicense)]),
        new("bit about", l[nameof(AppStrings.CliRefAbout)])
    ];

    /// <summary>The ways to install it on its own: the one line's installers, or the package with the SDK already there.</summary>
    public static AppCliInstall[] Installs(IStringLocalizer<AppStrings> l) =>
    [
        new(BitIconName.WindowsLogo, l[nameof(AppStrings.NewProjectWindows)], l[nameof(AppStrings.CliInstallWindowsText)], WindowsInstallLine),
        new(BitIconName.CommandPrompt, l[nameof(AppStrings.CliInstallUnix)], l[nameof(AppStrings.CliInstallUnixText)], UnixInstallLine),
        new(BitIconName.Package, l[nameof(AppStrings.CliInstallSdk)], l[nameof(AppStrings.CliInstallSdkText)], NewProjectAnswers.ManualInstallLine)
    ];

    /// <summary>What the visitor can check for themselves: the source, the build and what it reports.</summary>
    public static AppHomeTile[] Check(IStringLocalizer<AppStrings> l, RenderFragment? source = null, RenderFragment? verify = null, RenderFragment? telemetry = null) =>
    [
        new(BitIconName.Code, l[nameof(AppStrings.CliCheckSource)], l[nameof(AppStrings.CliCheckSourceText), License], More: source),
        new(BitIconName.Certificate, l[nameof(AppStrings.CliCheckBuild)], l[nameof(AppStrings.CliCheckBuildText)], More: verify),
        new(BitIconName.View, l[nameof(AppStrings.CliCheckReports)], l[nameof(AppStrings.CliCheckReportsText)], More: telemetry)
    ];

    public static AppCliQuestion[] Questions(IStringLocalizer<AppStrings> l) =>
    [
        new(l[nameof(AppStrings.CliQuestionLearn)], l[nameof(AppStrings.CliAnswerLearn)]),
        new(l[nameof(AppStrings.CliQuestionComputers)], l[nameof(AppStrings.CliAnswerComputers)]),
        new(l[nameof(AppStrings.CliQuestionChanges)], l[nameof(AppStrings.CliAnswerChanges)]),
        new(l[nameof(AppStrings.CliQuestionBitPlatform)], l[nameof(AppStrings.CliAnswerBitPlatform)])
    ];

    /// <summary>
    /// The page as Markdown for AI agents, in the order the page shows it. <paramref name="pageUrl"/> and
    /// <paramref name="getStartedUrl"/> are whole addresses on the host serving it.
    /// </summary>
    public static string Markdown(IStringLocalizer<AppStrings> l, string pageUrl, string getStartedUrl)
    {
        var md = new StringBuilder();

        md.Append("# ").AppendLine(l[nameof(AppStrings.CliTitle)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.CliLead)]).AppendLine();
        md.Append("The page: ").AppendLine(pageUrl).AppendLine();

        md.Append("## ").AppendLine(l[nameof(AppStrings.CliWhatTitle)]).AppendLine();
        foreach (var stage in Stages(l))
        {
            md.Append("### ").AppendLine(stage.Title).AppendLine();
            md.AppendLine(stage.Text).AppendLine();
            foreach (var command in stage.Commands)
            {
                md.Append("- **").Append(command.Title).Append("** (`").Append(command.Command).Append("`): ").AppendLine(command.Text);
            }
            md.AppendLine();
        }

        md.Append("## ").AppendLine(l[nameof(AppStrings.CliYourAITitle)]).AppendLine();
        foreach (var turn in Conversation(l))
        {
            md.Append("- ").Append(turn.Ask).Append(" → `").Append(turn.Command).Append("`: ").AppendLine(turn.Reply);
        }
        md.AppendLine();
        foreach (var tile in YourAI(l))
        {
            md.Append("- **").Append(tile.Title).Append("**: ").AppendLine(tile.Text);
        }
        md.AppendLine();

        md.Append("## ").AppendLine(l[nameof(AppStrings.CliCommandsTitle)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.CliCommandsIntro), HelpLine, "bit <command> --help"]).AppendLine();
        md.Append("| ").Append(l[nameof(AppStrings.CliCommandColumn)]).Append(" | ").Append(l[nameof(AppStrings.NewProjectStepWhatItDoes)]).AppendLine(" |");
        md.AppendLine("|---|---|");
        foreach (var command in Commands(l))
        {
            md.Append("| `").Append(command.Command).Append("` | ").Append(command.Text).AppendLine(" |");
        }
        md.AppendLine();
        md.Append('[').Append(l[nameof(AppStrings.CliGuideLink)]).Append("](").Append(GuideUrl).AppendLine(")").AppendLine();

        md.Append("## ").AppendLine(l[nameof(AppStrings.CliInstallTitle)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.CliInstallIntro)]).AppendLine();
        foreach (var install in Installs(l))
        {
            md.Append("### ").AppendLine(install.Title).AppendLine();
            md.AppendLine(install.Text).AppendLine();
            md.AppendLine("```").AppendLine(install.Line).AppendLine("```").AppendLine();
        }
        md.Append("- [").Append(l[nameof(AppStrings.CliPackageLink)]).Append("](").Append(PackageUrl).AppendLine(")");
        md.Append("- [").Append(l[nameof(AppStrings.CliSourceLink)]).Append("](").Append(SourceUrl).AppendLine(")");
        md.AppendLine();

        md.Append("## ").AppendLine(l[nameof(AppStrings.CliCheckTitle)]).AppendLine();
        foreach (var tile in Check(l))
        {
            md.Append("- **").Append(tile.Title).Append("**: ").AppendLine(tile.Text);
        }
        md.AppendLine();
        md.AppendLine("```").AppendLine(AttestationLine).AppendLine(TelemetryOffLine).AppendLine("```").AppendLine();
        md.Append('[').Append(License).Append("](").Append(LicenseUrl).AppendLine(")").AppendLine();

        md.Append("## ").AppendLine(l[nameof(AppStrings.CompareQuestionsTitle)]).AppendLine();
        foreach (var question in Questions(l))
        {
            md.Append("### ").AppendLine(question.Question).AppendLine();
            md.AppendLine(question.Answer).AppendLine();
        }

        md.Append("## ").AppendLine(l[nameof(AppStrings.AppHomeFinalTitle)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.AppHomeFinalText)]).AppendLine();
        md.Append('[').Append(l[nameof(AppStrings.AppHomeGetStarted)]).Append("](").Append(getStartedUrl).AppendLine(")");

        return md.ToString().ReplaceLineEndings("\n");
    }
}
