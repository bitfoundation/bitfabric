using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// A project's SLA tab (bitfabric-internal/specs/monitoring.md, SLA reports; the approved design is
/// bitfabric-internal#1's Workbench OpsSla, bb36f0b): a month's availability against the owner's promise, the downtime
/// still affordable, the last 12 months, the formula with the weights the owner sets, each part of the app, the credits of
/// their contract and what was left out, with the month as a PDF and a CSV.
/// The server counts every figure (See ProjectSlaCalculator), so the tab, the CSV and the PDF never disagree.
/// </summary>
public partial class ProjectSla
{
    /// <summary>The <c>@key</c> of the app's own row, which has no report key.</summary>
    private const string AppRow = "$app";

    private static readonly double[] targets = [99, 99.5, 99.9, 99.95, 99.99];
    private static readonly double[] partialWeights = [0, 0.1, 0.25, 0.3, 0.5, 1];
    private static readonly double[] degradedWeights = [0, 0.1, 0.25, 0.5];

    /// <summary>The tiers a contract commonly has, offered when the owner adds theirs for the first time.</summary>
    private static readonly (double Below, double Credit)[] commonCredits = [(99.9, 10), (99, 25), (95, 50)];

    [Parameter, EditorRequired] public Guid ProjectId { get; set; }

    [AutoInject] private IProjectSlaController slaController = default!;
    [AutoInject] private HttpClient httpClient = default!;
    [AutoInject] private FileSaveService fileSaveService = default!;

    private ProjectSlaSettingsDto? settings;
    private ProjectSlaReportDto? report;

    /// <summary>The credits being edited; null while they aren't.</summary>
    private List<ProjectSlaCreditDto>? credits;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        settings = await slaController.GetSettings(ProjectId, CurrentCancellationToken);
        report = await slaController.GetReport(new() { ProjectId = ProjectId }, CurrentCancellationToken);
    }

    /// <summary>Shows another month: one of the last 12, picked on the chart.</summary>
    private async Task Open(int year, int month)
    {
        report = await slaController.GetReport(new() { ProjectId = ProjectId, Year = year, Month = month }, CurrentCancellationToken);
    }

    /// <summary>Saves a change of the target or a weight, then counts the month again with it.</summary>
    private async Task Save(Action<ProjectSlaSettingsDto> change)
    {
        if (settings is null || report is null) return;

        change(settings);
        await SaveSettings();
    }

    private async Task SaveSettings()
    {
        try
        {
            settings = await slaController.SaveSettings(settings!, CurrentCancellationToken);
        }
        catch
        {
            // Refused, the tab goes back to what the server holds.
            settings = await slaController.GetSettings(ProjectId, CurrentCancellationToken);
            throw;
        }

        report = await slaController.GetReport(new() { ProjectId = ProjectId, Year = report!.Year, Month = report.Month }, CurrentCancellationToken);
    }

    private void EditCredits()
    {
        credits = settings!.Credits.Length > 0
            ? [.. settings.Credits.Select(credit => new ProjectSlaCreditDto { BelowPercent = credit.BelowPercent, CreditPercent = credit.CreditPercent })]
            : [.. commonCredits.Select(credit => new ProjectSlaCreditDto { BelowPercent = credit.Below, CreditPercent = credit.Credit })];
    }

    private void AddCredit()
    {
        var lowest = credits!.Count is 0 ? 100 : credits.Min(credit => credit.BelowPercent);
        credits.Add(new() { BelowPercent = Math.Max(1, Math.Floor(lowest) - 5), CreditPercent = 100 });
    }

    private async Task SaveCredits()
    {
        if (settings is null || credits is null) return;

        settings.Credits = [.. credits];
        await SaveSettings();
        credits = null;
        SnackBarService.Success(Localizer[nameof(AppStrings.MonitoringSaved)]);
    }

    private Task ExportPdf() => Export(IProjectSlaController.ExportPdfUri, "application/pdf", "pdf");

    private Task ExportCsv() => Export(IProjectSlaController.ExportCsvUri, "text/csv", "csv");

    /// <summary>
    /// The month as a file. The HttpClient rather than the generated proxy, which speaks json: the handler chain still
    /// sends the access token and the viewer's language, which the PDF is written in.
    /// </summary>
    private async Task Export(string uri, string contentType, string extension)
    {
        using var response = await httpClient.GetAsync($"{uri}/{ProjectId}/{report!.Year}/{report.Month}", CurrentCancellationToken);

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                       ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                       ?? $"sla-{report.Year:D4}-{report.Month:D2}.{extension}";

        await fileSaveService.Save(fileName, contentType, await response.Content.ReadAsByteArrayAsync(CurrentCancellationToken));
    }

    /// <summary>The usual choices, and the saved value when it isn't one of them.</summary>
    private BitDropdownItem<double>[] Choices(double[] values, double current, bool weight = false)
        => [.. values.Append(current).Distinct().Order().Select(value => new BitDropdownItem<double>
        {
            Text = weight ? $"{Number(value * 100)}%" : Percent(value),
            Value = value
        })];

    private double BudgetLeftPercent => report is null || report.BudgetMinutes <= 0 ? 0 : Math.Clamp(100 * report.LeftMinutes / report.BudgetMinutes, 0, 100);

    /// <summary>A month's bar height between a floor below the promise and 100%, as a percentage of the chart.</summary>
    private string Height(double value)
    {
        var target = settings!.TargetPercent;
        var known = report!.Months.Where(month => month.Availability is not null).Select(month => month.Availability!.Value).DefaultIfEmpty(target).Min();
        var floor = Math.Min(target - (100 - target) * 2, known) - 0.01;

        return Math.Clamp(100 * (value - floor) / (100 - floor), 2, 100).ToString("0.#", CultureInfo.InvariantCulture);
    }

    /// <summary>"99.95%", never rounded up to a promise it didn't meet.</summary>
    private static string Percent(double? value)
        => value is { } percent ? $"{(Math.Floor(percent * 1000) / 1000).ToString("0.0##", CultureInfo.CurrentUICulture)}%" : "—";

    private static string Number(double value) => value.ToString("0.##", CultureInfo.CurrentUICulture);

    private string Minutes(double minutes)
    {
        var rounded = (int)Math.Round(minutes);

        return rounded switch
        {
            < 60 => Localizer[nameof(AppStrings.MonitoringDurationMinutes), rounded],
            < 24 * 60 => Localizer[nameof(AppStrings.MonitoringDurationHoursMinutes), rounded / 60, rounded % 60],
            _ when rounded % (24 * 60) < 60 => Localizer[nameof(AppStrings.MonitoringDurationDays), rounded / (24 * 60)],
            _ => Localizer[nameof(AppStrings.MonitoringDurationDaysHours), rounded / (24 * 60), rounded % (24 * 60) / 60]
        };
    }

    private string LocalTime(DateTimeOffset at) => TimeZoneService.ToLocalTime(at).ToString("g", CultureInfo.CurrentUICulture);
}
