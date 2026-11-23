namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>What the monitoring exports share: the SLA's CSV and PDF (See ProjectSlaReports) and the whole history (See ProjectHistoryExport).</summary>
public static class MonitoringFiles
{
    /// <summary>
    /// A CSV cell, quoted when it has to be and never read as a formula: a part's name, a title or a team's update is
    /// text of the customer's app or team.
    /// </summary>
    public static string CsvCell(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = $"'{value}";
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    /// <summary>"Sales" for a file name: the project's name in any script, without what a file name can't hold.</summary>
    public static string NameOf(string? projectName)
    {
        var name = new string([.. (projectName ?? "").Select(c => char.IsLetterOrDigit(c) ? c : '-')]).Trim('-');
        while (name.Contains("--", StringComparison.Ordinal))
        {
            name = name.Replace("--", "-", StringComparison.Ordinal);
        }

        return name.Length is 0 ? "project" : name;
    }
}
