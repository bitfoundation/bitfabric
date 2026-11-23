using Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;

/// <summary>
/// Every page the AI agent pages cite, each read on 21 November 2026 (<see cref="ForShared.CheckedOn"/>): the agents' and the
/// models' own documentation and price pages, and nothing written about them by others. Names and addresses aren't translated:
/// they are what a reader finds at the address. A page lists only the ones it cites.
/// </summary>
public static class ForSources
{
    public static IReadOnlyDictionary<string, CompareSource> All { get; } = new CompareSource[]
    {
        // GitHub Copilot
        new("GH-PLANS", "GitHub Copilot plans", ["https://github.com/features/copilot/plans"]),
        new("GH-SKILLS", "GitHub Docs: About agent skills", ["https://docs.github.com/en/copilot/concepts/agents/about-agent-skills"]),
        new("GH-BYOK", "GitHub Docs: Bring your own key (BYOK)", ["https://docs.github.com/en/copilot/concepts/models/bring-your-own-key"]),
        new("GH-BYOK-VSC", "GitHub Changelog: Bring your own language model key in VS Code now available", ["https://github.blog/changelog/2026-04-22-bring-your-own-language-model-key-in-vs-code-now-available/"]),
        new("GH-CLI-BYOK", "GitHub Docs: Using your own LLM models in GitHub Copilot CLI", ["https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/use-byok-models"]),
        new("VSC-INSTR", "VS Code documentation: Custom instructions", ["https://code.visualstudio.com/docs/agent-customization/custom-instructions"]),
        new("VSC-SKILLS", "VS Code documentation: Use Agent Skills in VS Code", ["https://code.visualstudio.com/docs/agent-customization/agent-skills"]),
        new("VSC-AGENTS", "VS Code documentation: Custom agents", ["https://code.visualstudio.com/docs/agent-customization/custom-agents"]),
        new("VSC-MCP", "VS Code documentation: MCP configuration", ["https://code.visualstudio.com/docs/agents/reference/mcp-configuration"]),
        new("VSC-MODELS", "VS Code documentation: AI language models in VS Code", ["https://code.visualstudio.com/docs/agent-customization/language-models"]),

        // Claude Code
        new("CC-DOCS", "Claude Code documentation", ["https://code.claude.com/docs/llms.txt"]),
        new("CC-MEM", "Claude Code documentation: How Claude remembers your project", ["https://code.claude.com/docs/en/memory"]),
        new("CC-SKILLS", "Claude Code documentation: Extend Claude with skills", ["https://code.claude.com/docs/en/skills"]),
        new("CC-MCP", "Claude Code documentation: Connect Claude Code to tools via MCP", ["https://code.claude.com/docs/en/mcp"]),
        new("CC-DIR", "Claude Code documentation: Explore the .claude directory", ["https://code.claude.com/docs/en/claude-directory"]),
        new("CL-PRICING", "Claude pricing", ["https://claude.com/pricing"]),

        // Codex
        new("CX-AGENTS", "Codex documentation: Custom instructions with AGENTS.md", ["https://learn.chatgpt.com/docs/agent-configuration/agents-md"]),
        new("CX-SKILLS", "Codex documentation: Build skills", ["https://learn.chatgpt.com/docs/build-skills"]),
        new("CX-CONFIG", "Codex documentation: Config basics", ["https://learn.chatgpt.com/docs/config-file/config-basic"]),
        new("CX-PRICING", "Codex pricing", ["https://learn.chatgpt.com/docs/pricing"]),

        // Cursor
        new("CU-RULES", "Cursor documentation: Rules", ["https://cursor.com/docs/rules"]),
        new("CU-SKILLS", "Cursor documentation: Agent Skills", ["https://cursor.com/docs/skills"]),
        new("CU-MCP", "Cursor documentation: Model Context Protocol (MCP)", ["https://cursor.com/docs/mcp"]),
        new("CU-PRICING", "Cursor documentation: Models and pricing", ["https://cursor.com/docs/models-and-pricing"]),

        // Antigravity
        new("AGY-RULES", "Antigravity documentation: Rules", ["https://antigravity.google/docs/rules"]),
        new("AGY-SKILLS", "Antigravity documentation: Agent skills", ["https://antigravity.google/docs/skills"]),
        new("AGY-MCP", "Antigravity documentation: MCP", ["https://antigravity.google/docs/mcp"]),
        new("AGY-CLI", "Antigravity documentation: Migrating from Gemini CLI", ["https://antigravity.google/docs/cli/gcli-migration"]),
        new("AGY-PRICING", "Antigravity pricing", ["https://antigravity.google/pricing"]),

        // Gemini CLI
        new("GEM-MD", "Gemini CLI documentation: Provide context with GEMINI.md files", ["https://geminicli.com/docs/cli/gemini-md/"]),
        new("GEM-SKILLS", "Gemini CLI documentation: Agent skills", ["https://geminicli.com/docs/cli/skills/"]),
        new("GEM-CMD", "Gemini CLI documentation: Custom commands", ["https://geminicli.com/docs/cli/custom-commands/"]),
        new("GEM-MCP", "Gemini CLI documentation: MCP servers with Gemini CLI", ["https://geminicli.com/docs/tools/mcp-server/"]),
        new("GEM-MOVE", "Google Developers Blog: Transitioning Gemini CLI to Antigravity CLI", ["https://developers.googleblog.com/an-important-update-transitioning-gemini-cli-to-antigravity-cli/"]),

        // Junie
        new("JN-PLUGIN", "Junie documentation: Junie IDE plugin", ["https://junie.jetbrains.com/docs/junie-ide-plugin.html"]),
        new("JN-SKILLS", "Junie documentation: Agent skills", ["https://junie.jetbrains.com/docs/agent-skills.html"]),
        new("JN-MCP", "Junie documentation: Add and configure MCP servers", ["https://junie.jetbrains.com/docs/junie-cli-mcp-configuration.html"]),
        new("JN-LLM", "Junie documentation: Custom LLMs", ["https://junie.jetbrains.com/docs/custom-llm-models.html"]),

        // Devin Desktop, where windsurf.com leads
        new("DV-CUSTOM", "Devin documentation: Devin Local customizations", ["https://docs.devin.ai/desktop/devin-local/customizations"]),
        new("DV-USAGE", "Devin documentation: Plans and usage", ["https://docs.devin.ai/desktop/accounts/usage"]),
        new("WS-MOVE", "windsurf.com, which leads to Devin Desktop", ["https://windsurf.com/", "https://devin.ai/desktop"]),

        // A model on your computer
        new("OL-CC", "Ollama documentation: Claude Code", ["https://docs.ollama.com/integrations/claude-code"]),
        new("OL-CX", "Ollama documentation: Codex CLI", ["https://docs.ollama.com/integrations/codex"]),
        new("LM-OAI", "LM Studio documentation: OpenAI compatibility endpoints", ["https://lmstudio.ai/docs/developer/openai-compat"]),

        // DeepSeek, Qwen, Kimi, GLM and MiniMax
        new("DS-PRICE", "DeepSeek API documentation: Models and pricing", ["https://api-docs.deepseek.com/quick_start/pricing"]),
        new("DS-CC", "DeepSeek API documentation: Integrate with Claude Code", ["https://api-docs.deepseek.com/quick_start/agent_integrations/claude_code"]),
        new("ALI-CC", "Alibaba Cloud Model Studio: Claude Code", ["https://www.alibabacloud.com/help/en/model-studio/claude-code"]),
        new("KIMI-CC", "Kimi platform documentation: Claude Code", ["https://platform.kimi.ai/docs/guide/claude-code-kimi"]),
        new("ZAI-PLAN", "Z.ai documentation: GLM Coding Plan", ["https://docs.z.ai/devpack/overview"]),
        new("MM-CC", "MiniMax documentation: Claude Code", ["https://platform.minimax.io/docs/coding-plan/claude-code"]),
        new("OR-MODELS", "OpenRouter models", ["https://openrouter.ai/models"]),

        // Every agent
        new("AGENTSMD", "AGENTS.md", ["https://agents.md/"])
    }.ToDictionary(source => source.Id);
}
