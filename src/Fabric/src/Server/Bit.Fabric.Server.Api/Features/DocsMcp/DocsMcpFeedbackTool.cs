using System.Text.Json.Nodes;
using System.Threading.RateLimiting;
using FluentEmail.Core;
using ModelContextProtocol.Protocol;

namespace Bit.Fabric.Server.Api.Features.DocsMcp;

/// <summary>
/// The one tool of /docs-mcp that writes rather than reads: it emails what an agent reports to the team that maintains the
/// bit platform, at <see cref="DocsMcpOptions.FeedbackEmailAddress"/>. A singleton, for the limit it keeps per caller.
/// </summary>
public class DocsMcpFeedbackTool(ServerApiSettings settings, ILogger<DocsMcpFeedbackTool> logger) : IDisposable
{
    public const string ToolName = "SendBitPlatformFeedback";

    /// <summary>A report longer than this belongs in a GitHub issue.</summary>
    private const int MaxReportLength = 4000;

    private const int MaxTitleLength = 200;

    private const string IssuesUrl = "https://github.com/bitfoundation/bitplatform/issues";

    /// <summary>
    /// The endpoint is public and needs no sign-in, and this tool reaches people rather than a cache, so a caller address
    /// sends five reports in five minutes at most.
    /// </summary>
    private readonly PartitionedRateLimiter<string> rateLimiter = PartitionedRateLimiter.Create<string, string>(
        callerAddress => RateLimitPartition.GetFixedWindowLimiter(callerAddress, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(5)
        }));

    public static readonly Tool Definition = new()
    {
        Name = ToolName,
        Title = "Send feedback to the bit platform team",
        Description = $"""
            Sends a defect report, or any other feedback, about the bit platform straight to the team that maintains
            it: the {BitLibraries.Names(BitLibraries.All)} libraries, the bit Boilerplate project template, and
            this endpoint itself. It reaches people rather than an issue tracker, so call it only once the
            user has agreed that the report should be sent - never to take a note, and never to ask whether something
            is a bug. Write the report so that it stands on its own without the conversation it came out of: what you
            did, what happened, what you expected instead, the versions involved and the shortest repro you have,
            marking plainly what you measured and what you are only inferring.
            """,
        InputSchema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["title"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "One line naming the defect, such as \"BitDropdown loses its selection after a virtualized scroll\"."
                },
                ["report"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = $"The report itself, at most {MaxReportLength} characters."
                }
            },
            ["required"] = new JsonArray("title", "report")
        }),
        Annotations = new()
        {
            // Every other tool on this endpoint only reads. This one reaches people, so a client is right to ask before it
            // runs, and right to not retry it on its own.
            ReadOnlyHint = false,
            DestructiveHint = false,
            IdempotentHint = false,
            OpenWorldHint = true
        }
    };

    /// <summary>
    /// Sends what the agent wrote, stamped with the release and the project it connected for - the only facts this server
    /// is certain of, whatever the report itself claims.
    /// </summary>
    /// <param name="request">The call, with the title and report the agent wrote.</param>
    /// <param name="connection">The address the agent connected with.</param>
    /// <param name="requestServices">The request's scope, which the app's email sender is resolved from.</param>
    /// <param name="cancellationToken">Ends the call.</param>
    public async ValueTask<CallToolResult> Call(CallToolRequestParams request, DocsMcpConnection connection, IServiceProvider requestServices, CancellationToken cancellationToken)
    {
        var teamAddress = settings.DocsMcp.FeedbackEmailAddress;

        if (string.IsNullOrWhiteSpace(teamAddress))
            return DocsMcpToolResults.Failed($"This server has no channel to the team configured, so nothing was sent. Open an issue on {IssuesUrl} instead.");

        var title = DocsMcpToolResults.Argument(request, "title");
        var report = DocsMcpToolResults.Argument(request, "report");

        if (title is null || report is null)
            return DocsMcpToolResults.Failed("Both title and report are required and neither may be empty. Nothing was sent.");

        if (title.Length > MaxTitleLength)
            return DocsMcpToolResults.Failed($"The title is {title.Length} characters long; keep it under {MaxTitleLength}. Nothing was sent.");

        if (report.Length > MaxReportLength)
            return DocsMcpToolResults.Failed($"The report is {report.Length} characters long. Trim it under {MaxReportLength}, or open an issue on {IssuesUrl} if it genuinely needs more room. Nothing was sent.");

        using var lease = rateLimiter.AttemptAcquire(connection.CallerAddress);

        if (lease.IsAcquired is false)
            return DocsMcpToolResults.Failed($"Too many reports have been sent from this address. Wait a few minutes, or open an issue on {IssuesUrl}. Nothing was sent.");

        // The agent tells the user their report is with the team, so it is told whether it really is; why not lands in this
        // server's logs, where the caller could do nothing with it anyway. Plain text, so nothing an agent writes renders.
        var sent = await requestServices.GetRequiredService<IFluentEmail>()
            .To(teamAddress)
            .Subject($"bit platform feedback: {title.ReplaceLineEndings(" ")}")
            .Body($"""
                Release: {connection.RequestedVersion ?? "-"}
                Project: {connection.ProjectId ?? "-"}

                {report}
                """, isHtml: false)
            .SendAsync(cancellationToken);

        if (sent.Successful is false)
        {
            logger.LogError("A report titled {FeedbackTitle} could not be emailed to the team: {Errors}", title, string.Join("; ", sent.ErrorMessages));
            return DocsMcpToolResults.Failed($"The report could not be delivered to the team. Open an issue on {IssuesUrl} instead.");
        }

        logger.LogInformation("A report titled {FeedbackTitle} was sent to the team through the {ToolName} tool.", title, ToolName);

        return DocsMcpToolResults.Succeeded("The report reached the bit platform team. Tell the user it was sent.");
    }

    public void Dispose() => rateLimiter.Dispose();
}
