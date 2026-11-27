using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// The Troubleshoot button's prompt for one of the team's environments (See ProjectTroubleshootPrompt), read through the
/// tenant filter as its page is: another team's environment is simply not found.
/// </summary>
public partial class ProjectTroubleshootPrompts(AppDbContext dbContext, MonitoringsService monitoringsService)
{
    public async Task<TroubleshootPromptDto> For(Guid monitoringId, CancellationToken cancellationToken)
    {
        var details = await monitoringsService.GetMonitoring(monitoringId, cancellationToken);
        var project = await dbContext.Monitorings.AsNoTracking().Include(p => p.Connection).FirstAsync(p => p.Id == monitoringId, cancellationToken);
        var connection = project.Connection;

        return ProjectTroubleshootPrompt.For(new(details.Monitoring?.ProjectName ?? project.Name,
                                                 project.Environment,
                                                 project.Address,
                                                 project.ServerAddress,
                                                 project.TemplateVersion,
                                                 project.ConnectionStatus,
                                                 project.ConnectionProblem,
                                                 connection?.Issuer,
                                                 connection?.TokenEndpoint,
                                                 connection?.Resource,
                                                 connection?.ReportFailingSince), details);
    }
}
