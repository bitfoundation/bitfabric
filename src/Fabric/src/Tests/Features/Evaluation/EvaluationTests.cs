using System.Net;
using System.Runtime.CompilerServices;
using Bit.Fabric.Server.Api.Features.Evaluation;
using Bit.Fabric.Server.Api.Features.Evaluation.Models;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Bit.Fabric.Tests.Features.Evaluation;

/// <summary>
/// The AI evaluation (bitfabric-internal/specs/home-page.md, "The AI evaluation"): /evaluate-mcp, open to any AI with no
/// sign-in, hands a visitor's business and needs to an agent that reads every page of the site and the license, answers,
/// and keeps an anonymous summary of it. The model is stood in by <see cref="FakeEvaluator"/>, so these run without a key.
/// </summary>
[TestClass, TestCategory("IntegrationTest")]
public partial class EvaluationTests
{
    private const string FakeLicense = "FAKE LICENSE: free for one project, paid for more.";

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task AnEvaluation_Should_ReadTheSiteAndTheLicense_KeepAnAnonymousSummary_AndStopAtItsLimit()
    {
        var model = new FakeEvaluator();
        var startedAt = DateTimeOffset.UtcNow;
        await using var server = await StartServer(model);
        await using var mcp = await Connect(server);

        Assert.AreEqual("bitfabric-evaluation", mcp.ServerInfo!.Name);

        var tools = await mcp.ListToolsAsync(cancellationToken: TestContext.CancellationToken);
        Assert.HasCount(1, tools, "One tool, and nothing of the assistant's or the documentation's.");
        Assert.AreEqual(EvaluationMcpServer.ToolName, tools[0].Name);
        var annotations = tools[0].ProtocolTool.Annotations;
        Assert.IsNotNull(annotations);
        Assert.IsTrue(annotations.ReadOnlyHint, "It changes nothing the caller owns.");

        var answer = await Evaluate(mcp);

        Assert.Contains(FakeEvaluator.Verdict, answer, "The agent's answer is the tool's.");
        Assert.Contains("Keycloak", model.Instructions, "The agent reads every page of the site: /features' words are there.");
        Assert.Contains(FakeLicense, model.Instructions, "The agent reads the license.");
        Assert.Contains("A small bakery chain", model.LastUserMessage);

        await using (var scope = server.ApiApp.Services.CreateAsyncScope())
        {
            var outcomes = await scope.ServiceProvider.GetRequiredService<AppDbContext>().EvaluationOutcomes.Where(o => o.Summary == FakeEvaluator.Summary && o.CreatedOn >= startedAt).ToListAsync(TestContext.CancellationToken);

            Assert.HasCount(1, outcomes, "An evaluation keeps one anonymous summary.");
            Assert.AreEqual((EvaluationSource.Mcp, EvaluationConvinced.Unknown), (outcomes[0].Source, outcomes[0].Convinced), "Nobody answers the verdict over MCP.");
        }

        var tooLong = await mcp.CallToolAsync(EvaluationMcpServer.ToolName, new Dictionary<string, object?> { ["business"] = new string('a', EvaluationMcpServer.MaxArgumentLength + 1), ["needs"] = "An app" }, cancellationToken: TestContext.CancellationToken);
        var isTooLong = tooLong.IsError ?? false;
        Assert.IsTrue(isTooLong, "An argument is a few lines, never a page: nothing reaches the model.");
        Assert.AreEqual(1, model.Evaluations);

        for (var i = 1; i < EvaluationMcpServer.EvaluationsPerHour; i++)
        {
            await Evaluate(mcp);
        }

        var refused = await mcp.CallToolAsync(EvaluationMcpServer.ToolName, Arguments(), cancellationToken: TestContext.CancellationToken);
        var isRefused = refused.IsError ?? false;
        Assert.IsTrue(isRefused, "A caller gets its evaluations an hour, and no more: each runs a model this server pays for.");
        Assert.AreEqual(EvaluationMcpServer.EvaluationsPerHour, model.Evaluations);
    }

    [TestMethod]
    [DataRow("A two-person bakery chain wants pre-orders and loyalty points; worth starting with.", true)]
    [DataRow("Write to sara@example.com about the bakery.", false)]
    [DataRow("The bakery at +31 6 1234 5678 wants an app.", false)]
    [DataRow("See bloomflowers.nl for what they sell.", false)]
    [DataRow("They read bitfabric.co and were convinced.", true)]
    public async Task ASummary_Should_BeKept_OnlyWhenItNamesNobody(string summary, bool kept)
    {
        await using var server = new AppTestServer();
        await server.Build().Start(TestContext.CancellationToken);

        await using var scope = server.ApiApp.Services.CreateAsyncScope();

        Assert.AreEqual(kept, await scope.ServiceProvider.GetRequiredService<EvaluationRecorder>().Record(EvaluationSource.Chat, summary, EvaluationConvinced.Yes, TestContext.CancellationToken));
    }

    private async Task<string> Evaluate(McpClient mcp)
    {
        var result = await mcp.CallToolAsync(EvaluationMcpServer.ToolName, Arguments(), cancellationToken: TestContext.CancellationToken);

        var text = string.Join("\n", result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(block => block.Text));
        Assert.AreNotEqual(true, result.IsError, text);

        return text;
    }

    private static Dictionary<string, object?> Arguments() => new()
    {
        ["business"] = "A small bakery chain with three shops",
        ["needs"] = "Pre-orders and loyalty points on iPhone and Android"
    };

    private async Task<AppTestServer> StartServer(FakeEvaluator model)
    {
        var server = new AppTestServer();

        await server.Build(configureTestServices: services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IChatClient>(new ChatClientBuilder(model).UseFunctionInvocation().Build()));
            services.AddHttpClient(SiteContent.HttpClientName).AddHttpMessageHandler(() => new LicenseHandler());
        }, configureTestConfiguration: configuration => configuration["WebAppRender:PrerenderEnabled"] = "true").Start(TestContext.CancellationToken);

        return server;
    }

    private async Task<McpClient> Connect(AppTestServer server)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(server.ApiAppAddress, EvaluationMcpServer.Path.TrimStart('/')),
            TransportMode = HttpTransportMode.StreamableHttp
        });

        return await McpClient.CreateAsync(transport, cancellationToken: TestContext.CancellationToken);
    }

    /// <summary>The license from GitHub, stood in; every other address the site content reads goes through.</summary>
    private sealed class LicenseHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.ToString() == SiteContent.LicenseUrl)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(FakeLicense) });

            return base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// The model, stood in: asked to evaluate, it keeps its summary with the record tool first, then answers with its verdict,
    /// as the instructions ask.
    /// </summary>
    private sealed class FakeEvaluator : IChatClient
    {
        public const string Summary = "A small bakery chain wants pre-orders and loyalty points on phones; worth starting with.";
        public const string Verdict = "Verdict: worth starting with.";

        public string Instructions { get; private set; } = "";
        public string LastUserMessage { get; private set; } = "";
        public int Evaluations { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var list = messages.ToList();

            if (list.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Any())
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Verdict)));

            Evaluations++;
            Instructions = string.Join("\n", list.Where(m => m.Role == ChatRole.System).Select(m => m.Text)) + options?.Instructions;
            LastUserMessage = list.Last(m => m.Role == ChatRole.User).Text;

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent($"call-{Evaluations}", EvaluationPrompt.RecordToolName, new Dictionary<string, object?> { ["summary"] = Summary, ["convinced"] = "yes" })])));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var update in (await GetResponseAsync(messages, options, cancellationToken)).ToChatResponseUpdates())
            {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
