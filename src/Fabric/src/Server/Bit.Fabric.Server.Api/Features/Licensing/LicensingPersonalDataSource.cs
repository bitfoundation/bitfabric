using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Fabric.Server.Api.Features.PersonalData;

namespace Bit.Fabric.Server.Api.Features.Licensing;

/// <summary>The projects this account got licenses for, and the licenses, which name the account.</summary>
public partial class LicensingPersonalDataSource : IPersonalDataSource
{
    [AutoInject] private AppDbContext dbContext = default!;

    public string Key => "licenses";

    public string Purpose => "The free licenses you got for your projects, which bit CLI checks in each project's bit.json, and which name you as their holder.";

    public string Retention => "For as long as your account exists. Deleting your account deletes the licenses issued to it; its projects stay, without an account, so you can license them again.";

    public string? Notes => "A license you saved in a project's bit.json stays in that project's files and git history, which only you hold.";

    public PersonalDataErasure Erasure => PersonalDataErasure.CascadeFromUser;

    public async Task<JsonNode?> Export(Guid userId, CancellationToken cancellationToken)
    {
        var licenses = await dbContext.IssuedLicenses
            .AsNoTracking()
            .Where(license => license.UserId == userId)
            .OrderBy(license => license.IssuedOn)
            .Select(license => new
            {
                license.ProjectId,
                ProjectName = license.Project!.Name,
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
