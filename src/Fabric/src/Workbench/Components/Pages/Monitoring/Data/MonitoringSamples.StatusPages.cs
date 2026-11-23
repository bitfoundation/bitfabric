using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

public static partial class MonitoringSamples
{
    /// <summary>What a customer's CNAME record points at, once bit Fabric's own host names are set up.</summary>
    public const string CnameTarget = "custom.bitfabric.co";

    /// <summary>The Web Push key a visitor's browser would subscribe with; the follow button shows only where push is set up.</summary>
    public const string PushPublicKey = "BMonitoringSampleKeyForTheWorkbenchOnly-NotARealVapidKey_00000000000000000000000000000";

    public static string PublicUrlOf(StatusPageSettingsDto page)
        => page.CustomHostname is { } hostname && page.CustomHostnameState is "active" ? $"https://{hostname}" : $"https://bitfabric.co/status/{page.Slug}";

    /// <summary>
    /// A status page with the parts of the app as its users know them, named in each language it's written in, on its own
    /// host name in a Cloudflare state or on bit Fabric's address.
    /// </summary>
    private static void SetStatusPage(SampleProject project, bool published, int followers, string? support, string? hostname, string? hostnameState,
        (string English, string Persian, string[] Keys)[] components, string[]? languages = null)
    {
        languages ??= [MonitoringTexts.MainCulture];

        project.StatusPage = new()
        {
            ProjectId = project.Id,
            Slug = project.Key,
            IsPublished = published,
            ShowsResponseTime = published,
            Languages = languages,
            SupportAddress = support,
            Components = [.. components.Select((component, index) => new StatusComponentDto
            {
                Id = MonitoringNoise.IdOf($"{project.Key}:component:{index}"),
                Name = languages.ToDictionary(culture => culture, culture => culture.StartsWith("fa", StringComparison.Ordinal) ? component.Persian : component.English),
                Description = index is 0 && languages.Length > 1
                    ? new() { ["en-US"] = "Opening the app, signing in and everything you do in it.", ["fa-IR"] = "باز کردن اپ، ورود و هر کاری که در آن انجام می‌دهید." }
                    : [],
                CheckKeys = [.. component.Keys.Where(key => project.Checks.Any(check => check.Key == key && check.IsNotConfigured is false))],
                IsShown = true
            })],
            CustomHostname = hostname,
            CustomHostnameState = hostnameState,
            CnameTarget = CnameTarget,
            FollowerCount = followers,
            Version = 1
        };

        project.StatusPage.PublicUrl = PublicUrlOf(project.StatusPage);
    }
}
