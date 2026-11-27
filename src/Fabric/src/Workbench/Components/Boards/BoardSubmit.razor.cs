using System.Text.Json.Nodes;

namespace Bit.Fabric.Workbench.Components.Boards;

/// <summary>
/// The Submit bar of a board that asks its reader to pick (BoardFrame.Picks): what they pick is in the board's file as they go
/// (WorkbenchBoardFiles), and Submit keeps it there as final and, when the board names the Progress task of the agent waiting
/// for it, tells that agent at once. Its words are in the board's language. It shows only to the user the agent works for, on
/// this machine or with an invite link, and only on a board opened in the Boards section, which hands it the board.
/// </summary>
public partial class BoardSubmit
{
    [AutoInject] private WorkbenchBoardFiles boardFiles = default!;
    [AutoInject] private WorkbenchReviewSession reviewSession = default!;
    [AutoInject] private BlexManager blexManager = default!;
    [AutoInject] private ILogger<BoardSubmit> logger = default!;

    private WorkbenchBoardPicks? picks;
    private bool isSubmitting;

    [CascadingParameter] private WorkbenchBoard? Board { get; set; }

    /// <summary>The board's title, which the agent's task shows with the submit.</summary>
    [Parameter, EditorRequired] public string Title { get; set; } = default!;

    /// <summary>The language the board is written in (BoardFrame.Language), which these words follow.</summary>
    [Parameter, EditorRequired] public string Language { get; set; } = default!;

    /// <summary>The id of the Progress task whose agent waits for the picks; none to only keep them in the file.</summary>
    [Parameter] public string? ProgressTask { get; set; }

    private CultureInfo Culture
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(Language);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.CurrentUICulture;
            }
        }
    }

    private bool IsRightToLeft => Culture.TextInfo.IsRightToLeft;

    private bool IsSubmitted => picks?.IsSubmitted is true;

    private string ButtonText => Text(IsSubmitted ? nameof(WorkbenchStrings.BoardSubmitted) : picks?.Submitted is null ? nameof(WorkbenchStrings.BoardSubmit) : nameof(WorkbenchStrings.BoardSubmitAgain));

    private string Status => picks?.Submitted is not { } submitted
        ? Text(nameof(WorkbenchStrings.BoardPicksSaved))
        : Text(IsSubmitted ? nameof(WorkbenchStrings.BoardPicksSubmittedAt) : nameof(WorkbenchStrings.BoardPicksChangedSince), Time(submitted.At));

    protected override async Task OnInitializedAsync()
    {
        boardFiles.Changed += OnFileChanged;

        await Load();

        await base.OnInitializedAsync();
    }

    private async Task Load()
    {
        if (Board is not null)
        {
            picks = await boardFiles.Read(Board.Id, CurrentCancellationToken);
        }
    }

    // A save of this board, from this window or another, or a submit elsewhere, changes what the bar says.
    private void OnFileChanged(string boardId)
    {
        if (string.Equals(boardId, Board?.Id, StringComparison.OrdinalIgnoreCase) is false)
            return;

        _ = InvokeAsync(async () =>
        {
            try
            {
                await Load();
                StateHasChanged();
            }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
            {
            }
        });
    }

    private async Task Submit()
    {
        if (Board is null || isSubmitting)
            return;

        isSubmitting = true;

        try
        {
            // What waits to be saved is saved first, so the file never ends up older than what was submitted.
            await Persistor.FlushAsync();

            var (state, version) = StoreState(Board.Id);
            var by = WorkbenchStore.ReviewerName ?? Localizer[nameof(WorkbenchStrings.ProgressYou)];

            if ((picks = await boardFiles.Submit(Board.Id, state, version, by, CurrentCancellationToken)) is null)
            {
                SnackBarService.Error(Text(nameof(WorkbenchStrings.BoardSubmitFailed)));
                return;
            }

            if (ProgressTask is { } taskId && await Tell(taskId, by) is { } agent)
            {
                SnackBarService.Success(Text(nameof(WorkbenchStrings.BoardPicksSent), agent));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The picks of the board {BoardId} could not be submitted.", Board.Id);
            SnackBarService.Error(Text(nameof(WorkbenchStrings.BoardSubmitFailed)));
        }
        finally
        {
            isSubmitting = false;
        }
    }

    /// <summary>Adds the submit to the agent's task, which wakes its wait; returns the agent's name, or null when the task is over or gone.</summary>
    private async Task<string?> Tell(string taskId, string by)
    {
        await reviewSession.Open(Localizer[nameof(WorkbenchStrings.Reviewer)]);

        if (reviewSession.Tasks.FirstOrDefault(task => task.Id == taskId) is not { } task)
        {
            logger.LogWarning("The board {BoardId} names the Progress task {TaskId}, which isn't there.", Board!.Id, taskId);
            return null;
        }

        return reviewSession.SubmitBoard(task, Board!.Id, Title, by) ? task.Agent : null;
    }

    /// <summary>The board's store as it is on this screen, and its own version.</summary>
    private (JsonObject? State, int Version) StoreState(string boardId)
    {
        return blexManager.Stores.FirstOrDefault(store => store.Name == WorkbenchBoardFiles.StoreName(boardId)) is { } store
            ? (store.SerializeState(), store is BlexStoreBase { PersistVersion: var version } ? version : 0)
            : (null, 0);
    }

    /// <summary>A text of the Workbench's in the board's language, whatever language the Workbench is in.</summary>
    private string Text(string key, params object[] arguments)
    {
        var text = WorkbenchStrings.ResourceManager.GetString(key, Culture) ?? key;

        return arguments.Length == 0 ? text : string.Format(Culture, text, arguments);
    }

    /// <summary>The time of day on this computer, with the day when it isn't today.</summary>
    private string Time(DateTimeOffset at)
    {
        var local = at.ToLocalTime();

        return local.Date == DateTimeOffset.UtcNow.ToLocalTime().Date ? local.ToString("HH:mm", Culture) : local.ToString("d MMM, HH:mm", Culture);
    }

    public override async ValueTask DisposeAsync()
    {
        boardFiles.Changed -= OnFileChanged;

        await base.DisposeAsync();
    }
}
