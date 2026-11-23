using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using SkiaSharp;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// Draws a month's SLA report as an A4 PDF with SkiaSharp (bitfabric-internal/specs/monitoring.md, SLA reports), in the
/// viewer's language and direction, in bit Fabric's light look: its logo, Inter and Vazirmatn, ink headings on white, soft
/// gray surfaces. It is a report to forward, so every figure carries the weights it was counted with, and every time is in
/// the project's zone with the zone named, since its reader may be anywhere.
/// </summary>
public sealed class SlaPdfWriter(ProjectSlaReportDto report, IStringLocalizer<AppStrings> localizer, CultureInfo culture, byte[]? logo)
{
    private const float PageWidth = 595.28f;
    private const float PageHeight = 841.89f;
    private const float Margin = 44;
    private const float FooterHeight = 28;
    private const float ContentWidth = PageWidth - 2 * Margin;

    // bit Fabric's light theme (Client.Core's Styles/_brand.scss), and Fluent 2's status colors it keeps.
    private static readonly SKColor ink = SKColor.Parse("#2C2C2B");
    private static readonly SKColor text = SKColor.Parse("#050505");
    private static readonly SKColor secondary = SKColor.Parse("#636363");
    private static readonly SKColor border = SKColor.Parse("#D4D3CF");
    private static readonly SKColor surface = SKColor.Parse("#F5F5F5");
    private static readonly SKColor success = SKColor.Parse("#107C10");
    private static readonly SKColor error = SKColor.Parse("#C50F1F");
    private static readonly SKColor warning = SKColor.Parse("#BC4B09");
    private static readonly SKColor info = SKColor.Parse("#1A5EFF");

    private readonly TimeZoneInfo zone = TimeZoneInfo.TryFindSystemTimeZoneById(report.TimeZoneId ?? "UTC", out var found) ? found : TimeZoneInfo.Utc;

    private SlaPdfText fonts = default!;
    private SKDocument document = default!;
    private SKCanvas canvas = default!;
    private int page;
    private float y;

    private double Target => report.Settings.TargetPercent;

    public byte[] Write()
    {
        using var stream = new MemoryStream();
        using (fonts = new SlaPdfText(culture))
        using (document = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata
        {
            Title = localizer[nameof(AppStrings.MonitoringSlaPdfTitle), report.ProjectName ?? "", MonthName],
            Author = "bit Fabric",
            Creator = "bit Fabric",
            Subject = report.Address ?? "",
            Creation = report.GeneratedAt.UtcDateTime,
            Modified = report.GeneratedAt.UtcDateTime
        }))
        {
            NewPage();

            Header();
            Figures();
            Formula();
            Parts();
            Months();
            Credits();
            LeftOut();

            document.EndPage();
            document.Close();
        }

        return stream.ToArray();
    }

    private string MonthName => ProjectSlaMonths.NameOf(report.Year, report.Month, culture);

    private void NewPage()
    {
        if (page > 0)
        {
            document.EndPage();
        }

        page++;
        canvas = document.BeginPage(PageWidth, PageHeight);
        y = Margin;

        // The footer first, so a page always has it, however far the content goes.
        var footerY = PageHeight - Margin + 14;
        Line(Margin, PageHeight - Margin - 2, PageWidth - Margin, PageHeight - Margin - 2, border);
        fonts.DrawAligned(canvas, localizer[nameof(AppStrings.MonitoringSlaPdfFooter), LocalTime(report.GeneratedAt), report.TimeZoneId ?? ""], Margin, PageWidth - Margin, footerY, 8, secondary);
        fonts.DrawAligned(canvas, localizer[nameof(AppStrings.MonitoringSlaPdfPage), page], Margin, PageWidth - Margin, footerY, 8, secondary, align: SlaPdfAlign.End);
    }

    /// <summary>Starts a new page unless <paramref name="height"/> more fits on this one.</summary>
    private void Room(float height)
    {
        if (y + height > PageHeight - Margin - FooterHeight)
        {
            NewPage();
        }
    }

    private void Header()
    {
        // The logo and the name where a line starts, what this is where it ends.
        var logoHeight = 18f;
        var logoWidth = 0f;

        if (logo is not null && SKImage.FromEncodedData(logo) is { } image)
        {
            using (image)
            {
                logoWidth = logoHeight * image.Width / image.Height;
                canvas.DrawImage(image, Box(0, logoWidth, y, logoHeight), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), null);
            }
        }

        var nameX = logoWidth > 0 ? logoWidth + 7 : 0;
        DrawAt("bit Fabric", nameX, ContentWidth - nameX, y + 14, 13, ink, bold: true);
        fonts.DrawAligned(canvas, localizer[nameof(AppStrings.MonitoringSlaReport)], Margin, PageWidth - Margin, y + 13, 10, secondary, align: SlaPdfAlign.End);

        y += 28;
        Line(Margin, y, PageWidth - Margin, y, border);
        y += 26;

        foreach (var line in fonts.Wrap(report.ProjectName ?? "", 22, ContentWidth, bold: true))
        {
            fonts.DrawAligned(canvas, line, Margin, PageWidth - Margin, y + 18, 22, text, bold: true);
            y += 28;
        }

        var month = report.IsCurrent ? localizer[nameof(AppStrings.MonitoringSlaSoFar), MonthName] : MonthName;
        fonts.DrawAligned(canvas, month, Margin, PageWidth - Margin, y + 14, 13, ink, bold: true);
        y += 22;

        Paragraph(localizer[nameof(AppStrings.MonitoringSlaPdfMonthRuns), report.Address ?? "",
                            report.From.ToOffset(zone.GetUtcOffset(report.From)).ToString("d", culture),
                            report.To.ToOffset(zone.GetUtcOffset(report.To)).ToString("d", culture),
                            report.TimeZoneId ?? ""], 9.5f, secondary);
        y += 14;
    }

    private void Figures()
    {
        const float height = 84;
        const float gap = 10;
        var width = (ContentWidth - 2 * gap) / 3;
        Room(height);

        var availability = report.App.Availability;
        var met = availability >= Target;

        Card(0, width, height, met ? success : availability is null ? border : error, localizer[nameof(AppStrings.MonitoringSlaAvailability)],
             Percent(availability), met ? success : availability is null ? secondary : error,
             availability is null ? localizer[nameof(AppStrings.MonitoringSlaNothingCountedYet)] : met ? localizer[nameof(AppStrings.MonitoringSlaMeetsYourPromise), Percent(Target)] : localizer[nameof(AppStrings.MonitoringSlaBelowYourPromise), Percent(Target)]);

        var budgetLeft = report.BudgetMinutes <= 0 ? 0 : (float)(report.LeftMinutes / report.BudgetMinutes);
        var meterColor = budgetLeft > 0.5f ? success : budgetLeft > 0.2f ? warning : error;
        Card(width + gap, width, height, border, localizer[nameof(AppStrings.MonitoringSlaDowntimeYouCanStillAfford)], Minutes(report.LeftMinutes), text,
             localizer[nameof(AppStrings.MonitoringSlaUsedOf), Minutes(report.App.WeightedMinutes), Minutes(report.BudgetMinutes)], meter: budgetLeft, meterColor: meterColor);

        var credit = report.Credit;
        Card(2 * (width + gap), width, height, credit is null ? border : error, localizer[nameof(AppStrings.MonitoringSlaServiceCredit)],
             credit is null ? localizer[nameof(AppStrings.MonitoringSlaNoCredit)] : localizer[nameof(AppStrings.MonitoringSlaCreditBack), Number(credit.CreditPercent)], credit is null ? text : error,
             credit is not null ? localizer[nameof(AppStrings.MonitoringSlaBelowInYourContract), Percent(credit.BelowPercent)]
             : report.Settings.Credits.Length is 0 ? localizer[nameof(AppStrings.MonitoringSlaCreditsNotSet)] : localizer[nameof(AppStrings.MonitoringSlaAboveEveryTier)]);

        y += height + 18;
    }

    private void Card(float x, float width, float height, SKColor edge, string label, string value, SKColor valueColor, string note, float? meter = null, SKColor meterColor = default)
    {
        var box = Box(x, width, y, height);
        using (var fill = new SKPaint { Color = SKColors.White, IsAntialias = true })
        using (var stroke = new SKPaint { Color = edge, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
        {
            canvas.DrawRoundRect(box, 10, 10, fill);
            canvas.DrawRoundRect(box, 10, 10, stroke);
        }

        var inner = 12f;
        DrawAt(label, x + inner, width - 2 * inner, y + 18, 8.5f, secondary);
        DrawAt(value, x + inner, width - 2 * inner, y + 44, 20, valueColor, bold: true);

        if (meter is { } share)
        {
            var track = Box(x + inner, width - 2 * inner, y + 53, 5);
            Fill(track, surface, 2.5f);
            var filled = Box(x + inner, (width - 2 * inner) * Math.Clamp(share, 0, 1), y + 53, 5);
            // A meter fills from where a line starts.
            Fill(filled, meterColor, 2.5f);
        }

        DrawAt(note, x + inner, width - 2 * inner, y + 72, 8.5f, secondary);
    }

    private void Formula()
    {
        Heading(localizer[nameof(AppStrings.MonitoringSlaHowItsCounted)]);

        var settings = report.Settings;
        Paragraph(localizer[nameof(AppStrings.MonitoringSlaPdfFormula), Number(settings.PartialWeight), Number(settings.DegradedWeight)], 10, ink, bold: true);
        Paragraph(localizer[nameof(AppStrings.MonitoringSlaPdfHowItsCounted),
                            Number(settings.PartialWeight * 100), Number(settings.DegradedWeight * 100)], 9, secondary);
        Paragraph(localizer[nameof(AppStrings.MonitoringSlaPdfThisMonth),
                            Minutes(report.App.CountedMinutes), Minutes(report.App.DownMinutes), Minutes(report.App.PartialMinutes), Minutes(report.App.DegradedMinutes)], 9, secondary);
        y += 12;
    }

    private void Parts()
    {
        Heading(localizer[nameof(AppStrings.MonitoringSlaEachPart)]);

        const float column = 78;
        var nameWidth = ContentWidth - 3 * column;

        Room(22);
        DrawAt(localizer[nameof(AppStrings.MonitoringSlaPartAsYourAppCallsIt)], 0, nameWidth, y + 10, 8.5f, secondary);
        DrawAt(localizer[nameof(AppStrings.MonitoringSlaAvailability)], nameWidth, column, y + 10, 8.5f, secondary, align: SlaPdfAlign.End);
        DrawAt(localizer[nameof(AppStrings.MonitoringSlaDown)], nameWidth + column, column, y + 10, 8.5f, secondary, align: SlaPdfAlign.End);
        DrawAt(localizer[nameof(AppStrings.MonitoringSlaPartlyDown)], nameWidth + 2 * column, column, y + 10, 8.5f, secondary, align: SlaPdfAlign.End);
        y += 16;
        Line(Margin, y, PageWidth - Margin, y, border);

        foreach (var row in report.Parts.Prepend(report.App))
        {
            Room(22);
            var isApp = row == report.App;
            var title = Ellipsize(isApp ? localizer[nameof(AppStrings.MonitoringSlaAsAWhole), row.Title ?? ""] : row.Title ?? row.Key ?? "", nameWidth - 8, 10, isApp);

            DrawAt(title, 0, nameWidth - 8, y + 15, 10, text, bold: isApp);
            DrawAt(Percent(row.Availability), nameWidth, column, y + 15, 10, row.Availability is null ? secondary : row.Availability >= Target ? success : error, bold: true, align: SlaPdfAlign.End);
            DrawAt(row.DownMinutes > 0 ? Minutes(row.DownMinutes) : "—", nameWidth + column, column, y + 15, 10, text, align: SlaPdfAlign.End);
            DrawAt(row.PartialMinutes > 0 ? Minutes(row.PartialMinutes) : "—", nameWidth + 2 * column, column, y + 15, 10, text, align: SlaPdfAlign.End);
            y += 22;
            Line(Margin, y, PageWidth - Margin, y, border);
        }

        y += 18;
    }

    private void Months()
    {
        const float chartHeight = 92;
        Heading(localizer[nameof(AppStrings.MonitoringSlaLast12Months)]);
        Room(chartHeight + 20);

        var known = report.Months.Where(month => month.Availability is not null).Select(month => month.Availability!.Value).ToArray();
        var floor = Math.Min(Target - (100 - Target) * 2, known.Length is 0 ? Target : known.Min()) - 0.01;
        double HeightOf(double value) => Math.Clamp((value - floor) / (100 - floor), 0.02, 1);

        // Time runs left to right in every language, as on any chart (See ProjectHistory).
        var gap = 6f;
        var barWidth = (ContentWidth - gap * (report.Months.Length - 1)) / Math.Max(1, report.Months.Length);
        var bottom = y + chartHeight;

        for (var i = 0; i < report.Months.Length; i++)
        {
            var month = report.Months[i];
            var left = Margin + i * (barWidth + gap);

            if (month.Availability is { } value)
            {
                var top = bottom - (float)HeightOf(value) * chartHeight;
                var color = value >= Target ? success : error;
                Fill(new SKRect(left, top, left + barWidth, bottom), month.Year == report.Year && month.Month == report.Month ? color : color.WithAlpha(150), 3);
            }
            else
            {
                Fill(new SKRect(left, bottom - 3, left + barWidth, bottom), surface, 1.5f);
            }

            var label = ProjectSlaMonths.ShortNameOf(month.Year, month.Month, culture);
            var labelWidth = fonts.Measure(label, 7.5f);
            fonts.Draw(canvas, label, left + (barWidth - labelWidth) / 2, bottom + 11, 7.5f, secondary);
        }

        var targetY = bottom - (float)HeightOf(Target) * chartHeight;
        using (var dashed = new SKPaint { Color = ink, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, PathEffect = SKPathEffect.CreateDash([4, 3], 0) })
        {
            canvas.DrawLine(Margin, targetY, PageWidth - Margin, targetY, dashed);
        }

        y = bottom + 20;
        Paragraph(localizer[nameof(AppStrings.MonitoringSlaPdfMonthsHelp), Percent(Target)], 8.5f, secondary);
        y += 12;
    }

    private void Credits()
    {
        Heading(localizer[nameof(AppStrings.MonitoringSlaServiceCreditsInYourContract)]);

        if (report.Settings.Credits.Length is 0)
        {
            Paragraph(localizer[nameof(AppStrings.MonitoringSlaPdfNoCredits)], 9, secondary);
            y += 12;
            return;
        }

        foreach (var credit in report.Settings.Credits.OrderByDescending(credit => credit.BelowPercent))
        {
            Room(24);
            var applies = report.Credit is { } applied && applied.BelowPercent == credit.BelowPercent;
            var box = Box(0, ContentWidth, y, 20);
            Fill(box, applies ? error.WithAlpha(26) : surface, 6);
            DrawAt(localizer[nameof(AppStrings.MonitoringSlaBelow), Percent(credit.BelowPercent)], 10, ContentWidth / 2, y + 14, 9.5f, applies ? error : text, bold: applies);
            DrawAt(localizer[nameof(AppStrings.MonitoringSlaCreditBack), Number(credit.CreditPercent)], ContentWidth / 2, ContentWidth / 2 - 10, y + 14, 9.5f, applies ? error : text, bold: applies, align: SlaPdfAlign.End);
            y += 24;
        }

        y += 14;
    }

    private void LeftOut()
    {
        Heading(localizer[nameof(AppStrings.MonitoringSlaLeftOutOfTheCount)]);

        if (report.Maintenance.Length is 0)
        {
            Paragraph(localizer[nameof(AppStrings.MonitoringSlaNoMaintenanceThisMonth)], 9, secondary);
        }

        foreach (var maintenance in report.Maintenance)
        {
            Room(30);
            DrawAt(maintenance.Title ?? "", 0, ContentWidth, y + 11, 10, text, bold: true);
            DrawAt(localizer[nameof(AppStrings.MonitoringSlaPdfMaintenanceLeftOut), LocalTime(maintenance.StartsAt), LocalTime(maintenance.EndsAt), report.TimeZoneId ?? "", Minutes(maintenance.Minutes)],
                   0, ContentWidth, y + 24, 8.5f, secondary);
            y += 32;
        }

        if (report.NoDataMinutes >= 1)
        {
            Paragraph(localizer[nameof(AppStrings.MonitoringSlaNoReading), Minutes(report.NoDataMinutes)], 9, secondary);
        }

        if (report.NotWatchedMinutes >= 1)
        {
            Paragraph(localizer[nameof(AppStrings.MonitoringSlaBeforeWatched), Minutes(report.NotWatchedMinutes)], 9, secondary);
        }
    }

    private void Heading(string heading)
    {
        Room(40);
        DrawAt(heading, 0, ContentWidth, y + 12, 12, ink, bold: true);
        y += 22;
    }

    /// <summary>Wrapped text from the line's start, moving down.</summary>
    private void Paragraph(string paragraph, float size, SKColor color, bool bold = false)
    {
        foreach (var line in fonts.Wrap(paragraph, size, ContentWidth, bold))
        {
            Room(size * 1.6f);
            fonts.DrawAligned(canvas, line, Margin, PageWidth - Margin, y + size, size, color, bold);
            y += size * 1.55f;
        }
    }

    /// <summary>
    /// Draws in a box <paramref name="x"/> from where the language starts a line and <paramref name="width"/> wide, smaller
    /// when it is wider than the box: a card's or a column's words, and a duration, run longer in most languages than in
    /// English ("23 Std. 59 Min.").
    /// </summary>
    private void DrawAt(string value, float x, float width, float baseline, float size, SKColor color, bool bold = false, SlaPdfAlign align = SlaPdfAlign.Start)
    {
        var box = Box(x, width, 0, 0);
        var measured = fonts.Measure(value, size, bold);
        fonts.DrawAligned(canvas, value, box.Left, box.Right, baseline, measured > width ? size * width / measured : size, color, bold, align);
    }

    /// <summary>A box <paramref name="x"/> from where the language starts a line: the left edge, or the right in Persian and Arabic.</summary>
    private SKRect Box(float x, float width, float top, float height)
    {
        var left = fonts.IsRightToLeft ? PageWidth - Margin - x - width : Margin + x;
        return new(left, top, left + width, top + height);
    }

    private void Fill(SKRect rect, SKColor color, float radius)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawRoundRect(rect, radius, radius, paint);
    }

    private void Line(float x0, float y0, float x1, float y1, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true, StrokeWidth = 0.75f };
        canvas.DrawLine(x0, y0, x1, y1, paint);
    }

    private string Ellipsize(string value, float width, float size, bool bold)
    {
        if (fonts.Measure(value, size, bold) <= width)
            return value;

        while (value.Length > 1 && fonts.Measure($"{value}…", size, bold) > width)
        {
            value = value[..^1];
        }

        return $"{value}…";
    }

    /// <summary>"99.95%", never rounded up to a promise it didn't meet.</summary>
    private string Percent(double? value) => value is { } percent ? $"{(Math.Floor(percent * 1000) / 1000).ToString("0.0##", culture)}%" : "—";

    private string Number(double value) => value.ToString("0.##", culture);

    private string Minutes(double minutes)
    {
        var rounded = (int)Math.Round(minutes);

        return rounded switch
        {
            < 60 => localizer[nameof(AppStrings.MonitoringDurationMinutes), rounded],
            >= 24 * 60 when rounded % (24 * 60) < 60 => localizer[nameof(AppStrings.MonitoringDurationDays), rounded / (24 * 60)],
            _ => ProjectTexts.Duration(localizer, TimeSpan.FromMinutes(rounded))
        };
    }

    private string LocalTime(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, zone).ToString("g", culture);
}
