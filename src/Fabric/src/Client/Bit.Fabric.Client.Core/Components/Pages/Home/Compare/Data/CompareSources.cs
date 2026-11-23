namespace Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

/// <summary>
/// Every page the comparisons cite, under its id in the go-to-market research
/// (bitfabric-internal/.agents/gtm/research/competitors.md, its Sources table), each read on 19 November 2026. Names and addresses aren't translated: they are what a reader finds at the
/// address. A source that no fact cites any more can stay here; a page lists only the ones it cites.
/// </summary>
public static class CompareSources
{
    public static IReadOnlyDictionary<string, CompareSource> All { get; } = new CompareSource[]
    {
        // Lovable
        new("LOV-1", "Lovable documentation: Subscription plans", ["https://docs.lovable.dev/introduction/subscription-plans"]),
        new("LOV-3", "Lovable documentation: Low credit balance and out-of-credits recovery", ["https://docs.lovable.dev/introduction/out-of-credits"]),
        new("LOV-4", "Lovable documentation: Deployment, hosting and ownership", ["https://docs.lovable.dev/tips-tricks/deployment-hosting-ownership"]),
        new("LOV-5", "Lovable documentation: External deployment and hosting", ["https://docs.lovable.dev/tips-tricks/external-deployment-hosting"]),
        new("LOV-7", "Lovable documentation: Lovable Cloud", ["https://docs.lovable.dev/features/cloud"]),
        new("LOV-9", "Lovable documentation index", ["https://docs.lovable.dev/llms.txt"]),
        new("LOV-14", "Lovable pricing", ["https://lovable.dev/pricing"]),

        // Bolt
        new("BOLT-1", "Bolt pricing", ["https://bolt.new/pricing"]),
        new("BOLT-2", "Bolt help center: Tokens", ["https://support.bolt.new/account/tokens"]),
        new("BOLT-4", "Bolt help center: Hosting plans", ["https://support.bolt.new/bolt-cloud/hosting/plans"]),
        new("BOLT-5", "Bolt help center: Bolt Database, advanced", ["https://support.bolt.new/bolt-cloud/database/advanced"]),
        new("BOLT-6", "Bolt help center: Expo", ["https://support.bolt.new/integrations/expo"]),
        new("BOLT-7", "Bolt help center index", ["https://support.bolt.new/llms.txt"]),

        // Base44
        new("B44-1", "Base44 pricing", ["https://base44.com/pricing"]),
        new("B44-2", "Base44 documentation: GitHub", ["https://docs.base44.com/developers/app-code/local-development/github"]),
        new("B44-3", "Base44 documentation: eject", ["https://docs.base44.com/developers/references/cli/commands/eject"]),
        new("B44-4", "Base44 documentation: what its built-in services cost", ["https://docs.base44.com/developers/backend/overview/pricing"]),
        new("B44-5", "Base44 documentation: Uploading to app stores", ["https://docs.base44.com/documentation/building-your-app/uploading-to-app-stores"]),
        new("B44-6", "Base44 documentation: Local development", ["https://docs.base44.com/developers/backend/overview/local-dev/local-development-overview"]),
        new("B44-7", "Base44 privacy policy", ["https://base44.com/privacy-policy"]),

        // Replit
        new("REP-1", "Replit pricing", ["https://replit.com/pricing"]),
        new("REP-2", "Replit documentation: Pricing and plans", ["https://docs.replit.com/help/pricing-and-plans"]),
        new("REP-3", "Replit documentation: AI billing", ["https://docs.replit.com/billing/ai-billing"]),
        new("REP-4", "Replit documentation: Starter plan", ["https://docs.replit.com/billing/plans/starter-plan"]),
        new("REP-5", "Replit documentation: Managing spend", ["https://docs.replit.com/billing/managing-spend"]),
        new("REP-7", "Replit documentation: Bring your own cloud", ["https://docs.replit.com/teams/bring-your-own-cloud"]),
        new("REP-8", "Replit documentation: August cloud billing updates", ["https://docs.replit.com/billing/aug-cloud-billing-updates"]),
        new("REP-9", "Replit documentation: Mobile apps and publishing to iOS", ["https://docs.replit.com/build/mobile-app", "https://docs.replit.com/build/mobile-publish-ios"]),
        new("REP-10", "Replit documentation: Connect your repository", ["https://docs.replit.com/build/code/connect-your-repository"]),
        new("REP-13", "Replit documentation: Database", ["https://docs.replit.com/help/database"]),
        new("INC-7", "Replit blog: A safer way to vibe code with Replit databases", ["https://replit.com/blog/introducing-a-safer-way-to-vibe-code-with-replit-databases"]),

        // v0
        new("V0-1", "v0 pricing", ["https://v0.app/pricing"]),
        new("V0-2", "v0 documentation", ["https://v0.app/docs/llms.txt"]),

        // The AI a builder brings
        new("AG-1", "Claude pricing", ["https://claude.com/pricing"]),
        new("AG-5", "GitHub Copilot plans", ["https://github.com/features/copilot/plans"]),
        new("AG-8", "ChatGPT pricing", ["https://learn.chatgpt.com/docs/pricing"]),
        new("AG-10", "Google Developers Blog: Transitioning Gemini CLI to Antigravity CLI", ["https://developers.googleblog.com/an-important-update-transitioning-gemini-cli-to-antigravity-cli/"]),

        // Starter kits
        new("KIT-1", "ShipFast and its license", ["https://shipfa.st/", "https://shipfa.st/license"]),
        new("KIT-2", "Makerkit pricing and license", ["https://makerkit.dev/pricing", "https://makerkit.dev/license"]),
        new("KIT-3", "supastarter and its license", ["https://supastarter.dev/", "https://supastarter.dev/legal/license"]),

        // What happened to AI-built apps
        new("INC-4", "UpGuard: Systemic data exposure in Supabase apps", ["https://www.upguard.com/blog/everything-everywhere-systemic-data-exposure-in-supabase-apps"])
    }.ToDictionary(source => source.Id);
}
