using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>
/// The way to the one line and what follows it, for the computer picked on the page: the film, then the line to copy with
/// what it does, how to open a terminal and paste it there, and what happens while it runs. The new project page shows it
/// for bit new, and the page that sets up a project that already exists for bit setup, so both say it
/// the same way. Only the line depends on the server, so only it shows the release's loading, failure and absence.
/// </summary>
public partial class NewProjectLineSteps
{
    [AutoInject] private Clipboard clipboard = default!;

    private NewProjectRelease? release;
    private NewProjectLoadStatus status = NewProjectLoadStatus.Loading;
    private string? copiedText;
    private bool isSafeOpen;

    /// <summary>What the line hands bit once the installer is done, such as "new Bluebird --yes" or "setup https://github.com/acme/Bluebird --yes".</summary>
    [Parameter, EditorRequired] public string Arguments { get; set; } = default!;

    /// <summary>The product's name, which the app in the film carries.</summary>
    [Parameter] public string Name { get; set; } = string.Empty;

    /// <summary>What the line does, step by step and in plain words, shown under "What does this line do?".</summary>
    [Parameter] public IEnumerable<string> PlanSteps { get; set; } = [];

    /// <summary>What the visitor sees while the line runs, under "Look around while it works".</summary>
    [Parameter] public RenderFragment? LookAround { get; set; }

    private string InstallLine => NewProjectAnswers.LineFor(Answers.Computer, Arguments);

    private string ManualLines => NewProjectAnswers.ManualLinesFor(Arguments);

    /// <summary>The line the film shows pasted: the one this page gives the visitor to copy.</summary>
    private string GuideLine => release is not null && release.HasInstaller(Answers.Computer) is false ? ManualLines : InstallLine;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        if (InPrerenderSession)
            return;

        await LoadRelease();
    }

    /// <summary>
    /// The release the line installs. It is the design's for now, written in the page: bit CLI's version and the computers
    /// its one-line installer is offered on. The design's loading and failure states stay for when it
    /// comes from the server.
    /// </summary>
    private Task LoadRelease()
    {
        release = NewProjectRelease.Current;
        status = NewProjectLoadStatus.Ready;

        return Task.CompletedTask;
    }

    private async Task Copy(string text)
    {
        await clipboard.WriteText(text);
        copiedText = text;
    }

    /// <summary>The line's words, each kept on one piece, so a line wraps between words and never inside an option like --database.</summary>
    // Literal sequence numbers, one per place in the code, as Blazor's diffing expects; the loop repeats them
    private static RenderFragment Tokens(string line) => builder =>
    {
        foreach (var (row, rowIndex) in line.Split(Environment.NewLine).Select((row, index) => (row, index)))
        {
            if (rowIndex > 0)
            {
                builder.AddMarkupContent(0, "<br>");
            }

            foreach (var (word, index) in row.Split(' ').Select((word, index) => (word, index)))
            {
                if (index > 0)
                {
                    builder.AddContent(1, " ");
                }

                builder.OpenElement(2, "span");
                builder.AddAttribute(3, "class", "np-word");
                builder.AddContent(4, word);
                builder.CloseElement();
            }
        }
    };

    private static string InstallerUrl(NewProjectComputer computer) => $"{NewProjectAnswers.InstallerHost}/{(computer is NewProjectComputer.Windows ? "install.ps1" : "install.sh")}";

    private string TerminalName(NewProjectComputer computer) => computer switch
    {
        NewProjectComputer.Windows => Localizer[nameof(AppStrings.NewProjectPowerShell)],
        NewProjectComputer.MacOs => Localizer[nameof(AppStrings.NewProjectTerminal)],
        _ => Localizer[nameof(AppStrings.NewProjectATerminal)]
    };

    private string TerminalHowTo(NewProjectComputer computer) => computer switch
    {
        NewProjectComputer.Windows => Localizer[nameof(AppStrings.NewProjectHowToWindows)],
        NewProjectComputer.MacOs => Localizer[nameof(AppStrings.NewProjectHowToMac)],
        _ => Localizer[nameof(AppStrings.NewProjectHowToLinux)]
    };
}
