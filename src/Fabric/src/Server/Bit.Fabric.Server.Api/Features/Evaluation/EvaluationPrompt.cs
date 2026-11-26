namespace Bit.Fabric.Server.Api.Features.Evaluation;

/// <summary>
/// The instructions of an evaluation (bitfabric-internal/specs/home-page.md, "The AI evaluation"): weigh bit Fabric against the
/// visitor's business and needs, honestly, with each claim linked to its page or its code, and end with a verdict. The same
/// words serve the chat panel's evaluation and the agent behind /evaluate-mcp.
/// </summary>
public static class EvaluationPrompt
{
    /// <summary>What the agent does in an evaluation, wherever it is held.</summary>
    public const string Instructions = """
        ## Evaluating bit Fabric for this visitor
        The visitor wants to find out, in under a minute, whether bit Fabric fits their business and is worth starting with. Be a fair, skeptical reviewer, not a salesperson.
        - If you don't know their business or what their app must do, ask for it in one short question first.
        - Check bit Fabric's claims that matter to them against the site's pages below and, where a claim is about what a project has or does, against the code, with the bit platform library tools. Never claim what the pages and the code don't show.
        - Link each claim you rely on to its page on the site or to its code, so the visitor or their own AI can check it.
        - Say plainly what doesn't fit them, what bit Fabric doesn't have, and what it costs them in time, money and skills, as well as what fits.
        - End with a verdict in one line: worth starting with, not for them, or not yet, and why.
        - Keep it short: a few bullet points and the verdict, in the visitor's language.
        """;

    /// <summary>Added in the chat panel, where the visitor can answer the verdict.</summary>
    public const string ChatRecording = $"""
        - Once the visitor has your verdict and has said whether it convinced them, call `{RecordToolName}` once, with an anonymous summary and whether they were convinced.
        """;

    /// <summary>Added behind /evaluate-mcp, where the visitor's own AI asks and nobody answers the verdict.</summary>
    public const string McpRecording = $"""
        - Before you answer, call `{RecordToolName}` once, with an anonymous summary and `unknown` for whether the visitor was convinced.
        - You answer another AI, which passes your answer on to its user: write it for that user.
        """;

    public const string RecordToolName = "RecordEvaluationOutcome";

    /// <summary>What the summary may and may not say, in the tool's own description.</summary>
    public const string RecordToolDescription = """
        Keeps an anonymous record of this evaluation, to learn which kinds of business bit Fabric fits. The summary is two or
        three lines: the kind of business (an industry and its size, never its name), what its app had to do, and the verdict
        and its main reason. Never a person's or a company's name, a product's name, a place, an address, an email, a phone
        number, a web address or any number that could identify them. Call it once per evaluation.
        """;

    /// <summary>The site's pages and the license, as the agent reads them after its instructions.</summary>
    public static string Sources(string? site, string? license) => $"""
        ## bitfabric.co, every page
        The site's own words, each page under its address: where they say more than your instructions above, answer from them. Link a page by its address.

        {site ?? "The site's pages couldn't be read just now: say so, and answer only from what you know for sure."}

        ## The license of every project (LICENSE.md in bitfoundation/bitfabric)
        {license ?? $"The license couldn't be read just now; it is at {SiteContent.LicenseUrl}."}
        """;
}
