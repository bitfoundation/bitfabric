using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Bit.Fabric.Shared.Features.Diagnostic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Bit.Fabric.Server.Shared.Infrastructure.Services;

/// <summary>
/// The part of <see cref="DeploymentConfigurationDto"/> both server projects have. A standalone api and Server.Web are
/// two processes with two configurations, so each of them answers for itself.
/// </summary>
public static class DeploymentConfigurationReader
{
    public static DeploymentConfigurationDto ReadShared(IConfiguration configuration, ServerSharedSettings settings, IHostEnvironment environment, Assembly assembly)
    {
        var rendering = configuration.GetSection("WebAppRender");

        return new()
        {
            InstanceName = System.Environment.MachineName,
            Environment = environment.EnvironmentName,
            ApplicationVersion = assembly.GetName().Version?.ToString(3),
            TimeZone = TimeZoneInfo.Local.Id,
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.ProcessArchitecture})",
            ProcessorCount = System.Environment.ProcessorCount,
            // Not the machine's memory: in a container this is the cgroup limit the process is killed for crossing.
            AvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            UsedMemoryBytes = System.Environment.WorkingSet,
            Uptime = DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            SupportedCultures = CultureInfoManager.InvariantGlobalization
                ? []
                : [.. CultureInfoManager.SupportedCultures.Select(culture => culture.Culture.Name)],
            TrustedOrigins = settings.TrustedOrigins,
            TrustedOriginsRegex = settings.TrustedOriginsRegex().ToString(),
            ForwardedHeadersConfigured = configuration.GetSection("ForwardedHeaders").Exists(),

            OutputCachingEnabled = settings.ResponseCaching?.EnableOutputCaching is true,
            CdnEdgeCachingEnabled = settings.ResponseCaching?.EnableCdnEdgeCaching is true,

            // Null in a standalone api, which renders nothing; Server.Web answers for it there.
            BlazorMode = rendering.Exists() ? rendering["BlazorMode"] : null,
            PrerenderEnabled = rendering.GetValue<bool>("PrerenderEnabled"),

            OtlpExporterConfigured = string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]) is false
                                     || string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"]) is false
        };
    }
}
