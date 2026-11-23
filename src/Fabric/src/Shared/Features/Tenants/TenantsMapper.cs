using Bit.Fabric.Shared.Features.Tenants.Dtos;

namespace Bit.Fabric.Shared.Features.Tenants;

[Mapper(UseDeepCloning = true)]
public static partial class TenantsMapper
{
    public static partial void Patch(this TenantDto source, TenantDto destination);
}
