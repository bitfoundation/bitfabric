using Bit.Fabric.Client.Windows.Infrastructure.Services;

namespace Bit.Fabric.Client.Windows.Components.Pages;

public partial class AboutPage
{
    [AutoInject] private ITelemetryContext telemetryContext = default!;


    private string appVersion = default!;
    private string platform = default!;
    private string webView = default!;
    private string processId = default!;
    private string? signer;


    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        appVersion = telemetryContext.AppVersion!;
        platform = telemetryContext.Platform!;
        webView = telemetryContext.WebView!;
        processId = Environment.ProcessId.ToString();
        signer = Environment.ProcessPath is { } processPath ? WindowsCodeSignature.GetVerifiedSigner(processPath) : null;
    }
}
