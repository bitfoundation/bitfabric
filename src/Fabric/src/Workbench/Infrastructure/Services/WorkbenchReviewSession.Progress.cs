using Bit.Bcrdt;

namespace Bit.Fabric.Workbench.Infrastructure.Services;

/// <summary>
/// The agents' tasks of the Progress page in this circuit. On this machine, or for a reviewer an invite link was opened
/// for, the page shows the tasks and what is done on them reaches the agent; any other visitor sees none.
/// </summary>
public sealed partial class WorkbenchReviewSession
{
    [AutoInject] private WorkbenchSecrets secrets = default!;

    /// <summary>The agents' tasks, newest change first.</summary>
    public IReadOnlyList<WorkbenchProgressTask> Tasks { get; private set; } = [];

    /// <summary>Whether this circuit sees the tasks and can act on them.</summary>
    public bool CanSeeTasks => SavesComments;

    /// <summary>The tasks the page shows: the agents' own, or none to a visitor who may not see them.</summary>
    public IReadOnlyList<WorkbenchProgressTask> ShownTasks => CanSeeTasks ? Tasks : [];

    /// <summary>How many questions and steps of the tasks wait for the user.</summary>
    public int NeedsYouCount => CanSeeTasks ? Tasks.Sum(t => t.NeedsYouCount) : 0;

    public bool CanAnswer(WorkbenchProgressTask task) => task.IsFinished is false && CanSeeTasks;

    /// <summary>Only this machine removes a task.</summary>
    public bool CanDelete(WorkbenchProgressTask task) => Access.IsLocal;

    /// <summary>Answers a question of the task; returns false when the answer doesn't fit it or it was answered meanwhile.</summary>
    public bool Answer(WorkbenchProgressTask task, string askId, IReadOnlyList<string> choices, string? text, string by)
    {
        return EditProgress(task, progress => WorkbenchProgressDocument.Answer(progress, task.Id, askId, choices, text, by));
    }

    public bool CompleteStep(WorkbenchProgressTask task, string stepId, string by)
    {
        return EditProgress(task, progress => WorkbenchProgressDocument.CompleteStep(progress, task.Id, stepId, by));
    }

    public bool SendMessage(WorkbenchProgressTask task, string text, string by)
    {
        return EditProgress(task, progress => WorkbenchProgressDocument.AddMessage(progress, task.Id, text, by));
    }

    /// <summary>
    /// Tells the task's agent that the user submitted their picks on a board (BoardSubmit); false when the task is over or this
    /// circuit may not act on it.
    /// </summary>
    public bool SubmitBoard(WorkbenchProgressTask task, string boardId, string title, string by)
    {
        return CanAnswer(task) && EditProgress(task, progress => WorkbenchProgressDocument.AddBoardSubmission(progress, task.Id, boardId, title, by));
    }

    /// <summary>Whether the user may take a note or a line of the activity off a task: wherever they may act on it, a finished one too.</summary>
    public bool CanRemoveItems(WorkbenchProgressTask task) => CanSeeTasks;

    /// <summary>Takes a note off the task; its agent learns of it and doesn't add it again.</summary>
    public bool RemoveNote(WorkbenchProgressTask task, string noteId, string by)
    {
        return CanRemoveItems(task) && EditProgress(task, progress => WorkbenchProgressDocument.RemoveNote(progress, task.Id, noteId, by));
    }

    /// <summary>Takes a line off the task's activity; its agent learns of it.</summary>
    public bool RemoveEntry(WorkbenchProgressTask task, string entryId, string by)
    {
        return CanRemoveItems(task) && EditProgress(task, progress => WorkbenchProgressDocument.RemoveEntry(progress, task.Id, entryId, by));
    }

    /// <summary>
    /// Whether this circuit may put a secret where a question says: in dev mode on this machine only, as gh runs in the user's
    /// name there, so never an invited reviewer elsewhere.
    /// </summary>
    public bool CanStoreSecrets(WorkbenchProgressTask task) => CanWrite;

    /// <summary>
    /// Puts what the user pasted into a secret question where the question says, then marks it answered; the secret itself
    /// never enters the document. Returns what went wrong, or null once it is stored.
    /// </summary>
    public async Task<string?> StoreSecret(WorkbenchProgressTask task, WorkbenchProgressAsk ask, string secret, string by, CancellationToken cancellationToken)
    {
        var failed = localizer[nameof(WorkbenchStrings.ProgressAnswerFailed)].Value;

        if (CanAnswer(task) is false || CanStoreSecrets(task) is false || ask.Kind is not WorkbenchProgressAskKind.Secret || string.IsNullOrWhiteSpace(secret))
            return failed;

        // Read again, so a question taken back or answered meanwhile stores nothing.
        if (Tasks.FirstOrDefault(t => t.Id == task.Id)?.Asks.FirstOrDefault(a => a.Id == ask.Id) is not { IsOpen: true } current)
            return failed;

        if (await secrets.Store(task.Id, current, secret.Trim(), cancellationToken) is { } problem)
            return localizer[nameof(WorkbenchStrings.ProgressSecretStoreFailed), problem].Value;

        return EditProgress(task, progress => WorkbenchProgressDocument.MarkStored(progress, task.Id, ask.Id, by)) ? null : failed;
    }

    public void DeleteTask(WorkbenchProgressTask task)
    {
        if (CanDelete(task))
        {
            document?.Transact(_ => document.GetMap(WorkbenchProgressDocument.MapName).Delete(task.Id), null);
            WorkbenchSecrets.DeleteFiles(task.Id);
        }
    }

    /// <summary>Edits the task in the document when this circuit may.</summary>
    private bool EditProgress(WorkbenchProgressTask task, Func<BcrdtMap, bool> edit)
    {
        var done = false;

        if (CanSeeTasks && document is not null)
        {
            document.Transact(_ => done = edit(document.GetMap(WorkbenchProgressDocument.MapName)), null);
        }

        return done;
    }

    private void RefreshTasks()
    {
        Tasks = document is null ? [] : WorkbenchProgressDocument.ReadTasks(document.GetMap(WorkbenchProgressDocument.MapName));
    }
}
