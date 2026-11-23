using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Cli.Infrastructure;
using Bit.Cli.Projects;

namespace Bit.Cli.Licensing;

/// <summary>
/// A project's bit.json as its license reads and writes it: <c>projectId</c>, <c>name</c> and <c>license</c>. Everything
/// else in it is written back as it was, in the file's own indentation and line endings.
/// </summary>
public sealed class BitJsonFile
{
    public const string ProjectIdProperty = "projectId";

    public const string LicenseProperty = "license";

    private readonly JsonObject root;

    private readonly string newLine;

    private readonly string indent;

    private readonly bool endsWithNewLine;

    private BitJsonFile(string path, JsonObject root, string text)
    {
        FilePath = path;
        this.root = root;
        newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        endsWithNewLine = text.EndsWith('\n');
        indent = text.Split('\n').Select(line => line.TrimEnd('\r')).FirstOrDefault(line => line.Length > 0 && char.IsWhiteSpace(line[0])) is { } first
            ? first[..(first.Length - first.TrimStart().Length)]
            : "    ";
    }

    public string FilePath { get; }

    public string Directory => Path.GetDirectoryName(FilePath)!;

    public Guid? ProjectId => Guid.TryParse(Text(ProjectIdProperty), out var id) && id != Guid.Empty ? id : null;

    /// <summary>The project's name: bit.json's <c>name</c>, or the folder's.</summary>
    public string Name => Text("name") is { Length: > 0 } name ? name : Path.GetFileName(Directory);

    public string? License => Text(LicenseProperty) is { Length: > 0 } license ? license : null;

    /// <summary>The nearest bit.json, in <paramref name="directory"/> or a folder above it.</summary>
    public static BitJsonFile? Find(string directory)
    {
        try
        {
            for (var folder = new DirectoryInfo(directory); folder is not null; folder = folder.Parent)
            {
                var file = Path.Combine(folder.FullName, ProjectBit.FileName);

                if (File.Exists(file))
                    return Read(file);
            }
        }
        catch (Exception exp) when (exp is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
        }

        return null;
    }

    public static BitJsonFile? Read(string path)
    {
        try
        {
            var text = File.ReadAllText(path);

            return JsonNode.Parse(text, documentOptions: new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) is JsonObject root
                ? new BitJsonFile(path, root, text)
                : null;
        }
        catch (Exception exp) when (exp is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The project's id, made and written now when bit.json has none.</summary>
    public Guid EnsureProjectId()
    {
        if (ProjectId is { } id)
            return id;

        id = Guid.NewGuid();
        Set(ProjectIdProperty, id.ToString("D"), after: "name");
        Save();

        return id;
    }

    public void SetLicense(string license)
    {
        Set(LicenseProperty, license, after: ProjectIdProperty);
        Save();
    }

    private string? Text(string property) => root[property] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : null;

    private void Set(string property, string value, string after)
    {
        if (root.ContainsKey(property))
        {
            root[property] = value;
            return;
        }

        var index = root.IndexOf(after);
        root.Insert(index < 0 ? root.Count : index + 1, property, value);
    }

    private void Save()
    {
        var json = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            IndentCharacter = indent.StartsWith('\t') ? '\t' : ' ',
            IndentSize = Math.Max(1, indent.Length),
            NewLine = newLine,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        AtomicFile.WriteAllText(FilePath, endsWithNewLine ? json + newLine : json);
    }
}
