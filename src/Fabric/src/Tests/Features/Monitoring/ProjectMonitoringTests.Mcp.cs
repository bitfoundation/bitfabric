using System.Security.Cryptography;
using System.Text.Json;
using Bit.Fabric.Server.Api.Features.Identity.OAuth.Services;
using Bit.Fabric.Server.Api.Infrastructure.SignalR;
using Bit.Fabric.Shared.Features.Identity.OAuth.Dtos;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Features.Tenants;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// Project monitoring for AI apps (bitfabric-internal/specs/monitoring.md, AI apps): the tools <c>/mcp</c> serves, called
/// over HTTP the way grok.com or any other MCP client calls them, with the signed-in user's own token or with one an AI app
/// got through the app's OAuth.
/// </summary>
public partial class ProjectMonitoringTests
{
    /// <summary>The client id of the AI app the test server knows (See StartServer).</summary>
    private const string AIAppClientId = "an-ai-app";

    private const string AIAppRedirectUri = "https://ai-app.example/oauth/callback";

    [TestMethod]
    public async Task TheMonitoringTools_Should_BeListed_TheActionsAskingForConfirmation()
    {
        await using var server = await StartServer(new FakeCustomerApp());
        await using var client = await SignInWithATeamOfTheirOwn(server);
        await using var mcp = await ConnectToMcp(server, await AccessTokenOf(client));

        var tools = await mcp.ListToolsAsync(cancellationToken: TestContext.CancellationToken);

        string[] reads = ["GetProjects", "GetProject", "GetApplicationMap", "GetIncidents", "GetHistory", "GetSlaReport", "GetStatusPage"];
        string[] actions = ["AcknowledgeIncident", "PostIncidentUpdate", "ScheduleMaintenance"];

        foreach (var name in reads)
        {
            var annotations = tools.Single(tool => tool.Name == name).ProtocolTool.Annotations;
            Assert.IsNotNull(annotations);
            Assert.IsTrue(annotations.ReadOnlyHint, $"{name} only reads.");
        }

        foreach (var name in actions)
        {
            var tool = tools.Single(tool => tool.Name == name);
            Assert.IsNotNull(tool.ProtocolTool.Annotations);
            Assert.IsFalse(tool.ProtocolTool.Annotations.ReadOnlyHint, $"{name} changes the project.");
            Assert.Contains("confirmed", tool.Description, $"{name} tells the model to ask the user first.");
        }
    }

    [TestMethod]
    public async Task AProjectsHealth_AndAFailingPart_Should_ComeBackByTheAppsOwnNames()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");

        customerApp.Fail("smtp", "The mail server didn't answer within 5 s.");
        await Probe(server, project.Id);

        await using var mcp = await ConnectToMcp(server, await AccessTokenOf(client));

        var list = await Call(mcp, "GetProjects");
        Assert.AreEqual("Test team", list.GetProperty("team").GetString(), "A user in several teams sees which one answers.");

        var card = list.GetProperty("projects").EnumerateArray().Single();
        Assert.AreEqual(project.Id, card.GetProperty("id").GetGuid());
        Assert.AreEqual("Customer app", card.GetProperty("name").GetString());
        Assert.AreEqual("Degraded", card.GetProperty("health").GetString());
        Assert.AreSequenceEqual(new[] { "Email" }, Strings(card.GetProperty("partsNotWorking")), "By the name the app gives it; the part that only misses its settings isn't failing.");
        Assert.IsTrue(card.TryGetProperty("lastCheckedAt", out _));

        var details = await Call(mcp, "GetProject", new() { ["project"] = $"{project.Id}" });
        var parts = details.GetProperty("parts").EnumerateArray()
            .SelectMany(group => group.GetProperty("parts").EnumerateArray().Select(part => (Category: group.GetProperty("category").GetString(), Part: part)))
            .ToArray();

        var (category, email) = parts[0];
        Assert.AreEqual("Email", email.GetProperty("title").GetString(), "What needs attention comes first, as on the Overview.");
        Assert.AreEqual("Messaging", category);
        Assert.AreEqual("smtp", email.GetProperty("key").GetString());
        Assert.AreEqual("Degraded", email.GetProperty("status").GetString());
        Assert.AreEqual("The mail server didn't answer within 5 s.", email.GetProperty("message").GetString());
        Assert.AreEqual(5000, email.GetProperty("limitMs").GetInt32());
        Assert.IsTrue(email.TryGetProperty("since", out _));

        Assert.AreEqual("NotConfigured", parts.Single(part => part.Part.GetProperty("key").GetString() == "googleSignIn").Part.GetProperty("status").GetString());
        Assert.AreEqual("Authorizes and voids a one cent payment.", parts.Single(part => part.Part.GetProperty("title").GetString() == "Payment gateway").Part.GetProperty("proves").GetString());

        Assert.AreEqual("1.0.0+aaaa111", details.GetProperty("applicationVersion").GetString());
        Assert.AreEqual("Germany", details.GetProperty("checkedFrom")[0].GetProperty("name").GetString());
        Assert.Contains("Oceania", Strings(details.GetProperty("checkedFrom"), "name"), "A new project's locations on the bitplatform Network.");
    }

    [TestMethod]
    public async Task AProject_Should_BeFoundByTheNameOrAddressTheUserSays()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");

        await using var mcp = await ConnectToMcp(server, await AccessTokenOf(client));

        Assert.AreEqual(project.Id, (await Call(mcp, "GetProject", new() { ["project"] = "customer APP" })).GetProperty("id").GetGuid(), "Its name, in any case.");
        Assert.AreEqual(project.Id, (await Call(mcp, "GetProject", new() { ["project"] = "https://Customer.example/" })).GetProperty("id").GetGuid(), "Its address, however it's written.");

        // A part of its name, in the time zone the user is in.
        var history = await Call(mcp, "GetHistory", new() { ["project"] = "customer", ["days"] = 3, ["timeZone"] = "Asia/Tehran" });
        Assert.AreEqual(project.Id, history.GetProperty("project").GetProperty("id").GetGuid());
        Assert.AreEqual(3, history.GetProperty("days").GetArrayLength());
        Assert.AreEqual("Asia/Tehran", history.GetProperty("timeZone").GetString());
        Assert.AreEqual(100, history.GetProperty("uptimePercent").GetDouble(), "Watched since it was added, and never down.");

        // Nothing matches: the model is told so, with the projects there are to ask the user about.
        var nothing = await Call(mcp, "GetProject", new() { ["project"] = "Todo" });
        Assert.Contains("No project", nothing.GetProperty("message").GetString()!);
        Assert.AreEqual(project.Id, nothing.GetProperty("projects").EnumerateArray().Single().GetProperty("id").GetGuid());

        // The SLA of this month and the status page, by its name too.
        var sla = await Call(mcp, "GetSlaReport", new() { ["project"] = "Customer app" });
        Assert.AreEqual(99.9, sla.GetProperty("targetPercent").GetDouble());
        Assert.IsTrue(sla.GetProperty("isCurrentMonth").GetBoolean());
        Assert.AreEqual("UTC", sla.GetProperty("timeZone").GetString());

        var noPage = await Call(mcp, "GetStatusPage", new() { ["project"] = "Customer app" });
        Assert.Contains("no status page yet", noPage.GetProperty("message").GetString()!, "Reading never makes one: the Status page tab does.");

        await client.GetController<IStatusPagesController>().GetStatusPage(project.Id, TestContext.CancellationToken);
        var page = await Call(mcp, "GetStatusPage", new() { ["project"] = "Customer app" });
        Assert.IsFalse(page.GetProperty("published").GetBoolean());
        Assert.AreEqual(0, page.GetProperty("followers").GetInt32());
        Assert.IsGreaterThan(0, page.GetProperty("components").GetArrayLength());
    }

    [TestMethod]
    public async Task AnIncident_Should_BeAcknowledgedAndUpdatedThroughTheTools()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var incidents = client.GetController<IProjectIncidentsController>();
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");

        customerApp.Fail("AppDbContext", "The database didn't answer.");
        await Probe(server, project.Id);
        await Probe(server, project.Id);

        await using var mcp = await ConnectToMcp(server, await AccessTokenOf(client));

        var card = (await Call(mcp, "GetProjects")).GetProperty("projects")[0];
        Assert.AreEqual("Down", card.GetProperty("health").GetString());
        var incidentId = card.GetProperty("ongoingIncident").GetProperty("id").GetGuid();

        var ongoing = (await Call(mcp, "GetIncidents", new() { ["ongoingOnly"] = true })).GetProperty("incidents").EnumerateArray().Single();
        Assert.AreEqual(incidentId, ongoing.GetProperty("id").GetGuid());
        Assert.AreEqual("Down", ongoing.GetProperty("impact").GetString());
        Assert.Contains("Database", Strings(ongoing.GetProperty("parts")), "By the name the app gives the part.");
        Assert.IsTrue(ongoing.GetProperty("openedByBitFabric").GetBoolean());
        Assert.AreEqual("Push", ongoing.GetProperty("whoWasTold")[0].GetProperty("channel").GetString(), "Whoever added the project heard of it at once.");

        var acknowledged = await Call(mcp, "AcknowledgeIncident", new() { ["incidentId"] = incidentId });
        Assert.StartsWith("Acknowledged", acknowledged.GetProperty("message").GetString()!);
        var by = acknowledged.GetProperty("incident").GetProperty("acknowledgedBy").GetString();
        Assert.IsFalse(string.IsNullOrEmpty(by));

        // The page sees it as if its button were pressed: the update says who has it, and the escalation stopped.
        var onThePage = (await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single();
        Assert.AreEqual(by, onThePage.AcknowledgedBy);
        Assert.AreEqual(ProjectIncidentPhase.Identified, onThePage.Updates[0].Phase);

        await Escalate(server, project.Id);
        Assert.HasCount(onThePage.Alerts.Length, (await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single().Alerts, "Nothing more goes out once someone is on it.");

        Assert.StartsWith("Nothing changed", (await Call(mcp, "AcknowledgeIncident", new() { ["incidentId"] = incidentId })).GetProperty("message").GetString()!);

        var updated = await Call(mcp, "PostIncidentUpdate", new() { ["incidentId"] = incidentId, ["phase"] = "Monitoring", ["text"] = "Restarted the database." });
        Assert.AreEqual("Restarted the database.", updated.GetProperty("incident").GetProperty("updates")[0].GetProperty("text").GetString());
        Assert.AreEqual(by, updated.GetProperty("incident").GetProperty("updates")[0].GetProperty("author").GetString());

        Assert.HasCount(onThePage.Updates.Length + 1, (await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single().Updates);

        // bit Fabric closes the incidents it opened.
        var refused = await Call(mcp, "PostIncidentUpdate", new() { ["incidentId"] = incidentId, ["phase"] = "Resolved", ["text"] = "Fixed." });
        Assert.Contains("closes it by itself", refused.GetProperty("message").GetString()!);
        Assert.HasCount(onThePage.Updates.Length + 1, (await incidents.GetIncidents(project.Id, TestContext.CancellationToken)).Single().Updates, "Nothing was posted.");

        var startsAt = DateTimeOffset.UtcNow.AddDays(1);
        var scheduled = await Call(mcp, "ScheduleMaintenance", new() { ["project"] = "Customer app", ["title"] = "Moving the database", ["startsAt"] = startsAt, ["endsAt"] = startsAt.AddHours(2) });
        Assert.StartsWith("Scheduled", scheduled.GetProperty("message").GetString()!);
        Assert.AreEqual("Moving the database", (await incidents.GetMaintenanceWindows(project.Id, TestContext.CancellationToken)).Single().Title);

        Assert.Contains("end after it starts", (await Call(mcp, "ScheduleMaintenance", new() { ["project"] = "Customer app", ["title"] = "Backwards", ["startsAt"] = startsAt, ["endsAt"] = startsAt.AddHours(-1) })).GetProperty("message").GetString()!);
    }

    [TestMethod]
    public async Task AnotherTeam_Should_SeeNoneOfAProject_ThroughTheTools()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var owner = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(owner.GetController<IProjectsController>(), customerApp, "customer.example");

        customerApp.Fail("smtp", "The mail server didn't answer within 5 s.");
        await Probe(server, project.Id);
        await Probe(server, project.Id);
        var incident = (await owner.GetController<IProjectIncidentsController>().GetIncidents(project.Id, TestContext.CancellationToken)).Single();

        await using var stranger = await SignInWithATeamOfTheirOwn(server);
        await using var mcp = await ConnectToMcp(server, await AccessTokenOf(stranger));

        Assert.AreEqual(0, (await Call(mcp, "GetProjects")).GetProperty("projects").GetArrayLength());
        Assert.AreEqual(0, (await Call(mcp, "GetIncidents")).GetProperty("incidents").GetArrayLength());
        Assert.Contains("No project", (await Call(mcp, "GetProject", new() { ["project"] = $"{project.Id}" })).GetProperty("message").GetString()!);
        Assert.Contains("No project", (await Call(mcp, "GetProject", new() { ["project"] = "Customer app" })).GetProperty("message").GetString()!);
        Assert.Contains("No project", (await Call(mcp, "GetSlaReport", new() { ["project"] = $"{project.Id}" })).GetProperty("message").GetString()!);
        Assert.Contains("No incident", (await Call(mcp, "AcknowledgeIncident", new() { ["incidentId"] = incident.Id })).GetProperty("message").GetString()!);
        Assert.Contains("No incident", (await Call(mcp, "PostIncidentUpdate", new() { ["incidentId"] = incident.Id, ["phase"] = "Monitoring", ["text"] = "Mine now." })).GetProperty("message").GetString()!);
        Assert.Contains("No project", (await Call(mcp, "ScheduleMaintenance", new() { ["project"] = $"{project.Id}", ["title"] = "Mine now", ["startsAt"] = DateTimeOffset.UtcNow, ["endsAt"] = DateTimeOffset.UtcNow.AddHours(1) })).GetProperty("message").GetString()!);

        var untouched = (await owner.GetController<IProjectIncidentsController>().GetIncidents(project.Id, TestContext.CancellationToken)).Single();
        Assert.IsNull(untouched.AcknowledgedBy);
        Assert.HasCount(incident.Updates.Length, untouched.Updates);
        Assert.IsEmpty(await owner.GetController<IProjectIncidentsController>().GetMaintenanceWindows(project.Id, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task TheMonitoringTools_Should_BeRefused_SignedOut_AndSayWhy_WithoutATeam()
    {
        await using var server = await StartServer(new FakeCustomerApp());

        // Signed out, /mcp answers an MCP client's first request with where to sign in (RFC 9728), as grok.com needs.
        using var http = server.CreateRawHttpClient();
        using var initialize = new HttpRequestMessage(HttpMethod.Post, "mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "test", version = "1.0" } } })
        };
        initialize.Headers.Accept.ParseAdd("application/json");
        initialize.Headers.Accept.ParseAdd("text/event-stream");

        using var refused = await http.SendAsync(initialize, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Contains("resource_metadata", refused.Headers.WwwAuthenticate.ToString());

        await Assert.ThrowsAsync<Exception>(() => ConnectToMcp(server, "not-a-token"));

        // Signed in without a team: a project belongs to one, so the tools say what to do instead of answering for the
        // fallback tenant every team-less user shares.
        var email = await TestUsers.SignUp(server, TestContext.CancellationToken);
        await using var client = server.CreateAppClient();
        await client.AuthManager.SignIn(new() { Email = email, Password = TestUsers.Password }, TestContext.CancellationToken);
        await using var mcp = await ConnectToMcp(server, await AccessTokenOf(client));

        Assert.Contains("no team", (await Call(mcp, "GetProjects")).GetProperty("message").GetString()!);
    }

    /// <summary>
    /// grok.com names no team when it asks for an approval, so the approval is for the team the user is in when they give
    /// it, which the consent page names (See OAuthController.ResolveTenantId): a user in several teams connects the one
    /// they mean, and the token reads that team's projects and no other's.
    /// </summary>
    [TestMethod]
    public async Task AnAIAppsApproval_Should_BeForTheTeamTheUserApprovedIn()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");
        var teams = client.GetController<ITenantController>();
        var firstTeam = (await teams.GetCurrentTenant(TestContext.CancellationToken))!;

        var secondTeam = await teams.Create(new() { Name = $"t{Guid.NewGuid():N}"[..20], Title = "Second team" }, TestContext.CancellationToken);
        Assert.IsTrue(await client.AuthManager.SwitchTenant(secondTeam.Id, TestContext.CancellationToken));

        var (inSecondTeam, consentedFor) = await ApproveAnAIApp(server, client);
        Assert.AreEqual(secondTeam.Name, consentedFor, "The consent page names the team the approval is for.");

        await using (var mcp = await ConnectToMcp(server, inSecondTeam))
        {
            var list = await Call(mcp, "GetProjects");
            Assert.AreEqual("Second team", list.GetProperty("team").GetString());
            Assert.AreEqual(0, list.GetProperty("projects").GetArrayLength());
            Assert.Contains("No project", (await Call(mcp, "GetProject", new() { ["project"] = $"{project.Id}" })).GetProperty("message").GetString()!);
        }

        Assert.IsTrue(await client.AuthManager.SwitchTenant(firstTeam.Id, TestContext.CancellationToken));
        var email = IAuthTokenProvider.ParseAccessToken(await AccessTokenOf(client), validateExpiry: false).GetEmail();
        var (inFirstTeam, _) = await ApproveAnAIApp(server, client);

        customerApp.Fail("smtp", "The mail server didn't answer within 5 s.");
        await Probe(server, project.Id);
        await Probe(server, project.Id);

        await using (var mcp = await ConnectToMcp(server, inFirstTeam))
        {
            var list = await Call(mcp, "GetProjects");
            Assert.AreEqual("Test team", list.GetProperty("team").GetString());

            var card = list.GetProperty("projects").EnumerateArray().Single();
            Assert.AreEqual(project.Id, card.GetProperty("id").GetGuid());
            Assert.AreEqual("Degraded", card.GetProperty("health").GetString());

            // An AI app's token carries no name, so who is on it comes from the account, as the page's own token says it.
            var acknowledged = await Call(mcp, "AcknowledgeIncident", new() { ["incidentId"] = card.GetProperty("ongoingIncident").GetProperty("id").GetGuid() });
            Assert.AreEqual(email, acknowledged.GetProperty("incident").GetProperty("acknowledgedBy").GetString());
        }
    }

    /// <summary>
    /// The in-app assistant gets the same tools, so "are my projects healthy?" is answered alike in bit Fabric's own chat. Called
    /// the way the assistant's agent calls them, on the hub's request with its signed-in user (See AppHub.StartChat).
    /// </summary>
    [TestMethod]
    public async Task TheInAppAssistant_Should_AnswerFromTheSameTools()
    {
        var customerApp = new FakeCustomerApp();
        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var project = await Connect(client.GetController<IProjectsController>(), customerApp, "customer.example");

        customerApp.Fail("smtp", "The mail server didn't answer within 5 s.");
        await Probe(server, project.Id);

        await using var scope = server.ApiApp.Services.CreateAsyncScope();
        var functions = scope.ServiceProvider.GetRequiredService<AppChatbot>().GetAIFunctions();

        await using (var mcp = await ConnectToMcp(server, await AccessTokenOf(client)))
        {
            foreach (var tool in (await mcp.ListToolsAsync(cancellationToken: TestContext.CancellationToken)).Where(tool => tool.Name is not ("GetCurrentDateTime" or "GetAppPages")))
            {
                Assert.Contains(function => function.Name == tool.Name, functions, $"The assistant has {tool.Name} too.");
            }
        }

        var bearer = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(IdentityConstants.BearerScheme);
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = bearer.BearerTokenProtector.Unprotect(await AccessTokenOf(client))!.Principal, RequestServices = scope.ServiceProvider };

        try
        {
            var answer = (JsonElement)(await functions.Single(function => function.Name == "GetProjects").InvokeAsync(new(), TestContext.CancellationToken))!;

            var card = answer.GetProperty("projects").EnumerateArray().Single();
            Assert.AreEqual(project.Id, card.GetProperty("id").GetGuid());
            Assert.AreEqual("Degraded", card.GetProperty("health").GetString());
            Assert.AreSequenceEqual(new[] { "Email" }, Strings(card.GetProperty("partsNotWorking")));
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    /// <summary>An MCP client of the api's own <c>/mcp</c>, over the streamable HTTP transport, as an AI app connects.</summary>
    private async Task<McpClient> ConnectToMcp(AppTestServer server, string? accessToken)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(server.ApiAppAddress, "mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {accessToken}" }
        });

        return await McpClient.CreateAsync(transport, cancellationToken: TestContext.CancellationToken);
    }

    /// <summary>A tool's answer, which the tools write as one JSON text.</summary>
    private async Task<JsonElement> Call(McpClient mcp, string tool, Dictionary<string, object?>? arguments = null)
    {
        var result = await mcp.CallToolAsync(tool, arguments ?? [], cancellationToken: TestContext.CancellationToken);

        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        Assert.IsFalse(result.IsError ?? false, text);

        using var json = JsonDocument.Parse(text);
        return json.RootElement.Clone();
    }

    private static string?[] Strings(JsonElement array, string? property = null)
        => [.. array.EnumerateArray().Select(item => (property is null ? item : item.GetProperty(property)).GetString())];

    private static async Task<string?> AccessTokenOf(AppClient client)
        => await client.Services.GetRequiredService<IAuthTokenProvider>().GetAccessToken();

    /// <summary>
    /// What an AI app goes through to get a token for <c>/mcp</c>: its authorization request naming no team, the user's
    /// approval on the consent page, and the code's exchange with PKCE. Returns the token and the team the consent page named.
    /// </summary>
    private async Task<(string AccessToken, string? Team)> ApproveAnAIApp(AppTestServer server, AppClient client)
    {
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        OAuthAuthorizeRequestDto request = new()
        {
            ClientId = AIAppClientId,
            RedirectUri = AIAppRedirectUri,
            ResponseType = "code",
            Scope = "chat",
            State = "state",
            CodeChallenge = OAuthService.ComputeCodeChallenge(verifier),
            CodeChallengeMethod = "S256",
            Resource = $"{server.ApiAppAddress.ToString().TrimEnd('/')}/mcp"
        };

        // The consent page calls these as the signed-in user, straight at the api, which is the issuer.
        using var asTheUser = server.CreateRawHttpClient();
        asTheUser.DefaultRequestHeaders.Authorization = new("Bearer", await AccessTokenOf(client));

        using var review = await asTheUser.PostAsJsonAsync("api/v1/OAuth/Review", request, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, review.StatusCode, await review.Content.ReadAsStringAsync(TestContext.CancellationToken));
        var consent = (await review.Content.ReadFromJsonAsync<OAuthConsentDto>(TestContext.CancellationToken))!;

        using var approval = await asTheUser.PostAsJsonAsync("api/v1/OAuth/Approve", request, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, approval.StatusCode, await approval.Content.ReadAsStringAsync(TestContext.CancellationToken));
        var redirect = (await approval.Content.ReadFromJsonAsync<OAuthApprovalDto>(TestContext.CancellationToken))!.RedirectUrl!;

        // The AI app's own server exchanges the code, with nobody signed in.
        using var asTheAIApp = server.CreateRawHttpClient();
        using var exchange = await asTheAIApp.PostAsync("oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = QueryHelpers.ParseQuery(new Uri(redirect).Query)["code"].ToString(),
            ["client_id"] = AIAppClientId,
            ["redirect_uri"] = AIAppRedirectUri,
            ["code_verifier"] = verifier,
            ["resource"] = request.Resource
        }), TestContext.CancellationToken);

        var token = await exchange.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, exchange.StatusCode, $"{token}");

        return (token.GetProperty("access_token").GetString()!, consent.TenantName);
    }
}
