namespace Bit.Fabric.Workbench.Components.Pages.ProjectTelemetry;

/// <summary>
/// The tip at the top of a project's telemetry dashboard: a card or an alert is added by asking one's own AI agent, which
/// reaches bit Fabric through MCP, with requests to copy and the way to connect the agent. Closed, it waits behind the
/// page's "Add with your AI" button.
/// </summary>
public partial class TelemetryAskAi
{
    [Parameter] public EventCallback OnClose { get; set; }

    [Parameter] public EventCallback OnConnect { get; set; }
}
