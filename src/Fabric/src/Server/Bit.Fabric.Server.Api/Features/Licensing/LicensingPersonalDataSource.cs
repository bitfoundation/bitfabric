using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Fabric.Server.Api.Features.PersonalData;

namespace Bit.Fabric.Server.Api.Features.Licensing;

/// <summary>The licenses this account got for its teams' projects on the license page.</summary>
public partial class LicensingPersonalDataSource : IPersonalDataSource
{
    [AutoInject] private AppDbContext dbContext = default!;

    public string Key => "licenses";

    public string Purpose => "The free licenses you got for your teams' projects, which bit CLI checks in each project's bit.json. A license names the team that holds it, never you; bit Fabric records that you got it.";

    public string Retention => "For as long as the team holds the license. Deleting your account leaves the licenses with your teams, without a record of who got them.";

    public string? Notes => "A license saved in a project's bit.json stays in that project's files and git history, which only you and your team hold.";

    public PersonalDataErasure Erasure => PersonalDataErasure.CascadeFromUser;

    public async Task<JsonNode?> Export(Guid userId, CancellationToken cancellationToken)
    {
        var licenses = await dbContext.IssuedLicenses.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(license => license.IssuedByUserId == userId)
            .OrderBy(license => license.IssuedOn)
            .Select(license => new
            {
                ProjectId = license.Project!.BitProjectId,
                ProjectName = license.Project.Name,
                Team = license.Tenant!.Title ?? license.Tenant.Name,
                license.FirstCommit,
                license.Plan,
                license.IssuedOn,
                license.ExpiresOn,
                license.Document
            })
            .ToArrayAsync(cancellationToken);

        return JsonSerializer.SerializeToNode(licenses, IPersonalDataSource.SerializerOptions);
    }
}
