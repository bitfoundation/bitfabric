using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// The dialog of a project's Troubleshoot button (bitfabric-internal/specs/monitoring.md, Monitoring keeps watching when the
/// approval breaks): the prompt the customer copies into the AI that works on their app's code, read fresh each time it
/// opens, since the environment's state changes from minute to minute.
/// </summary>
public partial class ProjectTroubleshoot
{
    [AutoInject] private IMonitoringsController monitoringsController = default!;
    [AutoInject] private Clipboard clipboard = default!;

    private TroubleshootPromptDto? prompt;
    private Guid? loadedFor;
    private bool isCopied;

    [Parameter, EditorRequired] public Guid MonitoringId { get; set; }

    [Parameter] public bool IsOpen { get; set; }

    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    protected override async Task OnParamsSetAsync()
    {
        await base.OnParamsSetAsync();

        if (IsOpen is false)
        {
            loadedFor = null;
            return;
        }

        if (loadedFor == MonitoringId)
            return;

        loadedFor = MonitoringId;
        prompt = null;
        isCopied = false;
        prompt = await monitoringsController.GetTroubleshootPrompt(MonitoringId, CurrentCancellationToken);
    }

    private async Task Copy()
    {
        if (prompt?.Prompt is not { } text)
            return;

        await clipboard.WriteText(text);
        isCopied = true;
    }

    private Task Close() => Changed(false);

    private async Task Changed(bool isOpen)
    {
        IsOpen = isOpen;
        await IsOpenChanged.InvokeAsync(isOpen);
    }
}
