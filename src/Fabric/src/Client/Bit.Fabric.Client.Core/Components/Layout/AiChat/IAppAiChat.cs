namespace Bit.Fabric.Client.Core.Components.Layout.AiChat;

public interface IAppAiChat
{
    Task SendPrompt(string prompt);

    Task Clear();

    Task OnPanelClosed();
}
