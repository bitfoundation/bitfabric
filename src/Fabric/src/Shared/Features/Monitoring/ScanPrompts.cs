using System.Text;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Shared.Features.Monitoring;

/// <summary>
/// The prompts for the customer's AI agent (bitfabric-internal/specs/scanners.md: every finding has a prepared prompt, and
/// one prompt fixes them all in order, then runs the build and the tests and sums up the changes). Built from a finding's
/// key and parameters with the English texts of ScanFindings, whatever language the viewer reads, as the incident's prompt
/// is: agents read English best.
/// </summary>
public static class ScanPrompts
{
    private static readonly CultureInfo english = CultureInfo.InvariantCulture;

    /// <summary>The prompt that fixes one finding.</summary>
    public static string For(string? address, ProjectScanKind kind, ProjectScanFindingDto finding)
    {
        StringBuilder prompt = new();

        prompt.Append(CultureInfo.InvariantCulture, $"bit Fabric's {KindName(kind)} scan of my app at {address} found this, a finding of {SeverityName(finding.Severity)} severity, which this project's code can fix:");
        prompt.AppendLine();
        AppendFinding(prompt, finding);
        prompt.AppendLine();
        prompt.Append("Make the fix in this project, keeping to its conventions (AGENTS.md), then run the build and the tests and tell me what you changed. Don't change the live app's settings or data yourself; tell me what I have to change outside the code.");

        return prompt.ToString();
    }

    /// <summary>One prompt that fixes every finding of the latest scans, most urgent first (the "Fix everything" button).</summary>
    public static string ForAll(string? address, IEnumerable<(ProjectScanKind Kind, ProjectScanFindingDto Finding)> findings)
    {
        var ordered = findings.OrderBy(item => item.Finding.Severity).ThenBy(item => item.Kind).ToArray();

        StringBuilder prompt = new();
        prompt.Append(CultureInfo.InvariantCulture, $"bit Fabric scanned my app at {address} and found {ordered.Length} things to fix. Fix them in this project in this order, most urgent first, keeping to its conventions (AGENTS.md):");
        prompt.AppendLine();

        for (var index = 0; index < ordered.Length; index++)
        {
            prompt.AppendLine();
            prompt.Append(CultureInfo.InvariantCulture, $"{index + 1}. [{SeverityName(ordered[index].Finding.Severity)}, {KindName(ordered[index].Kind)}] ");
            AppendFinding(prompt, ordered[index].Finding);
        }

        prompt.AppendLine();
        prompt.Append("When they are all done, run the build and the tests, then sum up what you changed for each finding, and list what I have to change outside the code, such as a CDN, a registrar or a hosting setting.");

        return prompt.ToString();
    }

    private static void AppendFinding(StringBuilder prompt, ProjectScanFindingDto finding)
    {
        if (finding.Key is null || ScanFindings.All.TryGetValue(finding.Key, out var texts) is false)
        {
            prompt.Append(finding.Key);
            return;
        }

        prompt.Append(Text(texts.Title, finding.Parameters)).Append(". ");
        prompt.Append(Text(texts.Why, finding.Parameters)).Append(' ');
        prompt.Append("Fix: ").Append(Text(texts.Fix, finding.Parameters));

        if (finding.Where.Length > 0)
        {
            prompt.Append(" Seen at: ").Append(string.Join(", ", finding.Where)).Append('.');
        }
    }

    private static string Text(string resourceKey, string[] parameters)
    {
        var format = AppStrings.ResourceManager.GetString(resourceKey, english) ?? resourceKey;

        return parameters.Length is 0 ? format : string.Format(english, format, parameters);
    }

    private static string KindName(ProjectScanKind kind) => kind switch
    {
        ProjectScanKind.Seo => "SEO",
        ProjectScanKind.DomainAndCertificate => "domain and certificate",
        _ => kind.ToString().ToLowerInvariant()
    };

    private static string SeverityName(ScanFindingSeverity severity) => severity.ToString().ToLowerInvariant();
}
