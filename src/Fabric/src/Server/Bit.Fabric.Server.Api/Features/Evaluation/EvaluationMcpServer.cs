using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.RateLimiting;
using Bit.Fabric.Server.Api.Features.DocsMcp;
using Bit.Fabric.Server.Api.Features.Evaluation.Models;
using Microsoft.Agents.AI;
using ModelContextProtocol.Protocol;

namespace Bit.Fabric.Server.Api.Features.Evaluation;

/// <summary>
/// /evaluate-mcp (bitfabric-internal/specs/home-page.md, "The AI evaluation"): one tool, open to any AI with no sign-in for
/// now, that hands the visitor's business and needs to an agent on this server. The agent reads every page of the site and the
/// license, checks claims in the code through the bit platform library tools, and answers whether bit Fabric fits, each claim
/// linked to where it can be checked. It shares the api's one MCP server registration with /mcp, /dev-mcp and /docs-mcp, each
/// session taking the tools of the path it was opened on (See DevMcpServiceCollectionExtensions).
/// </summary>
public class EvaluationMcpServer
{
    public const string Path = "/evaluate-mcp";

    public const string ToolName = "EvaluateBitFabric";

    /// <summary>How many evaluations one caller gets an hour: each runs a model, which this server pays for.</summary>
    public const int EvaluationsPerHour = 10;

    /// <summary>The longest answer, so one evaluation can't run away.</summary>
    private const int MaxOutputTokens = 3000;

    private readonly PartitionedRateLimiter<HttpContext> limiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitOptionsExtensions.GetPartitionKey(context),
            factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = EvaluationsPerHour, Window = TimeSpan.FromHours(1) }));

    public static readonly Tool Definition = new()
    {
        Name = ToolName,
        Title = "Evaluate bit Fabric for a business",
        Description = """
            Asks bit Fabric's own evaluation agent whether bit Fabric fits a business and is worth starting with. Tell it what
            the business does and what its app must do; it reads every page of bitfabric.co and the license, checks claims in
            the code, and answers in short: what fits, what doesn't, what it costs in time, money and skills, and a verdict,
            each claim linked to the page or the code it rests on, so you can check it yourself. It takes up to a minute.
            Don't send names, emails, phone numbers or anything else that identifies the person or the company.
            """,
        InputSchema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["business"] = new JsonObject { ["type"] = "string", ["description"] = "What the business does and how big it is, without its name, such as \"a 3-person agency building booking apps for clinics\"." },
                ["needs"] = new JsonObject { ["type"] = "string", ["description"] = "What its app must do, on which platforms, and what matters most: budget, privacy, time to launch, skills." },
                ["question"] = new JsonObject { ["type"] = "string", ["description"] = "Optional: a claim or a doubt to check first." }
            },
            ["required"] = new JsonArray("business", "needs")
        }),
        Annotations = new() { ReadOnlyHint = true, DestructiveHint = false, IdempotentHint = false, OpenWorldHint = true }
    };

    /// <summary>Gives a session opened on <see cref="Path"/> its own name, instructions and one tool.</summary>
    public void ConfigureSession(McpServerOptions options)
    {
        options.ServerInfo = new()
        {
            Name = "bitfabric-evaluation",
            Title = "Evaluate bit Fabric",
            Version = typeof(EvaluationMcpServer).Assembly.GetName().Version?.ToString(3) ?? "0.0.1"
        };
        options.ServerInstructions = $"Call {ToolName} with what the user's business does and what its app must do, and pass its answer on to the user. Its claims link to bit Fabric's pages and code: check any of them yourself if the user asks.";
        options.ToolCollection = null; // The chatbot's tools are /mcp's.
        options.Handlers.ListToolsHandler = (request, cancellationToken) => ValueTask.FromResult(new ListToolsResult { Tools = [Definition] });
        options.Handlers.CallToolHandler = (request, cancellationToken) => CallTool(request.Services!, request.Params!, cancellationToken);
    }

    private async ValueTask<CallToolResult> CallTool(IServiceProvider requestServices, CallToolRequestParams request, CancellationToken cancellationToken)
    {
        if (request.Name is not ToolName)
            return DocsMcpToolResults.Failed($"There is no tool named {request.Name}; the only one is {ToolName}.");

        var business = DocsMcpToolResults.Argument(request, "business");
        var needs = DocsMcpToolResults.Argument(request, "needs");

        if (business is null || needs is null)
            return DocsMcpToolResults.Failed("Say what the business does in `business` and what its app must do in `needs`.");

        var httpContext = requestServices.GetRequiredService<IHttpContextAccessor>().HttpContext!;

        using (var lease = limiter.AttemptAcquire(httpContext))
        {
            if (lease.IsAcquired is false)
                return DocsMcpToolResults.Failed($"This caller has had its {EvaluationsPerHour} evaluations this hour; try again later.");
        }

        var question = DocsMcpToolResults.Argument(request, "question");

        try
        {
            var answer = await Evaluate(requestServices, httpContext.Request.GetWebAppUrl(), EvaluationSource.Mcp, business, needs, question, cancellationToken);

            return DocsMcpToolResults.Succeeded(answer);
        }
        catch (Exception exp) when (cancellationToken.IsCancellationRequested is false)
        {
            requestServices.GetRequiredService<ILogger<EvaluationMcpServer>>().LogError(exp, "An evaluation over MCP failed.");
            return DocsMcpToolResults.Failed("The evaluation couldn't be finished just now. The site's pages are at https://bitfabric.co/llms-full.txt to read instead.");
        }
    }

    /// <summary>Runs the evaluation agent once on the visitor's business and needs, and returns its answer.</summary>
    public static async Task<string> Evaluate(IServiceProvider services, Uri webAppUrl, EvaluationSource source, string business, string needs, string? question, CancellationToken cancellationToken)
    {
        var siteContent = services.GetRequiredService<SiteContent>();
        var recorder = services.GetRequiredService<EvaluationRecorder>();

        var site = await siteContent.All(webAppUrl, cancellationToken);
        var license = await siteContent.License(cancellationToken);

        var record = AIFunctionFactory.Create(async (
            [Description("The anonymous summary, two or three lines.")] string summary,
            [Description("yes, no or unknown")] string convinced,
            CancellationToken ct) =>
        {
            _ = Enum.TryParse<EvaluationConvinced>(convinced, ignoreCase: true, out var parsed);
            return await recorder.Record(source, summary, source is EvaluationSource.Mcp ? EvaluationConvinced.Unknown : parsed, ct)
                ? "Kept."
                : "Not kept: it named something that identifies someone. Don't try again.";
        }, new AIFunctionFactoryOptions { Name = EvaluationPrompt.RecordToolName, Description = EvaluationPrompt.RecordToolDescription });

        var agent = services.GetRequiredService<IChatClient>().AsAIAgent(
            instructions: $"{EvaluationPrompt.Instructions}\n{EvaluationPrompt.McpRecording}\n\n{EvaluationPrompt.Sources(site, license)}",
            name: "EvaluationAgent",
            description: "Weighs bit Fabric against a visitor's business and needs",
            tools: [.. DocsMcpChatbotTools.AIFunctions(services), record]);

        var message = $"""
            The business: {business}
            What its app must do: {needs}
            {(question is null ? "" : $"Check first: {question}")}
            """;

        var response = await agent.RunAsync(message, options: new ChatClientAgentRunOptions(new ChatOptions { MaxOutputTokens = MaxOutputTokens }), cancellationToken: cancellationToken);

        return response.Text;
    }
}
