using System.Text;
using Bit.BlazorUI;
using Bit.Fabric.Server.Api.Features.DocsMcp;
using Bit.Fabric.Shared.Features.Chatbot;
using Bit.Fabric.Tests.Features.DocsMcp;
using Microsoft.Extensions.AI;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Bit.Fabric.Tests.Features.Chatbot;

/// <summary>
/// What the AI assistant answers a visitor who asks whether bit Fabric has something (bitfabric-internal/specs/home-page.md,
/// 2026-11-07): yes for what every project comes with, and no claim for what it doesn't. The answer comes from the real
/// model, through the hub the chat panel uses, so it is judged rather than matched: a second call to the same model reads
/// the question, what a right answer does, and the answer, and says whether it does that.
/// <para>
/// Both need the chat model's key, <c>AI:OpenAI:ChatApiKey</c> (dotnet user-secrets in Server.Api), so without it these are
/// inconclusive rather than failed, as in CI, which has no key.
/// </para>
/// </summary>
[TestClass, TestCategory("IntegrationTest"), TestCategory("AITest")]
public partial class AssistantTests
{
    [TestMethod]
    [DataRow("Does an app made with bit Fabric work offline?", "Says yes: the app keeps working without a connection and syncs once it is back.")]
    [DataRow("Can my users sign in with Face ID or their fingerprint?", "Says yes.")]
    [DataRow("I want to sell my app to many companies as a SaaS. Is it multi-tenant?", "Says yes: it is multi-tenant and SaaS ready.")]
    [DataRow("آیا اپ iOS و ویندوز هم داره؟", "Says yes, in Persian: there are iOS and Windows apps, with Android and macOS too.")]
    [DataRow("Do I get push notifications, and do they cost anything?", "Says yes, and that they are free.")]
    [DataRow("Will the Windows app run on an old Windows 7 PC?", "Says yes: it runs on Windows 7 and later.")]
    [DataRow("Once my app is live, does anyone keep an eye on it?", "Says yes: bit Fabric watches over the live app, such as with health monitoring and alerts, scanners or a status page.")]
    [DataRow("If my app breaks, can the alert come to our Slack?", "Says yes: bit Fabric's alerts reach Slack, and Microsoft Teams or Discord too.")]
    [DataRow("Do I have to create an account before I can start a project?", "Says no: no sign-in is needed to start.")]
    public async Task AFeatureEveryProjectHas_Should_BeConfirmed(string question, string rightAnswer)
    {
        await AssertAnswer(question, rightAnswer);
    }

    // The yes is for what is listed only: a prompt that made it say yes to everything would pass the test above.
    [TestMethod]
    [DataRow("Does it come with a built-in crypto wallet for my users?", "Does not say that a crypto wallet is built in. Saying it isn't, or that it can be added to the project, is right.")]
    public async Task AFeatureNoProjectHas_Should_NotBeClaimed(string question, string rightAnswer)
    {
        await AssertAnswer(question, rightAnswer);
    }

    /// <summary>
    /// A question about a bit platform library is answered from that library's own documentation, through the tools /docs-mcp
    /// serves (See DocsMcpChatbotTools): Bmage's here, stood in by one that says what no model knows from memory.
    /// </summary>
    [TestMethod]
    public async Task ALibraryQuestion_Should_BeAnsweredFromItsDocs()
    {
        await using var platform = new FakeBitPlatform();
        await platform.Start(TestContext.CancellationToken);

        await AssertAnswer("Do you also have an image editor, and what can it do?",
            "Says yes, and that it crops, resizes, rotates, redacts faces and exports PNG, JPEG and WebP - all of these, as Bmage's documentation says.",
            services => services.AddHttpClient(DocsMcpHttp.ClientName).AddHttpMessageHandler(() => new FakeBitPlatform.Handler(platform)));
    }

    private async Task AssertAnswer(string question, string rightAnswer, Action<IServiceCollection>? configureTestServices = null)
    {
        await using var server = new AppTestServer();

        await server.Build(configureTestServices).Start(TestContext.CancellationToken);

        var chatClient = server.ApiApp.Services.GetService<IChatClient>()
            ?? throw new AssertInconclusiveException("The assistant has no chat model here: set AI:OpenAI:ChatApiKey with dotnet user-secrets in Server.Api.");

        var answer = await Ask(server, question);

        var verdict = await Judge(chatClient, question, rightAnswer, answer);

        // "PASS: ...", markdown around it or not
        Assert.IsTrue(Regex.IsMatch(verdict, @"^\W*PASS\b", RegexOptions.IgnoreCase),
            $"The judge: {verdict}{Environment.NewLine}The question: {question}{Environment.NewLine}The answer: {answer}");
    }

    /// <summary>Asks the way the chat panel does (See AppAiChat), through the hub it streams its answers from (BitAiSignalRTransport on app-hub), and reads the answer to its end.</summary>
    private async Task<string> Ask(AppTestServer server, string question)
    {
        var origin = server.WebAppAddress.GetLeftPart(UriPartial.Authority);

        await using var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.ApiAppAddress, $"app-hub?origin={Uri.EscapeDataString(origin)}"), options =>
            {
                options.SkipNegotiation = true;
                options.Transports = HttpTransportType.WebSockets;
            })
            .Build();

        var transport = new BitAiSignalRTransport(hubConnection);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));

        var message = new BitAiMessage(BitAiRole.User);
        message.AddPart(new BitAiTextPart(question));

        var request = new BitAiRequest([message])
        {
            Options = new()
            {
                Extra = new Dictionary<string, object?>
                {
                    [AiChatProtocol.DeviceInfo] = "Windows 11, Edge",
                    [AiChatProtocol.TimeZoneId] = "UTC"
                }
            }
        };

        var text = new StringBuilder();
        string? answer = null;
        BitAiError? error = null;

        await foreach (var evt in transport.StreamAsync(request, timeout.Token))
        {
            switch (evt)
            {
                case BitAiTextDeltaEvent delta:
                    text.Append(delta.Delta);
                    break;
                // What the model wrote, word for word, without what the panel draws around it (See AiChatProtocol.Answer).
                case BitAiMetadataEvent { Metadata.Extra: { } extra } when extra.GetValueOrDefault(AiChatProtocol.Answer) is { } value:
                    answer = value is JsonElement { ValueKind: JsonValueKind.String } json ? json.GetString() : value.ToString();
                    break;
                case BitAiErrorEvent failure:
                    error = failure.Error;
                    break;
            }
        }

        Assert.IsNull(error, $"The assistant's answer failed: {error?.Message}");

        answer ??= text.ToString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(answer), "The assistant answered nothing.");

        return answer;
    }

    private async Task<string> Judge(IChatClient chatClient, string question, string rightAnswer, string answer)
    {
        var prompt = $"""
            You judge an answer that Ava, the assistant on bit Fabric's website, gave a visitor.

            The visitor asked:
            {question}

            A right answer:
            {rightAnswer}

            Ava's answer:
            {answer}

            Does Ava's answer do what a right answer does? Wording, length, extra details that are right and an offer to help
            further don't matter. Reply with PASS or FAIL, then a colon and one sentence on why.
            """;

        var response = await chatClient.GetResponseAsync(prompt, cancellationToken: TestContext.CancellationToken);

        return response.Text.Trim();
    }

    public TestContext TestContext { get; set; } = default!;
}
