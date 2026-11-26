namespace Bit.Fabric.Client.Core.Components.Layout.AiChat;

public interface IAppAiChat
{
    Task SendPrompt(string prompt);

    /// <summary>Starts an evaluation of bit Fabric for the visitor's business with <paramref name="prompt"/> (See AiChatProtocol.Evaluation).</summary>
    Task Evaluate(string prompt);

    Task Clear();

    Task OnPanelClosed();
}
