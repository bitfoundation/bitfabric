using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;
using System.Text;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

/// <summary>
/// The visitor's answers with every default filled in, and what follows from them: the project's code name, the
/// install line, the download and the time it takes, and what the computer needs. The page and its parts read this
/// instead of the store, so a question left unanswered always shows its default.
/// </summary>
public sealed record NewProjectAnswers
{
    public const string InstallerHost = "https://bitfabric.co";

    /// <summary>The name as the visitor typed it, trimmed; null until they type one.</summary>
    public string? Name { get; init; }

    /// <summary>A real product unless the visitor says it's a try, since that is what the persona came for.</summary>
    public NewProjectGoal Goal { get; init; }

    public NewProjectStart Start { get; init; }

    public bool IsMultiTenant { get; init; } = true;

    /// <summary>The database the visitor picked, or null to follow the goal.</summary>
    public NewProjectDatabase? PickedDatabase { get; init; }

    public bool HasAssistant { get; init; } = true;

    public bool HasPush { get; init; } = true;

    /// <summary>The customer's own server unless the visitor picks a service, as it costs nothing more.</summary>
    public NewProjectErrors Errors { get; init; }

    /// <summary>The native apps the visitor asked for, including ones this computer can't build.</summary>
    public NewProjectDevice[] PickedDevices { get; init; } = [];

    public NewProjectComputer Computer { get; init; }

    public bool HasGitHubRepo { get; init; } = true;

    /// <summary>A database on every device, so the app keeps working without internet; off unless the visitor asks, as it makes the app heavier.</summary>
    public bool IsOffline { get; init; }

    /// <summary>The template options the visitor changed, by their name in bit new (See <see cref="NewProjectTemplateOption"/>).</summary>
    public IReadOnlyDictionary<string, string> TemplateOptions { get; init; } = new Dictionary<string, string>();

    /// <summary>A super technical option's value: the visitor's, or the one the page picks for it.</summary>
    public string Option(string name) => TemplateOptions.TryGetValue(name, out var value) ? value : NewProjectTemplateOption.Find(name)?.Default ?? string.Empty;

    public bool IsTry => Goal is NewProjectGoal.Try;

    /// <summary>PostgreSQL for a real product unless the visitor picked another one, and SQLite for a quick try.</summary>
    public NewProjectDatabase Database => IsTry ? NewProjectDatabase.Sqlite : PickedDatabase ?? NewProjectDatabase.PostgreSql;

    /// <summary>
    /// A database server or Redis runs in a container of Aspire's while you develop, so the first run installs a container
    /// engine, as bit CLI decides it.
    /// </summary>
    public bool NeedsContainers => Option("aspire") is "true" && (Database is not NewProjectDatabase.Sqlite || Option("redis") is "true");

    /// <summary>The native apps that get set up now: the ones asked for that this computer can build.</summary>
    public IReadOnlyList<NewProjectDevice> Devices => [.. PickedDevices.Distinct().Where(CanBuild).Order()];

    /// <summary>The live dashboard needs the admin panel and live updates, so it is only promised with both.</summary>
    public bool HasLiveDashboard => Start is NewProjectStart.Admin && HasAssistant;

    /// <summary>The name of the folder and the code: letters and digits, words joined, starting with a letter.</summary>
    public string CodeName => IsNameValid && string.IsNullOrWhiteSpace(Name) is false ? Name! : SuggestedName;

    /// <summary>The name people see in the app.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? CodeName : Name;

    /// <summary>
    /// Whether the typed name can name the project as it is, by the rules of a C# project's name: it is the folder, the
    /// .csproj files and the namespaces. Nothing typed yet is valid, and takes MyProduct.
    /// </summary>
    public bool IsNameValid => string.IsNullOrWhiteSpace(Name) || IsValidProjectName(Name);

    /// <summary>A valid name made from the typed one, offered when the typed one isn't valid.</summary>
    public string SuggestedName
    {
        get
        {
            var name = ToCodeName(Name);

            return IsValidProjectName(name) ? name : name + "App";
        }
    }

    /// <summary>
    /// A C# project's name that every part of the project takes: parts joined by dots, each a C# identifier of English
    /// letters and digits that starts with a letter and isn't a C# keyword, at most 40 characters, and not a name Windows
    /// keeps for a device, as it names a folder. No underscore or other letters, as the iOS bundle id takes neither.
    /// </summary>
    public static bool IsValidProjectName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 40 || WindowsDeviceNames.Contains(name.Split('.')[0]))
            return false;

        return name.Split('.').All(part => part.Length > 0
            && char.IsAsciiLetter(part[0])
            && part.All(char.IsAsciiLetterOrDigit)
            && CSharpKeywords.Contains(part) is false);
    }

    private static readonly HashSet<string> WindowsDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
        "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile", "while"
    };

    public bool CanBuild(NewProjectDevice device) => device switch
    {
        // On Windows the iPhone app builds on a Mac on the network or in the cloud
        NewProjectDevice.Ios => Computer is NewProjectComputer.MacOs or NewProjectComputer.Windows,
        NewProjectDevice.MacOs => Computer is NewProjectComputer.MacOs,
        NewProjectDevice.Windows => Computer is NewProjectComputer.Windows,
        _ => true
    };

    /// <summary>The bit new arguments, with only the options that differ from bit CLI's defaults, so the line stays readable.</summary>
    public string BitNewArguments
    {
        get
        {
            var args = new List<string> { "new", CodeName };

            if (Database is not NewProjectDatabase.Sqlite) args.AddRange(["--database", Database switch
            {
                NewProjectDatabase.PostgreSql => "PostgreSQL",
                NewProjectDatabase.SqlServer => "SqlServer",
                _ => "MySql"
            }]);
            if (Start is not NewProjectStart.Blank) args.AddRange(["--module", Start is NewProjectStart.Admin ? "Admin" : "Sales"]);
            if (IsMultiTenant is false) args.AddRange(["--multitenant", "false"]);
            if (HasAssistant is false) args.AddRange(["--signalR", "false"]);
            if (HasPush is false) args.AddRange(["--notification", "false"]);
            if (Errors is NewProjectErrors.Sentry) args.AddRange(["--sentry", "true"]);
            if (Errors is NewProjectErrors.AppInsights) args.AddRange(["--appInsights", "true"]);
            if (IsOffline) args.AddRange(["--offlineDb", "true"]);
            if (IsTry) args.AddRange(["--sample", "true"]);
            // The template options, each only when it differs from bit CLI's; S3, the page's default, always does.
            foreach (var option in NewProjectTemplateOption.All)
            {
                var value = Option(option.Name);

                if (value != option.CliDefault) args.AddRange([$"--{option.Name}", value]);
            }
            if (HasGitHubRepo) args.Add("--github-repo");
            if (Devices.Count > 0) args.AddRange(["--platforms", string.Join(',', Devices.Select(d => d.ToString().ToLowerInvariant()))]);
            args.Add("--yes");

            return string.Join(' ', args);
        }
    }

    /// <summary>
    /// The one line the visitor pastes: the signed installer, which installs the SDK and bit CLI, then a readable bit new.
    /// The installer's address is a proposal (bitfabric-internal/specs/getting-started.md). On Windows it starts PowerShell by name, so the same line
    /// runs in Command Prompt as in PowerShell: a visitor who pastes it into cmd isn't stopped at "'irm' is not recognized".
    /// Every word inside its quotes is a valid project name or one of an option's fixed values, never a space, a quote or a
    /// character either shell would read on its own.
    /// </summary>
    public string InstallLine => LineFor(Computer, BitNewArguments);

    /// <summary>
    /// The one line for a computer that runs bit with these arguments once the installer has installed the SDK and bit CLI:
    /// bit new on this page, bit setup on the page that sets up a project that exists (See SetupProjectAnswers). The
    /// arguments go inside PowerShell's quotes on Windows, so they never hold a space inside a word, a quote, or a character
    /// either shell would read on its own.
    /// </summary>
    public static string LineFor(NewProjectComputer computer, string bitArguments) => computer is NewProjectComputer.Windows
        ? $"powershell -c \"irm {InstallerHost}/install.ps1 | iex; bit {bitArguments}\""
        : $"curl -fsSL {InstallerHost}/install.sh | bash -s -- {bitArguments}";

    /// <summary>The two lines for a computer the one-line installer isn't signed for yet: bit CLI from nuget.org, then bit.</summary>
    public static string ManualLinesFor(string bitArguments) => $"dotnet tool install --global Bit.Cli --prerelease{Environment.NewLine}bit {bitArguments}";

    /// <summary>A link that opens the wizard with these answers on another device, so a phone visitor can send it to their computer.</summary>
    public string ShareLink
    {
        get
        {
            var query = new List<string>();

            if (string.IsNullOrWhiteSpace(Name) is false) query.Add($"name={Uri.EscapeDataString(Name)}");
            if (IsTry) query.Add("goal=try");
            if (Start is not NewProjectStart.Blank) query.Add($"start={Start.ToString().ToLowerInvariant()}");
            if (IsMultiTenant is false) query.Add("customers=one");
            if (PickedDatabase is not null) query.Add($"db={PickedDatabase.ToString()!.ToLowerInvariant()}");
            if (HasAssistant is false) query.Add("assistant=off");
            if (HasPush is false) query.Add("push=off");
            if (Errors is not NewProjectErrors.OwnServer) query.Add($"errors={Errors.ToString().ToLowerInvariant()}");
            if (PickedDevices.Length > 0) query.Add($"devices={string.Join(',', PickedDevices.Select(d => d.ToString().ToLowerInvariant()))}");
            if (HasGitHubRepo is false) query.Add("github=off");
            if (IsOffline) query.Add("offline=on");
            foreach (var option in NewProjectTemplateOption.All)
            {
                if (TemplateOptions.TryGetValue(option.Name, out var value)) query.Add($"{option.Name}={Uri.EscapeDataString(value)}");
            }

            return $"{InstallerHost}/new{(query.Count == 0 ? string.Empty : "?" + string.Join('&', query))}";
        }
    }

    /// <summary>What the first run downloads, in GB. Design estimates: the first job is to measure them (bitfabric-internal/specs/getting-started.md).</summary>
    public double DownloadGb => 2 + (NeedsContainers ? 1.5 : 0) + Devices.Sum(d => d switch
    {
        NewProjectDevice.Android => 8,
        NewProjectDevice.Ios => 2,
        _ => 1
    });

    /// <summary>The usual length of the first run in minutes, from and to, on a common home connection.</summary>
    public (int From, int To) Minutes
    {
        get
        {
            var extra = (NeedsContainers ? 5 : 0) + Devices.Sum(d => d switch
            {
                NewProjectDevice.Android => 20,
                NewProjectDevice.Ios => 10,
                _ => 5
            });

            return (10 + extra, 15 + extra);
        }
    }

    /// <summary>Free disk space the project and its tools take, in GB, rounded up to the next 5.</summary>
    public int DiskGb => (int)(Math.Ceiling((DownloadGb * 2.5 + 5) / 5) * 5);

    /// <summary>Memory the computer should have, in GB: the Android emulator needs the most.</summary>
    public int MemoryGb => Devices.Contains(NewProjectDevice.Android) ? 16 : 8;

    /// <summary>Whether a native app needs Xcode, which the visitor installs from the App Store themselves.</summary>
    public bool NeedsXcode => Computer is NewProjectComputer.MacOs && Devices.Any(d => d is NewProjectDevice.Ios or NewProjectDevice.MacOs);

    /// <summary>Whether the iPhone app is set up on Windows, where it builds on a Mac with Xcode, on the network or in the cloud.</summary>
    public bool NeedsMacForIos => Computer is NewProjectComputer.Windows && Devices.Contains(NewProjectDevice.Ios);

    public static string ToCodeName(string? name)
    {
        var code = new StringBuilder();
        var startsWord = true;

        foreach (var c in (name ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                code.Append(startsWord && char.IsAsciiLetter(c) ? char.ToUpperInvariant(c) : c);
                startsWord = false;
            }
            else if (c == '.' && code.Length > 0 && code[^1] != '.')
            {
                code.Append('.');
                startsWord = true;
            }
            else if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) is not System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                startsWord = true;
            }
        }

        var result = code.ToString().Trim('.');

        if (result.Length == 0)
            return "MyProduct";

        if (char.IsAsciiLetter(result[0]) is false)
            result = "App" + result;

        return result.Length > 40 ? result[..40].TrimEnd('.') : result;
    }
}
