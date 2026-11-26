using Bit.Fabric.Shared.Features.Growth;
using Bit.Fabric.Shared.Features.Growth.Dtos;

namespace Bit.Fabric.Server.Api.Features.Growth;

[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]")]
[Authorize(Policy = AppFeatures.System.Growth_View)]
public partial class GrowthController : AppControllerBase, IGrowthController
{
    [AutoInject] private GrowthService growthService = default!;

    [HttpGet]
    public Task<GrowthDataDto> GetData(string? timeZone, CancellationToken cancellationToken) => growthService.GetData(timeZone, cancellationToken);
}
