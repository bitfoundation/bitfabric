using System.Text.RegularExpressions;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.SetupProject;

/// <summary>
/// What the visitor told the page that sets up a project that already exists (bitfabric-internal/specs/getting-started.md,
/// Setting up an existing project): where the project is, the computer, and the apps to set up on it; and the one line
/// that follows: the installer, then bit setup with the repository's address, which clones the project first.
/// </summary>
public sealed partial record SetupProjectAnswers
{
    /// <summary>The repository's address as the visitor typed it, or null.</summary>
    public string? Repository { get; init; }

    /// <summary>The project is in a folder of this computer already, so the line is pasted there and clones nothing.</summary>
    public bool IsOnThisComputer { get; init; }

    public NewProjectComputer Computer { get; init; }

    /// <summary>The native apps to set up on this computer: the ones asked for that it can build.</summary>
    public IReadOnlyList<NewProjectDevice> Devices { get; init; } = [];

    /// <summary>The address without the spaces and the trailing slash a copy often brings along; null when nothing was typed.</summary>
    public string? RepositoryAddress => string.IsNullOrWhiteSpace(Repository) ? null : Repository.Trim().TrimEnd('/');

    public bool IsRepositoryValid => IsValidRepository(RepositoryAddress);

    /// <summary>The repository's own name, which bit setup names its folder after; null without a valid address.</summary>
    public string? RepositoryName
    {
        get
        {
            if (IsRepositoryValid is false)
                return null;

            var name = RepositoryAddress![(RepositoryAddress!.LastIndexOfAny(['/', ':']) + 1)..];

            return name.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? name[..^".git".Length] : name;
        }
    }

    /// <summary>Whether there is a line to give: always in the project's folder, and from a repository only once its address is valid.</summary>
    public bool HasLine => IsOnThisComputer || IsRepositoryValid;

    /// <summary>The bit setup arguments: the address unless the project is here already, the apps, and --yes, as the new project page's line has.</summary>
    public string BitSetupArguments
    {
        get
        {
            List<string> args = ["setup"];

            if (IsOnThisComputer is false && RepositoryAddress is { } address)
                args.Add(address);

            if (Devices.Count > 0)
                args.AddRange(["--platforms", string.Join(',', Devices.Select(d => d.ToString().ToLowerInvariant()))]);

            args.Add("--yes");

            return string.Join(' ', args);
        }
    }

    public string InstallLine => NewProjectAnswers.LineFor(Computer, BitSetupArguments);

    /// <summary>A link that opens this page as it is, so the line can be sent to the computer it is for.</summary>
    public string ShareLink
    {
        get
        {
            var query = new List<string>();

            if (IsOnThisComputer)
            {
                query.Add("here=1");
            }
            else if (RepositoryAddress is { } address)
            {
                query.Add($"repo={Uri.EscapeDataString(address)}");
            }

            if (Devices.Count > 0)
                query.Add($"devices={string.Join(',', Devices.Select(d => d.ToString().ToLowerInvariant()))}");

            return $"{NewProjectAnswers.InstallerHost}{PageUrls.SetupProject}{(query.Count == 0 ? string.Empty : "?" + string.Join('&', query))}";
        }
    }

    /// <summary>
    /// Whether bit setup can clone from this address and the line can carry it: https://host/owner/name, or the SSH form
    /// git@host:owner/name, with only letters, digits and . - _ ~ in its parts and no . or .. part. It is the only word of
    /// the line the visitor typed, and a shared link carries it, so nothing in it may mean something to PowerShell, bash or
    /// Command Prompt. bit CLI's RepositoryAddress (bitplatform) takes the same addresses.
    /// </summary>
    public static bool IsValidRepository(string? address)
    {
        if (address is null)
            return false;

        var path = HttpsRegex().Match(address) is { Success: true } https
            ? https.Groups["path"].Value
            : SshRegex().Match(address) is { Success: true } ssh ? ssh.Groups["path"].Value : null;

        if (path is null)
            return false;

        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^".git".Length];
        }

        return path.Split('/').All(part => part is not ("" or "." or ".."));
    }

    [GeneratedRegex(@"^https://[A-Za-z0-9.-]+(:[0-9]{1,5})?/(?<path>[A-Za-z0-9._~-]+(/[A-Za-z0-9._~-]+)+)$")]
    private static partial Regex HttpsRegex();

    [GeneratedRegex(@"^[A-Za-z0-9._-]+@[A-Za-z0-9.-]+:(?<path>[A-Za-z0-9._~-]+(/[A-Za-z0-9._~-]+)+)$")]
    private static partial Regex SshRegex();
}
