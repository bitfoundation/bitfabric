using Bit.Butil;

namespace Bit.Fabric.Client.Web.Components.Pages;

public partial class AboutPage
{
    [AutoInject] private UserAgent userAgent = default!;
    [AutoInject] private ITelemetryContext telemetryContext = default!;


    private string? oem;
    private string? platform;
    private string appVersion = default!;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        appVersion = telemetryContext.AppVersion!;

        // From the user agent rather than ITelemetryContext.Platform, and only once there is a device to ask: while
        // prerendering, the only os this anonymous page could name is the host's, so it names none (See AppInfoCard.Platform).
        // Environment.ProcessId has the same problem and no client-side answer, so it isn't shown at all.
        if (InPrerenderSession) return;

        var userAgentData = await userAgent.Extract();
        oem = userAgentData.Manufacturer;
        platform = string.Join(' ', [userAgentData.Manufacturer, userAgentData.OsName, userAgentData.Name, "browser"]).Trim();
    }
}
