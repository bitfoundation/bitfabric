using Bit.Fabric.Server.Api.Features.Identity.OAuth.Services;
using Bit.Fabric.Shared.Features.Statistics;
using Fido2NetLib;

namespace Bit.Fabric.Server.Api.Infrastructure.Services;

/// <summary>
/// https://devblogs.microsoft.com/dotnet/try-the-new-system-text-json-source-generator/
/// </summary>
[JsonSourceGenerationOptions(
  AllowTrailingCommas = true,
  PropertyNameCaseInsensitive = true,
  GenerationMode = JsonSourceGenerationMode.Default,
  PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
)]
[JsonSerializable(typeof(NugetStatsDto))]
[JsonSerializable(typeof(CloudflarePurgeResponse))]
[JsonSerializable(typeof(AuthenticatorResponse))]
[JsonSerializable(typeof(AuthenticatorAssertionRawResponse))]
[JsonSerializable(typeof(ClientIdMetadataDocument))]
public partial class ServerJsonContext : JsonSerializerContext
{
}
