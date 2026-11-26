using Bit.Fabric.Shared.Features.Chatbot;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Identity.OAuth.Dtos;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Features.PushNotification;
using Bit.Fabric.Shared.Features.Statistics;
using Bit.Fabric.Shared.Features.Tenants.Dtos;
using Bit.Fabric.Shared.Infrastructure.Dtos.SignalR;

namespace Bit.Fabric.Shared.Infrastructure.Dtos;

/// <summary>
/// https://devblogs.microsoft.com/dotnet/try-the-new-system-text-json-source-generator/
/// </summary>
[JsonSourceGenerationOptions(


  AllowTrailingCommas = true,
  PropertyNameCaseInsensitive = true,
  GenerationMode = JsonSourceGenerationMode.Default,
  PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase

)]


[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(Dictionary<string, string?>))]
[JsonSerializable(typeof(TimeSpan))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Guid[]))]
[JsonSerializable(typeof(IAsyncEnumerable<string>))] // DiagnosticController.PerformDiagnostic
[JsonSerializable(typeof(GitHubStats))]
[JsonSerializable(typeof(NugetStatsDto))]
[JsonSerializable(typeof(AppProblemDetails))]
[JsonSerializable(typeof(HealthReportDto))]
[JsonSerializable(typeof(DeploymentConfigurationDto))]
[JsonSerializable(typeof(OAuthAuthorizeRequestDto))]
[JsonSerializable(typeof(OAuthConsentDto))]
[JsonSerializable(typeof(OAuthApprovalDto))]
[JsonSerializable(typeof(OAuthClientDto))]
[JsonSerializable(typeof(List<OAuthClientDto>))]
[JsonSerializable(typeof(RevokeOAuthClientRequestDto))]
[JsonSerializable(typeof(PushNotificationSubscriptionDto))]

[JsonSerializable(typeof(ProjectDto))]
[JsonSerializable(typeof(List<ProjectDto>))]
[JsonSerializable(typeof(ProjectDetailsDto))]
[JsonSerializable(typeof(ProjectMapDto))]
[JsonSerializable(typeof(ProjectPerformanceRequestDto))]
[JsonSerializable(typeof(ProjectPerformanceDto))]
[JsonSerializable(typeof(ProjectHistoryRequestDto))]
[JsonSerializable(typeof(ProjectHistoryDto))]
[JsonSerializable(typeof(ProjectIncidentDto))]
[JsonSerializable(typeof(ProjectIncidentDto[]))]
[JsonSerializable(typeof(ProjectIncidentUpdateRequestDto))]
[JsonSerializable(typeof(ProjectMaintenanceWindowDto))]
[JsonSerializable(typeof(ProjectMaintenanceWindowDto[]))]
[JsonSerializable(typeof(ProjectAlertSettingsDto))]
[JsonSerializable(typeof(ProjectAlertDto[]))]
[JsonSerializable(typeof(ProjectSettingsDto))]
[JsonSerializable(typeof(StatusPageSettingsDto))]
[JsonSerializable(typeof(ConnectCustomHostnameRequestDto))]
[JsonSerializable(typeof(StatusPostRequestDto))]
[JsonSerializable(typeof(DraftTranslationsRequestDto))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(PublicStatusDto))]
[JsonSerializable(typeof(FollowStatusPageRequestDto))]
[JsonSerializable(typeof(ProjectSlaSettingsDto))]
[JsonSerializable(typeof(ProjectSlaRequestDto))]
[JsonSerializable(typeof(ProjectSlaReportDto))]
[JsonSerializable(typeof(ProjectScansDto))]
[JsonSerializable(typeof(ProjectScanDto))]
[JsonSerializable(typeof(ProjectScanDto[]))]
[JsonSerializable(typeof(ProjectScanHistoryRequestDto))]
[JsonSerializable(typeof(RunProjectScansRequestDto))]
[JsonSerializable(typeof(ProjectScanSettingsDto))]
[JsonSerializable(typeof(ReportScanFindingRequestDto))]
[JsonSerializable(typeof(ProbeAddressRequestDto))]
[JsonSerializable(typeof(AddressProbeResultDto))]
[JsonSerializable(typeof(StartConnectRequestDto))]
[JsonSerializable(typeof(StartConnectResponseDto))]
[JsonSerializable(typeof(CompleteConnectRequestDto))]
[JsonSerializable(typeof(SetProjectLocationsRequestDto))]
[JsonSerializable(typeof(NetworkTargetDto[]))]
[JsonSerializable(typeof(NetworkResultsDto))]
[JsonSerializable(typeof(NetworkResultsResponseDto))]

[JsonSerializable(typeof(TrialLicenseRequestDto))]
[JsonSerializable(typeof(IssueLicenseRequestDto))]
[JsonSerializable(typeof(LicenseResponseDto))]
[JsonSerializable(typeof(IssuedLicenseDto))]

[JsonSerializable(typeof(TenantDto))]
[JsonSerializable(typeof(List<TenantDto>))]
[JsonSerializable(typeof(PagedResponse<TenantDto>))]
[JsonSerializable(typeof(InviteUserToTenantRequestDto))]
[JsonSerializable(typeof(DiagnosticLogDto[]))]
[JsonSerializable(typeof(AiChatMessage))]
[JsonSerializable(typeof(List<SystemPromptDto>))]
[JsonSerializable(typeof(BackgroundJobProgressDto))]
[JsonSerializable(typeof(SynthesizeSpeechRequestDto))]
[JsonSerializable(typeof(TranscribeSpeechResponseDto))]
[JsonSerializable(typeof(StartVoiceCallRequestDto))]
[JsonSerializable(typeof(StartVoiceCallResponseDto))]
public partial class AppJsonContext : JsonSerializerContext
{
}
