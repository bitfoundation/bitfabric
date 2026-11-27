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
    private bool failed;
    /// <summary>Counts the loads, so an answer a later load replaced, after a close and reopen, is dropped.</summary>
    private int loads;

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
        await Load();
    }

    private async Task Load()
    {
        var load = ++loads;
        prompt = null;
        failed = false;
        isCopied = false;

        try
        {
            var loaded = await monitoringsController.GetTroubleshootPrompt(MonitoringId, CurrentCancellationToken);
            if (load == loads)
            {
                prompt = loaded;
            }
        }
        catch
        {
            // The app's error handler says what went wrong; the dialog offers to try again instead of loading forever.
            if (load == loads)
            {
                failed = true;
            }
            throw;
        }
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
