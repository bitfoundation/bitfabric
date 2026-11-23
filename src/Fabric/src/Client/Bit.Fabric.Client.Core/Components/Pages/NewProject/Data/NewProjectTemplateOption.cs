using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

/// <summary>The control a template option takes on the page.</summary>
public enum NewProjectOptionKind
{
    Choice,
    Toggle
}

/// <summary>
/// One of the project template's options that no question of the page asks, shown among the technical choices or under
/// "Super technical":
/// its name in bit new, the control it takes, the value the page picks for it, the value bit CLI takes without it, and its
/// choices. Only a value that differs from bit CLI's goes into the line, so an untouched section adds nothing to it.
/// </summary>
public sealed record NewProjectTemplateOption(string Name, NewProjectOptionKind Kind, string Default, string CliDefault, string[] Choices)
{
    /// <summary>
    /// Every option of the template (bit-bp's template.json) that the questions leave out, in the order the sections show them
    /// and bit new takes them. Left out on purpose: helpUrl, which bit CLI hides, the generated symbols, and the addresses
    /// (apiServerUrl, webAppUrl, landingPageUrl), which the deploy skill fills in once the app has a domain.
    /// </summary>
    public static IReadOnlyList<NewProjectTemplateOption> All { get; } =
    [
        // .NET 11 by default, as bit Fabric itself runs on it, while bit CLI still takes .NET 10.
        new("framework", NewProjectOptionKind.Choice, "net11.0", "net10.0", ["net10.0", "net11.0"]),
        new("api", NewProjectOptionKind.Choice, "Integrated", "Integrated", ["Integrated", "Standalone"]),
        // S3 by default: the files outlive the server, so it differs from bit CLI's.
        new("filesStorage", NewProjectOptionKind.Choice, "S3", "Local", ["Local", "S3", "AzureBlobStorage", "Other"]) { IsTechnical = true },
        new("captcha", NewProjectOptionKind.Choice, "None", "None", ["reCaptcha", "None"]),
        new("aspire", NewProjectOptionKind.Toggle, "true", "true", []),
        new("redis", NewProjectOptionKind.Toggle, "false", "false", []),
        // Among the technical choices, as file storage is.
        new("cloudflare", NewProjectOptionKind.Toggle, "true", "true", []) { IsTechnical = true },
        new("brouter", NewProjectOptionKind.Toggle, "true", "true", []),
        new("ads", NewProjectOptionKind.Toggle, "false", "false", []),
        new("advancedTests", NewProjectOptionKind.Toggle, "false", "false", []),
        // The last item of the section.
        new("theme", NewProjectOptionKind.Choice, "Fluent2", "Fluent2", ["Fluent2", "Fluent", "Cupertino", "Material"])
    ];

    /// <summary>Whether the technical choices show it, rather than "Super technical".</summary>
    public bool IsTechnical { get; init; }

    /// <summary>The options "Super technical" shows: all but the technical choices' own.</summary>
    public static IEnumerable<NewProjectTemplateOption> SuperTechnical => All.Where(option => option.IsTechnical is false);

    public static NewProjectTemplateOption? Find(string name) =>
        All.FirstOrDefault(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The value as bit new takes it, or null when it isn't one this option takes, so a mistyped link keeps the default.</summary>
    public string? Normalize(string? value) => Kind switch
    {
        NewProjectOptionKind.Choice => Choices.FirstOrDefault(choice => string.Equals(choice, value?.Trim(), StringComparison.OrdinalIgnoreCase)),
        NewProjectOptionKind.Toggle => value?.Trim().ToLowerInvariant() switch
        {
            "true" or "on" => "true",
            "false" or "off" => "false",
            _ => null
        },
        _ => null
    };
}
