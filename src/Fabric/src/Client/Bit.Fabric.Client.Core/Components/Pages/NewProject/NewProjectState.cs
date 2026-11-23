using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject;

/// <summary>
/// What the visitor chose on the new project page: every answer, the step they are on, and which help and details are open.
/// Answers keep their defaults while they are null or false, so an untouched page shows the recommended project. The design
/// keeps the same in a bit Blex store; the app has no Blex, so this is a plain object the page hands its parts, and the page
/// keeps the answers in its address (See NewProjectPage.WriteAddress), so a reload or a shared link brings them back.
/// </summary>
public class NewProjectState
{
    /// <summary>Raised after every change, so the page and each part that shows it renders again.</summary>
    public event Action? Changed;

    public string? ProductName { get; private set; }
    public NewProjectGoal? Goal { get; private set; }
    public NewProjectStart Start { get; private set; }
    public bool IsOneOrganization { get; private set; }
    public NewProjectDatabase? Database { get; private set; }
    public bool IsAssistantOff { get; private set; }
    public bool IsPushOff { get; private set; }
    public NewProjectErrors Errors { get; private set; }
    public bool IsGitHubRepoOff { get; private set; }
    public bool IsOffline { get; private set; }
    public NewProjectDevice[]? Devices { get; private set; }

    /// <summary>The super technical options the visitor changed, by their name in bit new; the rest keep the page's default.</summary>
    public IReadOnlyDictionary<string, string> TemplateOptions { get; private set; } = new Dictionary<string, string>();

    /// <summary>Null follows the computer the browser runs on.</summary>
    public NewProjectComputer? Computer { get; private set; }

    /// <summary>The operating system the visitor's browser names, read once the page is in the browser; null until then.</summary>
    public string? BrowserOs { get; private set; }

    /// <summary>The step the visitor is on.</summary>
    public int Step { get; private set; }

    /// <summary>The help topic open in the side panel, or null.</summary>
    public string? HelpTopic { get; private set; }

    public bool IsMoreOpen { get; private set; }
    public bool IsSuperTechnicalOpen { get; private set; }
    public bool IsLineExplained { get; private set; }

    /// <summary>The answers as they stand, with the computer guessed from the browser until the visitor picks one.</summary>
    public NewProjectAnswers Answers => new()
    {
        Name = ProductName,
        Goal = Goal ?? NewProjectGoal.Real,
        Start = Start,
        IsMultiTenant = IsOneOrganization is false,
        PickedDatabase = Database,
        HasAssistant = IsAssistantOff is false,
        HasPush = IsPushOff is false,
        Errors = Errors,
        PickedDevices = Devices ?? [],
        Computer = Computer ?? DetectedComputer ?? NewProjectComputer.Windows,
        HasGitHubRepo = IsGitHubRepoOff is false,
        IsOffline = IsOffline,
        TemplateOptions = TemplateOptions
    };

    /// <summary>
    /// The computer the visitor's browser runs on, read from the browser (the user's review, 2026-11-07), or null on a
    /// phone or tablet, which can't tell which computer the project will be created on. The visitor's own pick, on the page,
    /// wins over it.
    /// </summary>
    public NewProjectComputer? DetectedComputer => FromBrowser(BrowserOs);

    /// <summary>The computer an operating system's name stands for; null for a phone's, a tablet's, or one not known yet.</summary>
    private static NewProjectComputer? FromBrowser(string? os) => os?.ToLowerInvariant() switch
    {
        null => null,
        var name when name.Contains("windows") => NewProjectComputer.Windows,
        var name when name.Contains("mac") || name.Contains("os x") => NewProjectComputer.MacOs,
        var name when name.Contains("android") || name.Contains("ios") || name.Contains("chrome") => null,
        var name when name.Contains("linux") || name.Contains("ubuntu") || name.Contains("fedora") || name.Contains("debian") => NewProjectComputer.Linux,
        _ => null
    };

    public void Rename(string? name) => Change(() => ProductName = string.IsNullOrWhiteSpace(name) ? null : name.Trim());

    public void Aim(NewProjectGoal goal) => Change(() =>
    {
        Goal = goal;

        // A quick try has no database server; picking it again later is the visitor's own choice.
        if (goal is NewProjectGoal.Try)
        {
            Database = null;
        }
    });

    public void StartWith(NewProjectStart start) => Change(() => Start = start);

    public void Serve(bool isOneOrganization) => Change(() => IsOneOrganization = isOneOrganization);

    public void PickDatabase(NewProjectDatabase database) => Change(() => Database = database);

    public void SwitchAssistant(bool isOn) => Change(() => IsAssistantOff = isOn is false);

    public void SwitchPush(bool isOn) => Change(() => IsPushOff = isOn is false);

    public void SendErrorsTo(NewProjectErrors errors) => Change(() => Errors = errors);

    public void SwitchGitHubRepo(bool isOn) => Change(() => IsGitHubRepoOff = isOn is false);

    public void SwitchOffline(bool isOn) => Change(() => IsOffline = isOn);

    public void SwitchDevice(NewProjectDevice device, bool isOn) => Change(() =>
    {
        var devices = (Devices ?? []).Where(d => d != device);

        Devices = isOn ? [.. devices, device] : [.. devices];
    });

    /// <summary>Sets a super technical option; a value it doesn't take is ignored, and its default leaves it out of the line.</summary>
    public void SetTemplateOption(string name, string? value) => Change(() =>
    {
        if (NewProjectTemplateOption.Find(name) is not { } option || option.Normalize(value) is not { } normalized)
            return;

        var options = new Dictionary<string, string>(TemplateOptions);

        if (normalized == option.Default)
        {
            options.Remove(option.Name);
        }
        else
        {
            options[option.Name] = normalized;
        }

        TemplateOptions = options;
    });

    public void PickComputer(NewProjectComputer computer) => Change(() => Computer = computer);

    public void ReadBrowser(string? os) => Change(() => BrowserOs = os);

    public void GoToStep(int step) => Change(() => Step = Math.Max(0, step));

    public void OpenHelp(string? topic) => Change(() => HelpTopic = topic);

    public void ShowMore(bool isOpen) => Change(() => IsMoreOpen = isOpen);

    public void ShowSuperTechnical(bool isOpen) => Change(() => IsSuperTechnicalOpen = isOpen);

    public void ExplainLine(bool isOpen) => Change(() => IsLineExplained = isOpen);

    public void StartOver() => Change(() =>
    {
        ProductName = null;
        Goal = null;
        Start = NewProjectStart.Blank;
        IsOneOrganization = false;
        Database = null;
        IsAssistantOff = false;
        IsPushOff = false;
        Errors = NewProjectErrors.OwnServer;
        IsGitHubRepoOff = false;
        IsOffline = false;
        Devices = null;
        TemplateOptions = new Dictionary<string, string>();
        Computer = null;
        Step = 0;
        HelpTopic = null;
        IsMoreOpen = false;
        IsSuperTechnicalOpen = false;
        IsLineExplained = false;
    });

    /// <summary>
    /// The answers a link carries, the keys <see cref="NewProjectAnswers.ShareLink"/> writes, and the step. A value the page
    /// doesn't know is left at its default, so a mistyped link still opens the page.
    /// </summary>
    public void Restore(Func<string, string?> query)
    {
        ProductName = string.IsNullOrWhiteSpace(query("name")) ? null : query("name")!.Trim();
        Goal = query("goal") is "try" ? NewProjectGoal.Try : null;
        Start = Parse(query("start"), NewProjectStart.Blank);
        IsOneOrganization = query("customers") is "one";
        Database = Enum.TryParse<NewProjectDatabase>(query("db"), ignoreCase: true, out var database) ? database : null;
        IsAssistantOff = query("assistant") is "off";
        IsPushOff = query("push") is "off";
        Errors = Parse(query("errors"), NewProjectErrors.OwnServer);
        IsGitHubRepoOff = query("github") is "off";
        IsOffline = query("offline") is "on";

        var devices = (query("devices") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => Enum.TryParse<NewProjectDevice>(d, ignoreCase: true, out var device) ? device : (NewProjectDevice?)null)
            .OfType<NewProjectDevice>()
            .Distinct()
            .ToArray();
        Devices = devices.Length is 0 ? null : devices;

        TemplateOptions = NewProjectTemplateOption.All
            .Select(option => (option, Value: option.Normalize(query(option.Name))))
            .Where(pair => pair.Value is not null && pair.Value != pair.option.Default)
            .ToDictionary(pair => pair.option.Name, pair => pair.Value!);

        Step = int.TryParse(query("step"), out var step) ? Math.Max(0, step) : 0;
    }

    private static T Parse<T>(string? value, T fallback) where T : struct, Enum => Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private void Change(Action change)
    {
        change();
        Changed?.Invoke();
    }
}
