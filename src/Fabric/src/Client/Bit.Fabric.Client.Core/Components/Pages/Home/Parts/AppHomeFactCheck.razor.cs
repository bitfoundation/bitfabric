using System.Text.RegularExpressions;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

/// <summary>
/// The home page's fact-check band (bitfabric-internal/specs/home-page.md, "The AI evaluation"): a prompt the visitor copies
/// into their own AI, which connects to bit Fabric's evaluation agent at the api's /evaluate-mcp, or reads the site's
/// llms-full.txt when it can't; and a button that opens bit Fabric's own assistant on the same evaluation. A common kind of
/// business, picked as an example, fills in both, always as a real project to customize and keep building on.
/// </summary>
public partial class AppHomeFactCheck
{
    [AutoInject] private Clipboard clipboard = default!;

    private bool isOpen;
    private bool isCopied;
    private FactCheckExample? example;

    /// <summary>The kinds of business a visitor most often builds for, each a project meant to last rather than a prototype.</summary>
    private FactCheckExample[] Examples =>
    [
        Example(BitIconName.ShoppingCart, nameof(AppStrings.AppHomeFactCheckStore), nameof(AppStrings.AppHomeFactCheckStoreBusiness), nameof(AppStrings.AppHomeFactCheckStoreNeeds)),
        Example(BitIconName.ContactCard, nameof(AppStrings.AppHomeFactCheckCrm), nameof(AppStrings.AppHomeFactCheckCrmBusiness), nameof(AppStrings.AppHomeFactCheckCrmNeeds)),
        Example(BitIconName.PaymentCard, nameof(AppStrings.AppHomeFactCheckPos), nameof(AppStrings.AppHomeFactCheckPosBusiness), nameof(AppStrings.AppHomeFactCheckPosNeeds)),
        Example(BitIconName.Calendar, nameof(AppStrings.AppHomeFactCheckBookings), nameof(AppStrings.AppHomeFactCheckBookingsBusiness), nameof(AppStrings.AppHomeFactCheckBookingsNeeds)),
        Example(BitIconName.Repair, nameof(AppStrings.AppHomeFactCheckFieldService), nameof(AppStrings.AppHomeFactCheckFieldServiceBusiness), nameof(AppStrings.AppHomeFactCheckFieldServiceNeeds))
    ];

    private FactCheckExample Example(string icon, string name, string business, string needs)
        => new(icon, Localizer[name], Localizer[business], Localizer[needs]);

    /// <summary>What the visitor pastes into their own AI, with this site's own addresses, and the picked example's business.</summary>
    private string Prompt
    {
        get
        {
            var site = new Uri(NavigationManager.BaseUri).GetLeftPart(UriPartial.Authority);
            var mcp = new Uri(AbsoluteServerAddress, "evaluate-mcp");

            string prompt = Localizer[nameof(AppStrings.AppHomeFactCheckPrompt), site, mcp];

            if (example is null)
                return prompt;

            // The prompt's two blanks, in brackets in every language: the business, then what its app must do.
            var blank = 0;
            return Blank().Replace(prompt, match => blank++ switch { 0 => example.Business, 1 => example.Needs, _ => match.Value });
        }
    }

    private void Toggle() => isOpen = isOpen is false;

    /// <summary>Picks an example, or drops it when it is picked again, back to the blanks the visitor fills in.</summary>
    private void Pick(FactCheckExample picked)
    {
        example = example?.Name == picked.Name ? null : picked;
        isCopied = false;
    }

    private async Task Copy()
    {
        await clipboard.WriteText(Prompt);
        isCopied = true;
    }

    /// <summary>Opens the assistant on the evaluation, as the visitor's first message (See AppAiChatPanel).</summary>
    private void AskTheAssistant()
    {
        var question = example is null
            ? Localizer[nameof(AppStrings.AppHomeFactCheckChatPrompt)].Value
            : Localizer[nameof(AppStrings.AppHomeFactCheckExampleChatPrompt), example.Business, example.Needs].Value;

        PubSubService.Publish(ClientAppMessages.EVALUATE_WITH_AI, question);
    }

    [GeneratedRegex(@"\[[^\]\n]+\]")]
    private static partial Regex Blank();
}

/// <summary>A kind of business the fact-check can start from: its chip, and the two lines it fills in.</summary>
public record FactCheckExample(string Icon, string Name, string Business, string Needs);
