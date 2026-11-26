using System.Globalization;
using System.Text;
using System.Text.Json;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// The files the app's monitoring pages download: a project's whole history as CSV or JSON (its Settings tab) and a
/// month's SLA report as CSV or PDF (its SLA tab), made from the samples as the views first show them. The Workbench serves
/// them at the addresses the app's components ask for (See IMonitoringsController.ExportHistoryUri, IProjectSlaController).
/// </summary>
public static class MonitoringExports
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The sample project of an id, as a view shows it before anything was changed in it.</summary>
    public static SampleProject? Find(Guid monitoringId) => MonitoringSamples.Create(MonitoringSamples.Now()).FirstOrDefault(project => project.Id == monitoringId);

    public static string FileNameOf(SampleProject project, string what, string extension) => $"{project.Key}-{what}.{extension}";

    /// <summary>Every period of each part and every incident, in the project's time zone with UTC alongside.</summary>
    public static byte[] HistoryCsv(SampleProject project)
    {
        var zone = MonitoringReads.ZoneOf(project.TimeZoneId);
        var csv = new StringBuilder("kind,part,status,started (local),ended (local),started (UTC),ended (UTC),title or description\n");

        string Local(DateTimeOffset? at) => at is { } value ? TimeZoneInfo.ConvertTime(value, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "";
        string Utc(DateTimeOffset? at) => at?.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";

        foreach (var period in project.Periods.OrderBy(period => period.StartedAt))
        {
            csv.AppendLine(string.Join(',', "period", Quote(period.Title), period.Status, Local(period.StartedAt), Local(period.EndedAt), Utc(period.StartedAt), Utc(period.EndedAt), Quote(period.Description)));
        }

        foreach (var incident in project.Incidents.OrderBy(incident => incident.StartedAt))
        {
            csv.AppendLine(string.Join(',', incident.Kind.ToString().ToLowerInvariant(), Quote(string.Join(" + ", incident.CheckKeys.Select(project.TitleOf))), incident.Impact,
                Local(incident.StartedAt), Local(incident.EndedAt), Utc(incident.StartedAt), Utc(incident.EndedAt), Quote(MonitoringTexts.In(incident.Title, MonitoringTexts.MainCulture))));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    public static byte[] HistoryJson(SampleProject project) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        project = new { project.Name, project.Address, project.TimeZoneId, watchedSince = project.CreatedOn },
        periods = project.Periods,
        incidents = project.Incidents.Select(incident => MonitoringReads.Incident(project, incident)),
        starts = project.Starts,
        maintenance = project.Maintenance
    }, jsonOptions);

    public static byte[] SlaCsv(SampleProject project, int year, int month)
    {
        var report = MonitoringReads.SlaReport(project, year, month, DateTimeOffset.UtcNow);
        var csv = new StringBuilder("part,availability (%),counted minutes,down minutes,partial minutes,degraded minutes,weighted minutes\n");

        foreach (var row in report.Parts.Prepend(report.App))
        {
            csv.AppendLine(string.Join(',', Quote(row.Key is null ? $"{row.Title} (as a whole)" : row.Title), Number(row.Availability), Number(row.CountedMinutes), Number(row.DownMinutes), Number(row.PartialMinutes), Number(row.DegradedMinutes), Number(row.WeightedMinutes)));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    /// <summary>A one page PDF of the month's figures: enough for the download to open as the report would.</summary>
    public static byte[] SlaPdf(SampleProject project, int year, int month)
    {
        var report = MonitoringReads.SlaReport(project, year, month, DateTimeOffset.UtcNow);
        var monthName = ProjectSlaMonths.NameOf(year, month, CultureInfo.InvariantCulture);

        string[] lines =
        [
            $"{project.Name} - SLA report, {monthName}",
            $"{project.Address} - times in {project.TimeZoneId}",
            "",
            $"Availability: {Number(report.App.Availability)}% (promised {Number(report.Settings.TargetPercent)}%)",
            $"Downtime left: {Number(report.LeftMinutes)} of {Number(report.BudgetMinutes)} minutes",
            $"Service credit: {(report.Credit is { } credit ? $"{Number(credit.CreditPercent)}% back" : "none")}",
            "",
            .. report.Parts.Select(row => $"{row.Title}: {Number(row.Availability)}%"),
            "",
            "A sample from the bit Fabric Workbench."
        ];

        return Pdf(lines);
    }

    private static string Quote(string? text) => $"\"{(text ?? "").Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string Number(double? value) => value is { } number ? number.ToString("0.###", CultureInfo.InvariantCulture) : "";

    /// <summary>A PDF of lines of Helvetica text on an A4 page, with its cross-reference table counted byte by byte.</summary>
    private static byte[] Pdf(IEnumerable<string> lines)
    {
        // Helvetica's standard encoding: what isn't ASCII is left out, so the sample stays a valid file in every language.
        static string Escape(string text) => string.Concat(text.Where(c => c < 128)).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

        var content = new StringBuilder("BT /F1 12 Tf 56 780 Td 16 TL\n");
        foreach (var line in lines)
        {
            content.Append(CultureInfo.InvariantCulture, $"({Escape(line)}) Tj T*\n");
        }
        content.Append("ET");

        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content.ToString())} >>\nstream\n{content}\nendstream"
        ];

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();

        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }
        pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
