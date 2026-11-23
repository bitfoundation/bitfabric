using System.Text.RegularExpressions;
using Bit.Cli.Infrastructure;
using Bit.Cli.Projects;

namespace Bit.Cli.Licensing;

/// <summary>
/// What <c>bit new</c> gives a project before its first commit: an id of its own in bit.json, which its license names, and
/// the same id on the docs MCP server's address in each AI tool's MCP settings, as <c>project=&lt;id&gt;</c>.
/// </summary>
public static partial class NewProjectIdentity
{
    private static readonly string[] settingsExtensions = [".json", ".jsonc", ".toml", ".yaml", ".yml"];

    /// <summary>The new project's id; null when it has no bit.json.</summary>
    public static Guid? Assign(string projectDirectory)
    {
        if (BitJsonFile.Read(Path.Combine(projectDirectory, ProjectBit.FileName)) is not { } bitJson)
            return null;

        var id = bitJson.EnsureProjectId();

        NameInDocsMcp(projectDirectory, id);

        return id;
    }

    /// <summary>
    /// Sets <c>project=&lt;id&gt;</c> on every docs MCP address (<c>.../docs-mcp</c>, whatever its host and other query
    /// values) in the settings files at the project's root and in its dot folders, where AI tools keep theirs.
    /// </summary>
    public static void NameInDocsMcp(string projectDirectory, Guid projectId)
    {
        foreach (var file in SettingsFiles(projectDirectory))
        {
            var text = File.ReadAllText(file);
            var named = DocsMcpAddress().Replace(text, address => WithProject(address.Value, projectId));

            if (named != text)
            {
                AtomicFile.WriteAllText(file, named);
            }
        }
    }

    public static string WithProject(string address, Guid projectId)
    {
        var query = address.IndexOf('?');
        var values = query < 0 ? [] : address[(query + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries).Where(value => value.Split('=')[0].Equals("project", StringComparison.OrdinalIgnoreCase) is false);

        return $"{(query < 0 ? address : address[..query])}?{string.Join('&', values.Append($"project={projectId:D}"))}";
    }

    private static IEnumerable<string> SettingsFiles(string projectDirectory)
    {
        var dotFolders = Directory.EnumerateDirectories(projectDirectory)
            .Where(folder => Path.GetFileName(folder) is ['.', ..] and not ".git")
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories));

        return Directory.EnumerateFiles(projectDirectory)
            .Concat(dotFolders)
            .Where(file => settingsExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) && new FileInfo(file).Length < 1024 * 1024);
    }

    [GeneratedRegex(@"https?://[^\s""'<>?#]+/docs-mcp/?(\?[^\s""'<>#]*)?")]
    private static partial Regex DocsMcpAddress();
}
