using System.Text;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Shared.Features.Monitoring;

/// <summary>
/// The prompt a project's Troubleshoot button hands to the customer's own AI, the one that works on their app's code
/// (bitfabric-internal/specs/monitoring.md, Monitoring keeps watching when the approval breaks). It is in English whatever
/// the viewer's language, since an AI reads it, and is filled in from what bit Fabric knows of the environment: for one that
/// needs reconnecting, the problem, the addresses of the OAuth flow and what to check for that problem; for any other, how
/// it is doing, the parts that fail, the last deployment, and the project's own troubleshoot skill and dev MCP. It never
/// carries a token or an error text, which bit Fabric doesn't have. The server builds it for the api and the AI tools, and
/// the Workbench for its sample projects.
/// </summary>
public static class ProjectTroubleshootPrompt
{
    private static readonly CultureInfo english = CultureInfo.GetCultureInfo("en");

    /// <summary>The prompt that fits the environment now: why it needs reconnecting, or how the app is doing.</summary>
    public static TroubleshootPromptDto For(TroubleshootFacts project, MonitoringDetailsDto details)
        => project.ConnectionStatus is ProjectConnectionStatus.ReconnectNeeded
            ? new() { Kind = TroubleshootPromptKind.Connection, Prompt = ConnectionPrompt(project, details) }
            : new() { Kind = TroubleshootPromptKind.App, Prompt = AppPrompt(project, details) };

    public static string ConnectionPrompt(TroubleshootFacts project, MonitoringDetailsDto details)
    {
        var server = (project.ServerAddress ?? project.Address)!.TrimEnd('/');
        var resource = project.Resource?.TrimEnd('/') ?? $"{server}/healthz";
        var issuer = project.Issuer?.TrimEnd('/') ?? server;
        var tokenEndpoint = project.TokenEndpoint ?? $"{issuer}/oauth/token";
        var code = ProjectConnectionProblems.CodeOf(project.ConnectionProblem);
        var problem = ProjectConnectionProblems.Describe(project.ConnectionProblem, english) ?? "bit Fabric can't read the report.";

        StringBuilder md = new();

        md.AppendLine("bit Fabric (https://bitfabric.co), which monitors my app, can't read the app's detailed health report anymore. Please find out why and fix it. This repository holds the app's code; the app was made from the bit Boilerplate project template.").AppendLine();

        AppendTheApp(md, project, details);

        md.AppendLine("## What bit Fabric sees").AppendLine();
        md.Append("- The problem: ").Append(problem);
        if (code is not null)
        {
            md.Append(" (`").Append(code).Append("`)");
        }
        md.AppendLine();
        if (project.ReportFailingSince is { } failingSince)
        {
            md.Append("- First seen: ").AppendLine(Utc(failingSince));
        }
        md.Append("- bit Fabric still checks the home page and the public `").Append(server).AppendLine("/health` every minute, so uptime, incidents and alerts go on. Only the detailed report is paused: each health check, the build that runs, deployments and the scans.");
        md.AppendLine("- bit Fabric tries again by itself every 15 minutes, and reconnects on its own once the report can be read.").AppendLine();

        md.AppendLine("## How bit Fabric reads the report").AppendLine();
        md.AppendLine("bit Fabric is an OAuth 2.1 client of the app, and reads nothing else:").AppendLine();
        md.Append("1. `GET ").Append(server).AppendLine("/.well-known/oauth-protected-resource/healthz` (RFC 9728) names the authorization server, and `healthz` among `scopes_supported`.");
        md.Append("2. `GET ").Append(issuer).AppendLine("/.well-known/oauth-authorization-server` (RFC 8414) gives the authorization and token endpoints.");
        md.AppendLine("3. A user of the app with two-factor authentication on and the `System.Operations_View` feature approved bit Fabric: the authorization code flow with PKCE, for the `healthz` scope. bit Fabric's client id is the address of its client metadata document, which the template accepts without registration.");
        md.Append("4. bit Fabric renews its access token at `").Append(tokenEndpoint).Append("` with the `refresh_token` grant and `resource=").Append(resource).Append("`, then reads `GET ").Append(resource).AppendLine("/v1` with `Authorization: Bearer <token>`.").AppendLine();

        md.AppendLine("The template's code behind it, on the server: `OAuthResources`, `OAuthEndpoints` and `OAuthTokenService` (the token endpoint), `OAuthController` (the approval), and `MapAppHealthChecks` (`/health` and `/healthz`). The template ships a test of this flow in the default test run.").AppendLine();

        md.AppendLine("## What to check first").AppendLine();
        md.AppendLine(code switch
        {
            ProjectConnectionProblems.ApprovalWithdrawn or ProjectConnectionProblems.NoLongerAccepted => "- The token endpoint refused to renew the approval (`invalid_grant`, with the reason above when it gave one): the app doesn't know bit Fabric's approval anymore. That is expected when someone ended bit Fabric's session in the app's Settings, Sessions, when the user who approved it was deleted, lost two-factor authentication or changed their password, or when the app's database was recreated or restored. If one of those happened, nothing needs fixing: tell me to click Reconnect in bit Fabric. If none did, find out why the session or its OAuth grant is gone: the server's logs around the time above, and the code that deletes sessions.",
            ProjectConnectionProblems.ReportGone => "- One of the addresses above answers 404: the app no longer serves the report, so a reconnect can't help until it does. Check each one with the commands below, then compare the code behind them with the template's (`bit diff`, or the template's own files): a removed or renamed endpoint, a changed route prefix, `MapAppHealthChecks` no longer called, or a reverse proxy that doesn't forward these paths.",
            ProjectConnectionProblems.ServerError => "- For an hour, the token endpoint has answered with a server error or not at all. Read the server's logs and traces around `/oauth/token` (below): an exception in `OAuthTokenService`, the database out of reach or a migration not applied, the Data Protection keys or the signing certificate changed or missing, or a proxy in front of the server failing.",
            ProjectConnectionProblems.ReportFailing => "- For an hour, `/healthz/v1` has failed with the token (the failure above) while the public `/health` said the app is up, so the code that builds the detailed report is what fails. Read the server's logs and traces around `/healthz/v1` (below) for its exception, then check `MapAppHealthChecks` and the report's writer against the template's, and any health check or middleware added since that throws or never ends when the report runs.",
            ProjectConnectionProblems.Refused => "- `/healthz/v1` answered 401 even to a token the app had just issued. Check that the signing certificate didn't change between issuing and reading, that the address bit Fabric uses is among the server's trusted origins (the token's issuer), that the `/healthz` authorization in `MapAppHealthChecks` still accepts the OAuth tokens of `OAuthResources`, and the server's clock.",
            ProjectConnectionProblems.ApproverLostAccess => "- `/healthz/v1` answered 403: the token is valid, but the user who approved bit Fabric no longer has the `System.Operations_View` feature, through their roles or directly. Give it back, or have someone who has it click Reconnect in bit Fabric. Change no code unless that feature's check itself is broken.",
            ProjectConnectionProblems.ApprovalExpired or ProjectConnectionProblems.NoApproval => "- bit Fabric holds no approval it can renew. Someone who has two-factor authentication and the `System.Operations_View` feature in the app has to click Reconnect in bit Fabric; check that such a user exists and can sign in.",
            _ => "- The token endpoint refused to renew the approval with the error above. Read what the app's `OAuthTokenService` answers for a `refresh_token` grant with that error, and why it would for bit Fabric's client id."
        });
        md.AppendLine();

        md.AppendLine("Then, whatever the problem:").AppendLine();
        md.Append("- `curl -i ").Append(server).AppendLine("/.well-known/oauth-protected-resource/healthz`: 200 with JSON naming `healthz` and `bit_template_version`.");
        md.Append("- `curl -i ").Append(issuer).AppendLine("/.well-known/oauth-authorization-server`: 200 with JSON.");
        md.Append("- `curl -i ").Append(resource).AppendLine("/v1` without a token: 401. A 404 means it isn't served; a 200 means the report is open to anyone, which must be fixed.");
        md.Append("- `curl -i -X POST ").Append(tokenEndpoint).AppendLine(" -d grant_type=refresh_token -d refresh_token=unknown -d client_id=test`: 400 with an OAuth error such as `invalid_grant`. A 500 there is a bug: it tells every client the server is failing, rather than that the approval is gone.");
        md.Append("- `curl -i ").Append(server).AppendLine("/health`: 200 with Healthy or Degraded, or 503 with Unhealthy.");
        AppendTheDeployment(md, project);
        md.AppendLine("- Run the project's tests (`dotnet test`); a failing test of the OAuth flow or of `/healthz` points at the cause.").AppendLine();

        AppendTheRules(md);

        md.AppendLine("## When you're done").AppendLine();
        md.AppendLine("Tell me the cause, what you changed and how you checked it, and whether I need to click Reconnect on the project's page in bit Fabric. A fix in the code reaches bit Fabric once it is deployed.");

        return md.ToString();
    }

    public static string AppPrompt(TroubleshootFacts project, MonitoringDetailsDto details)
    {
        var dto = details.Monitoring!;
        var failing = details.Checks.Where(check => check.IsNotConfigured is false && check.Status is not HealthCheckStatus.Healthy)
                                    .OrderBy(check => check.Status is HealthCheckStatus.Unhealthy ? 0 : 1)
                                    .ToArray();
        var notConfigured = details.Checks.Where(check => check.IsNotConfigured).ToArray();

        StringBuilder md = new();

        md.AppendLine(dto.Health switch
        {
            ProjectHealth.Down => "bit Fabric (https://bitfabric.co), which monitors my app, says the app is down. Please find the cause and fix it.",
            ProjectHealth.Degraded => "bit Fabric (https://bitfabric.co), which monitors my app, says part of the app isn't working properly. Please find the cause and fix it.",
            _ => "bit Fabric (https://bitfabric.co), which monitors my app, sees no problem right now. Please review what it reports below, the recent incidents and the parts that miss their settings, and tell me what deserves attention."
        });
        md.AppendLine("This repository holds the app's code; the app was made from the bit Boilerplate project template.").AppendLine();

        AppendTheApp(md, project, details);

        md.AppendLine("## What bit Fabric sees").AppendLine();
        md.Append("- Health: ").Append(dto.Health).Append(dto.LastCheckedAt is { } checkedAt ? $", last checked {Utc(checkedAt)}" : "").AppendLine(".");
        if (dto.OngoingIncident is { } incident)
        {
            md.Append("- Ongoing incident: \"").Append(incident).Append('"').Append(dto.OngoingIncidentSince is { } since ? $", since {Utc(since)}" : "").AppendLine(".");
        }
        if (details.ApplicationVersion is { } version)
        {
            md.Append("- The build that runs: `").Append(version).Append('`').Append(details.LastDeployedAt is { } deployedAt ? $", deployed {Utc(deployedAt)}" : "").AppendLine(".");
        }
        if (details.HomePageMs is { } homePageMs)
        {
            md.Append("- The home page answered in ").Append(homePageMs).AppendLine(" ms in the last round.");
        }
        if (failing.Length > 0)
        {
            md.AppendLine("- The parts that fail, from the app's own health report:");
            foreach (var check in failing)
            {
                md.Append("  - ").Append(TitleOf(check)).Append(" (`").Append(check.Key).Append("`): ").Append(check.Status)
                  .Append(check.Since is { } since ? $" since {Utc(since)}" : "")
                  .Append(string.IsNullOrWhiteSpace(check.Description) ? "" : $". The app says: {check.Description.Trim()}")
                  .AppendLine();
            }
        }
        else if (dto.Health is ProjectHealth.Down)
        {
            md.AppendLine("- The app's server didn't answer, or answered without a health report.");
        }
        if (notConfigured.Length > 0)
        {
            md.Append("- Parts that miss their settings (a setup finding, not an outage): ").AppendLine(string.Join(", ", notConfigured.Select(check => $"{TitleOf(check)} (`{check.Key}`)")));
        }
        md.AppendLine("- bit Fabric never receives the errors' texts: they stay in the app's own logs.").AppendLine();

        md.AppendLine("## How to investigate").AppendLine();
        md.AppendLine("1. If your tool has the project's `troubleshoot` skill (`.github/agents/troubleshoot.agent.md`, or `/troubleshoot`), follow it: it knows this project and its environments.");
        AppendTheDeployment(md, project, numbered: 2);
        md.AppendLine("3. If the problem began after a deployment, read what that deployment changed (`git log`) next to the failing parts.");
        md.AppendLine("4. Reproduce it with an automated test before changing code, then fix the cause, not the symptom, and see the test pass.").AppendLine();

        AppendTheRules(md);

        md.AppendLine("## When you're done").AppendLine();
        md.AppendLine("Tell me the cause, what you changed and how you checked it. Once the fix is deployed, bit Fabric closes the incident by itself after the parts pass two rounds in a row.");

        return md.ToString();
    }

    private static void AppendTheApp(StringBuilder md, TroubleshootFacts project, MonitoringDetailsDto details)
    {
        md.AppendLine("## The app").AppendLine();
        md.Append("- Environment: ").Append(project.Environment).Append(" of ").AppendLine(details.Monitoring?.ProjectName ?? project.Name);
        md.Append("- Address: ").AppendLine(project.Address);
        if (project.ServerAddress is { } serverAddress && serverAddress != project.Address)
        {
            md.Append("- Server: ").AppendLine(serverAddress);
        }
        if (string.IsNullOrWhiteSpace(project.TemplateVersion) is false)
        {
            md.Append("- bit Boilerplate version it reports: ").AppendLine(project.TemplateVersion);
        }
        md.AppendLine();
    }

    /// <summary>The environment's dev MCP, which every template project has, named as its MCP files name it.</summary>
    private static void AppendTheDeployment(StringBuilder md, TroubleshootFacts project, int? numbered = null)
    {
        var name = $"dev-mcp-{(project.Environment ?? "production").Trim().ToLowerInvariant().Replace(' ', '-')}";
        md.Append(numbered is { } number ? $"{number}. " : "- ")
          .Append("Read the deployment's real state through the project's dev MCP server (`").Append(name)
          .AppendLine("` in the project's MCP files, signed in as a global admin): `GetHealth` for each check with its full exception, `ListLogs` and `ListTraces` for errors around the times above, `GetDeploymentInfo` for the settings in effect and `GetAppliedMigrations`. If it isn't connected, ask me to sign in to it rather than guess.");
    }

    private static void AppendTheRules(StringBuilder md)
    {
        md.AppendLine("## Rules").AppendLine();
        md.AppendLine("- Find and explain the cause before changing anything.");
        md.AppendLine("- Keep `/healthz` behind its OAuth authorization; never make the detailed report public.");
        md.AppendLine("- Never write a token, a password or a connection string into your answer, a file or a commit.").AppendLine();
    }

    private static string TitleOf(ProjectCheckDto check) => string.IsNullOrWhiteSpace(check.Title) ? check.Key ?? "" : check.Title.Trim();

    private static string Utc(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}

/// <summary>What a troubleshooting prompt says of an environment besides its details, from bit Fabric's own records.</summary>
/// <param name="ConnectionProblem">The stored ProjectConnectionProblems code, with its detail.</param>
/// <param name="Issuer">The app's OAuth issuer, as its approval named it; null for an environment never connected.</param>
/// <param name="ReportFailingSince">Since when a problem that may pass has kept the report from being read.</param>
public sealed record TroubleshootFacts(string? Name,
    string? Environment,
    string? Address,
    string? ServerAddress,
    string? TemplateVersion,
    ProjectConnectionStatus ConnectionStatus,
    string? ConnectionProblem,
    string? Issuer,
    string? TokenEndpoint,
    string? Resource,
    DateTimeOffset? ReportFailingSince);
