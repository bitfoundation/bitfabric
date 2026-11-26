namespace Bit.Fabric.Client.Core.Components.Pages.Licensing;

/// <summary>
/// What the license page, and the Licenses page again, give the customer to paste into their own AI (bitfabric-internal/
/// specs/product.md, Once issued, the page leads with a prompt for the customer's own AI): it saves the license the way
/// bit does, checked, and commits it. In English on purpose, as the AI reads it, whatever the page's language.
/// </summary>
public static class LicensePrompt
{
    public static string For(string? projectName, string license)
        => $"""
            Save this bit Fabric license in the {projectName} project: in the project's root folder, run `bit license {license}`, which checks that the license belongs to this project and writes it into bit.json. Don't edit bit.json by hand. Then commit bit.json with the message "Add the project's license".
            """;

    /// <summary>The terminal's way, the same command the prompt asks the AI to run.</summary>
    public static string Command(string license) => $"bit license {license}";
}
