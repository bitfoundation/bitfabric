using Bit.Bcrdt;

namespace Boilerplate.Workbench.Infrastructure.Services;

/// <summary>
/// One circuit's copy of the review document, synced through the relay: the threads, page statuses, the agent and its tasks.
/// Comments can always be written: on this machine, or from a browser an invite link was opened in (WorkbenchReviewers), they
/// go into the document for the agent that connects, and from anywhere else they stay in the circuit. Everything else writes
/// only in dev mode, for requests made on this machine.
/// </summary>
public sealed partial class WorkbenchReviewSession : IAsyncDisposable
{
    [AutoInject] private IHttpContextAccessor httpContextAccessor = default!;
    [AutoInject] private IConfiguration configuration = default!;
    [AutoInject] private IBcrdtCollaborationService collaboration = default!;
    [AutoInject] private WorkbenchReviewAttachments attachments = default!;
    [AutoInject] private IStringLocalizer<WorkbenchStrings> localizer = default!;

    private (bool CanWrite, bool IsLocal, string? InviteCookie)? access;
    private Task? opening;
    private BcrdtCollaborativeDocument? document;
    private Timer? agentGoneTimer;

    // A visitor's comments, which live and die with the circuit and never reach the document.
    private readonly BcrdtDocument visitorComments = new();
    private readonly Dictionary<string, string> visitorStates = [];

    /// <summary>Raised after the document changes, whoever changed it.</summary>
    public event Action? Changed;

    /// <summary>Whether this circuit may write: in dev mode, with its request from this machine, decided once per circuit.</summary>
    public bool CanWrite => Access.CanWrite;

    /// <summary>
    /// Whether this circuit's comments go into the document, where an agent finds them: on this machine, with or without dev
    /// mode, or from a reviewer elsewhere who opened an invite link.
    /// </summary>
    public bool SavesComments => Access.IsLocal || IsInvitedReviewer;

    /// <summary>
    /// Whether this circuit is a reviewer elsewhere who opened an invite link: their comments reach the agent, nothing else
    /// they do is saved. Checked against the current key each time, so deleting its file revokes an open circuit too.
    /// </summary>
    public bool IsInvitedReviewer => Access is { IsLocal: false, InviteCookie: { } cookie } && WorkbenchReviewers.IsInvited(cookie, configuration);

    // Read together, as the request is only there when the circuit starts.
    private (bool CanWrite, bool IsLocal, string? InviteCookie) Access => access ??= httpContextAccessor.HttpContext is { } context
        ? (WorkbenchLocalRequests.CanChange(context, configuration), WorkbenchLocalRequests.IsLocal(context), context.Request.Cookies[WorkbenchReviewers.CookieName])
        : (false, false, null);

    public bool IsOpen => document is not null;

    public IReadOnlyList<WorkbenchReviewThread> Threads { get; private set; } = [];

    public IReadOnlyDictionary<string, WorkbenchPageReview> Pages { get; private set; } = new Dictionary<string, WorkbenchPageReview>();

    public WorkbenchReviewAgentInfo? Agent { get; private set; }

    /// <summary>
    /// Opens the document once per circuit, under the reviewer's name for presence; calls made meanwhile wait for the same open,
    /// and one made after it failed tries again.
    /// </summary>
    public Task Open(string reviewerName)
    {
        if (opening is { IsFaulted: true } or { IsCanceled: true })
        {
            opening = null;
        }

        return opening ??= OpenDocument(reviewerName);
    }

    private async Task OpenDocument(string reviewerName)
    {
        var id = Guid.NewGuid().ToString("N");

        document = await collaboration.OpenAsync(WorkbenchReviewDocument.Id, new() { User = new(id, reviewerName, BcrdtPresenceService.SuggestColor(id), null) }, CancellationToken.None);
        document.Changed += OnDocumentChanged;

        Refresh();
    }

    public IEnumerable<WorkbenchReviewThread> ThreadsOf(string pageKey) => Threads.Where(t => t.PageKey == pageKey);

    public WorkbenchPageReview? PageOf(string pageKey) => Pages.GetValueOrDefault(pageKey);

    /// <summary>Whether this circuit can answer, finish or delete a comment: on this machine any of them, and from elsewhere only its own.</summary>
    public bool CanChange(string threadId) => SavesComments || visitorComments.GetMap("threads").GetMap(threadId) is not null;

    /// <summary>Adds a comment, with the view's page state when it goes into the document; returns its id, or null when it has no text or the document isn't open.</summary>
    public async Task<string?> AddThread(string pageKey, string query, WorkbenchReviewAnchor anchor, string author, string text, string? state)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var id = Guid.NewGuid().ToString("N")[..16];

        if (SavesComments is false)
        {
            if (state is not null)
            {
                visitorStates[id] = state;
            }

            visitorComments.Transact(_ => WorkbenchReviewDocument.AddThread(visitorComments.GetMap("threads"), id, pageKey, query, anchor, author, text, state is not null));
            OnCommentsChanged();

            return id;
        }

        if (document is null)
            return null;

        var hasState = await attachments.SaveState(id, state);

        document.Transact(_ => WorkbenchReviewDocument.AddThread(document.GetMap("threads"), id, pageKey, query, anchor, author, text, hasState), null);

        return id;
    }

    public void Reply(string threadId, string author, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        EditComments(threadId, threads => WorkbenchReviewDocument.AddMessage(threads, threadId, fromAgent: false, author, text));
    }

    public void SetStatus(string threadId, WorkbenchReviewStatus status)
    {
        EditComments(threadId, threads => WorkbenchReviewDocument.SetStatus(threads, threadId, status, fromAgent: false));
    }

    /// <summary>The page state kept with a comment.</summary>
    public async Task<string?> ReadState(string threadId) => visitorStates.TryGetValue(threadId, out var state) ? state : await attachments.ReadState(threadId);

    /// <summary>Deletes a comment and the page state saved with it.</summary>
    public void Delete(string threadId)
    {
        EditComments(threadId, threads => threads.Delete(threadId));
        visitorStates.Remove(threadId);

        if (SavesComments)
        {
            attachments.DeleteState(threadId);
        }
    }

    /// <summary>Edits a comment where it lives, when this circuit may change it.</summary>
    private void EditComments(string threadId, Action<BcrdtMap> edit)
    {
        if (SavesComments)
        {
            document?.Transact(_ => edit(document.GetMap("threads")), null);
        }
        else if (CanChange(threadId))
        {
            visitorComments.Transact(_ => edit(visitorComments.GetMap("threads")));
            OnCommentsChanged();
        }
    }

    public void SetPageStatus(string pageKey, WorkbenchPageStatus status, string by)
    {
        if (CanWrite is false)
            return;

        document?.Transact(_ => WorkbenchReviewDocument.SetPageStatus(document.GetMap("pages"), pageKey, status, by), null);
    }

    private void OnDocumentChanged(BcrdtUpdateEventArgs args) => OnCommentsChanged();

    // Also when the document never opened, as a visitor's comments don't need it.
    private void OnCommentsChanged()
    {
        Refresh();
        Changed?.Invoke();
    }

    /// <summary>Reads the document into the properties above.</summary>
    private void Refresh()
    {
        List<WorkbenchReviewThread> saved = document is null ? [] : WorkbenchReviewDocument.ReadThreads(document.GetMap("threads"));

        // A visitor's comments are numbered on from the page's saved ones.
        Threads = [.. saved, .. WorkbenchReviewDocument.ReadThreads(visitorComments.GetMap("threads")).Select(t => t with { Number = t.Number + saved.Count(s => s.PageKey == t.PageKey) })];

        if (document is null)
            return;

        Pages = WorkbenchReviewDocument.ReadPages(document.GetMap("pages"));
        Agent = WorkbenchReviewDocument.ReadAgent(document.GetMap("agent"));

        RefreshTasks();
        WatchAgent();
    }

    /// <summary>An agent that stops calling changes nothing in the document, so the review is told once it counts as gone.</summary>
    private void WatchAgent()
    {
        if (Agent is not { IsActive: true } agent)
            return;

        agentGoneTimer ??= new(_ => Changed?.Invoke());
        agentGoneTimer.Change(agent.At + WorkbenchReviewAgentInfo.OnlineFor + TimeSpan.FromSeconds(1) - DateTimeOffset.UtcNow, Timeout.InfiniteTimeSpan);
    }

    public async ValueTask DisposeAsync()
    {
        visitorComments.Dispose();
        samples?.Dispose();

        if (document is null)
            return;

        document.Changed -= OnDocumentChanged;
        agentGoneTimer?.Dispose();
        await collaboration.CloseAsync(WorkbenchReviewDocument.Id);
    }
}
