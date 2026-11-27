using Bit.Bcrdt;
using Microsoft.AspNetCore.WebUtilities;

namespace Bit.Fabric.Workbench.Infrastructure.Services;

/// <summary>
/// What the coding agent does on the review, over HTTP: read, wait for, answer and track comments, set page statuses
/// and take screenshots. It edits the server's copy of the review document, which syncs to every open browser.
/// </summary>
public sealed partial class WorkbenchReviewAgent
{
    /// <summary>The longest a wait for new comments lasts before it returns empty.</summary>
    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(55);

    [AutoInject] private IServiceProvider serviceProvider = default!;
    [AutoInject] private WorkbenchSnapshots snapshots = default!;
    [AutoInject] private WorkbenchReviewAttachments attachments = default!;
    [AutoInject] private WorkbenchScreenshotService screenshotService = default!;

    // Completed and replaced on every update of the document, which wakes the waiters.
    private TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private BcrdtDocumentHost Host => serviceProvider.GetRequiredService<BcrdtDocumentHost>();

    /// <summary>Wakes every waiter; the document host calls it for each update it receives.</summary>
    public void Pulse() => Interlocked.Exchange(ref changed, new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();

    public async Task<List<WorkbenchReviewThread>> GetThreads(CancellationToken cancellationToken)
    {
        return await Host.ReadAsync(WorkbenchReviewDocument.Id, document => WorkbenchReviewDocument.ReadThreads(document.GetMap("threads")), cancellationToken);
    }

    public async Task<Dictionary<string, WorkbenchPageReview>> GetPages(CancellationToken cancellationToken)
    {
        return await Host.ReadAsync(WorkbenchReviewDocument.Id, document => WorkbenchReviewDocument.ReadPages(document.GetMap("pages")), cancellationToken);
    }

    /// <summary>
    /// The review as the agent reads it from get_review and GET /api/review: the comments (done ones too when asked),
    /// the page statuses and the cursor to wait from. With pages, only the comments on those pages.
    /// </summary>
    public async Task<object> DescribeReview(bool includeDone, IReadOnlySet<string>? onPages, CancellationToken cancellationToken)
    {
        var (threads, pages) = await Host.ReadAsync(WorkbenchReviewDocument.Id, document => (WorkbenchReviewDocument.ReadThreads(document.GetMap("threads")),
                                                                                              WorkbenchReviewDocument.ReadPages(document.GetMap("pages"))), cancellationToken);

        threads = [.. threads.Where(t => onPages is null || onPages.Contains(t.PageKey))];

        return WorkbenchReviewJson.Describe(threads.Where(t => includeDone || t.Status is not WorkbenchReviewStatus.Done), pages, threads.Select(t => t.Sequence).DefaultIfEmpty(0).Max());
    }

    /// <summary>
    /// Returns the comments that aren't done and changed after the cursor, as soon as there are any, or none once the timeout passes.
    /// With pages, only the comments on those pages, so each of several chats hears about its own.
    /// </summary>
    public async Task<(long Cursor, List<WorkbenchReviewThread> Threads)> Wait(string agentName, long since, TimeSpan timeout, CancellationToken cancellationToken, IReadOnlySet<string>? onPages = null)
    {
        await Touch(agentName, null, cancellationToken);

        var deadline = DateTimeOffset.UtcNow + (timeout > MaxWait ? MaxWait : timeout);

        while (true)
        {
            var signal = Volatile.Read(ref changed).Task;
            var fresh = (await GetThreads(cancellationToken)).Where(t => t.Sequence > since && t.Status is not WorkbenchReviewStatus.Done && (onPages is null || onPages.Contains(t.PageKey))).ToList();

            if (fresh.Count > 0)
                return (fresh.Max(t => t.Sequence), fresh);

            var remaining = deadline - DateTimeOffset.UtcNow;

            if (remaining <= TimeSpan.Zero)
                return (since, []);

            // The delay's timer is cancelled as soon as the signal wins, so a busy review doesn't pile timers up.
            using (var delay = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                await Task.WhenAny(signal, Task.Delay(remaining, delay.Token));
                delay.Cancel();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public Task<bool> Reply(string agentName, string threadId, string text, CancellationToken cancellationToken)
    {
        return Edit(agentName, threadId, threads => WorkbenchReviewDocument.AddMessage(threads, threadId, fromAgent: true, agentName, text), cancellationToken);
    }

    /// <summary>Sets a comment's status; while it is working, the review shows the agent on that comment.</summary>
    public Task<bool> SetStatus(string agentName, string threadId, WorkbenchReviewStatus status, CancellationToken cancellationToken)
    {
        return Edit(agentName, status is WorkbenchReviewStatus.Working ? threadId : null, threads => WorkbenchReviewDocument.SetStatus(threads, threadId, status, fromAgent: true), cancellationToken);
    }

    public Task<bool> SetTasks(string agentName, string threadId, IEnumerable<WorkbenchReviewTask> tasks, CancellationToken cancellationToken)
    {
        return Edit(agentName, threadId, threads => WorkbenchReviewDocument.SetTasks(threads, threadId, tasks), cancellationToken);
    }

    public async Task<bool> SetPageStatus(string agentName, string pageKey, WorkbenchPageStatus status, CancellationToken cancellationToken)
    {
        if (WorkbenchPageCatalog.Find(pageKey) is not { } page)
            return false;

        await Host.EditAsync(WorkbenchReviewDocument.Id, document =>
        {
            WorkbenchReviewDocument.SetPageStatus(document.GetMap("pages"), page.Key, status, agentName);
            WorkbenchReviewDocument.SetAgent(document.GetMap("agent"), agentName, null);
        }, cancellationToken);

        return true;
    }

    /// <summary>Captures the view a comment was left on, with the page state saved with it.</summary>
    public async Task<(byte[] Png, string FileName)?> Screenshot(string threadId, CancellationToken cancellationToken)
    {
        var thread = (await GetThreads(cancellationToken)).FirstOrDefault(t => t.Id == threadId);

        if (thread is null || WorkbenchPageCatalog.Find(thread.PageKey) is not { } page)
            return null;

        var snapshotId = thread.HasState ? snapshots.Add(await attachments.ReadState(thread.Id)) : null;
        var query = thread.Scenario.ToQueryString();

        return await screenshotService.CapturePage(page, snapshotId is null ? query : QueryHelpers.AddQueryString(query, WorkbenchUrls.SnapshotQueryKey, snapshotId), cancellationToken);
    }

    /// <summary>Records that the agent is online, and the comment it works on.</summary>
    private async Task Touch(string agentName, string? threadId, CancellationToken cancellationToken)
    {
        await Host.EditAsync(WorkbenchReviewDocument.Id, document => WorkbenchReviewDocument.SetAgent(document.GetMap("agent"), agentName, threadId), cancellationToken);
    }

    /// <summary>Edits the comments and records the agent in one change; returns whether the comment was found.</summary>
    private async Task<bool> Edit(string agentName, string? activeThreadId, Func<Bit.Bcrdt.BcrdtMap, bool> edit, CancellationToken cancellationToken)
    {
        var done = false;

        await Host.EditAsync(WorkbenchReviewDocument.Id, document =>
        {
            done = edit(document.GetMap("threads"));
            WorkbenchReviewDocument.SetAgent(document.GetMap("agent"), agentName, activeThreadId);
        }, cancellationToken);

        return done;
    }
}
