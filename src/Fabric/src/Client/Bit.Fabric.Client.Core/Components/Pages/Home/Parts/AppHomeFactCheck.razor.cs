namespace Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

/// <summary>
/// The home page's fact-check band (bitfabric-internal/specs/home-page.md, "The AI evaluation"): a prompt the visitor copies
/// into their own AI, which connects to bit Fabric's evaluation agent at the api's /evaluate-mcp, or reads the site's
/// llms-full.txt when it can't; and a button that opens bit Fabric's own assistant on the same evaluation.
/// </summary>
public partial class AppHomeFactCheck
{
    [AutoInject] private Clipboard clipboard = default!;

    private bool isOpen;
    private bool isCopied;

    /// <summary>What the visitor pastes into their own AI, with this site's own addresses.</summary>
    private string Prompt
    {
        get
        {
            var site = new Uri(NavigationManager.BaseUri).GetLeftPart(UriPartial.Authority);
            var mcp = new Uri(AbsoluteServerAddress, "evaluate-mcp");

            return Localizer[nameof(AppStrings.AppHomeFactCheckPrompt), site, mcp];
        }
    }

    private void Toggle() => isOpen = isOpen is false;

    private async Task Copy()
    {
        await clipboard.WriteText(Prompt);
        isCopied = true;
    }

    /// <summary>Opens the assistant on the evaluation, as the visitor's first message (See AppAiChatPanel).</summary>
    private void AskTheAssistant()
    {
        PubSubService.Publish(ClientAppMessages.EVALUATE_WITH_AI, Localizer[nameof(AppStrings.AppHomeFactCheckChatPrompt)].Value);
    }
}
