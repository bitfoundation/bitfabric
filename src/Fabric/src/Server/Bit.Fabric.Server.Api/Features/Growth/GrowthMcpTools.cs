using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using Bit.Fabric.Shared.Features.Growth;

namespace Bit.Fabric.Server.Api.Features.Growth;

/// <summary>
/// The growth page's figures as a tool on <c>/mcp</c>, for global admins only, as the page is
/// (<see cref="AppFeatures.System.Growth_View"/>): the same counts, through <see cref="GrowthService"/> and the same
/// <see cref="GrowthReport"/>, so the tool says nothing the page doesn't. The in-app assistant gets it too (See
/// <see cref="AIFunctions"/>), refused there the same way to anyone else.
/// </summary>
[McpServerToolType]
public partial class GrowthMcpTools(GrowthService growthService, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor) : IAsyncDisposable
{
    [McpServerTool(Name = nameof(GetGrowth), Title = "Projects and licenses over time", ReadOnly = true, OpenWorld = false)]
    [Description("bit Fabric's own growth, every team's together, as the global admin's growth page shows it: projects made (by bit new's Trial, or a first License without one), licensed projects, Trial to License (of the Trials that ended in the period, the share whose project holds a License), teams with a License and their people, licensed projects in production, accounts created, each with what the period added and the same length of time before it, and where every Trial is now. Trials bit CLI asked for from CI are left out. Only global admins may call it.")]
    public async Task<JsonElement> GetGrowth(
        [Description("The period: Week (7 days), Month (30 days), Quarter (13 weeks), Year (12 months) or All. Month when left out.")] GrowthPeriod period = GrowthPeriod.Month,
        [Description("The IANA time zone days are counted in, such as \"Asia/Tehran\": the user's own when they say it. UTC when left out.")] string? timeZone = null,
        CancellationToken cancellationToken = default)
    {
        var user = httpContextAccessor.HttpContext?.User ?? new(new ClaimsIdentity());

        if (user.IsAuthenticated() is false || (await authorizationService.AuthorizeAsync(user, AppFeatures.System.Growth_View)).Succeeded is false)
            return JsonSerializer.SerializeToElement(new { Message = "Only bit Fabric's global admins see its growth." }, resultJson);

        var report = GrowthReport.Compute(await growthService.GetData(timeZone, cancellationToken), period, CultureInfo.InvariantCulture.Calendar);

        return JsonSerializer.SerializeToElement(new GrowthSummary(
            report.Today,
            report.Period,
            new(report.Projects, report.ProjectsMade.Now, report.HasBefore ? report.ProjectsMade.Before : null),
            new(report.LicensedProjects, report.ProjectsLicensed.Now, report.HasBefore ? report.ProjectsLicensed.Before : null),
            report.Conversion is { } conversion ? Math.Round(conversion * 100, 1) : null,
            report.ConversionOf,
            report.ConversionIsSoFar,
            report.MedianDaysToLicense,
            new(report.TeamsWithLicense, report.NewTeamsWithLicense.Now, report.HasBefore ? report.NewTeamsWithLicense.Before : null),
            report.PeopleInLicensedTeams,
            new(report.InProduction, report.NewInProduction.Now, report.HasBefore ? report.NewInProduction.Before : null),
            new(report.Accounts, report.NewAccounts.Now, report.HasBefore ? report.NewAccounts.Before : null),
            new(report.TrialsLicensed, report.TrialsRunning, report.TrialsEndingThisWeek, report.TrialsEndedUnlicensed)), resultJson);
    }

    /// <summary>A figure's total today, what the period added, and what the same length of time before it added.</summary>
    public sealed record Figure(int Total, int AddedInPeriod, int? AddedInPeriodBefore);

    public sealed record TrialsNow(int Licensed, int InTrial, int EndingWithinAWeek, int EndedWithoutLicense);

    public sealed record GrowthSummary(DateOnly Today,
        GrowthPeriod Period,
        Figure Projects,
        Figure LicensedProjects,
        double? TrialToLicensePercent,
        int TrialToLicenseOf,
        bool TrialToLicenseIsSoFar,
        double? MedianDaysToLicense,
        Figure TeamsWithLicense,
        int PeopleInLicensedTeams,
        Figure InProduction,
        Figure Accounts,
        TrialsNow Trials);

    /// <summary>The tool as the in-app assistant's function, in a scope of its own, as the monitoring tools are.</summary>
    public static IEnumerable<AIFunction> AIFunctions(IServiceProvider services)
    {
        return typeof(GrowthMcpTools).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => (Method: method, Tool: method.GetCustomAttribute<McpServerToolAttribute>()))
            .Where(tool => tool.Tool is not null)
            .Select(tool => AIFunctionFactory.Create(tool.Method, _ => InScopeOfItsOwn(services), new AIFunctionFactoryOptions { Name = tool.Tool!.Name ?? tool.Method.Name }));
    }

    private AsyncServiceScope? ownScope;

    private static GrowthMcpTools InScopeOfItsOwn(IServiceProvider services)
    {
        var scope = services.CreateAsyncScope();
        var tools = ActivatorUtilities.CreateInstance<GrowthMcpTools>(scope.ServiceProvider);
        tools.ownScope = scope;
        return tools;
    }

    public async ValueTask DisposeAsync()
    {
        if (ownScope is { } scope)
        {
            await scope.DisposeAsync();
        }
    }

    private static readonly JsonSerializerOptions resultJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}
