using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace Bit.Fabric.Workbench.Infrastructure.Services;

/// <summary>
/// What the reader picked on each board, in a file of its own in <see cref="WorkbenchFolders.Boards"/>, where the agent that
/// built the board reads it at any time: a board's store, named board-{id}, is written there each time it saves in the
/// Workbench's own window (WorkbenchStateStorage), and Submit keeps the picks as they were sent (BoardSubmit). The browser
/// still keeps the store for itself; this copy is what the agent reads. See the boards skill (.github/agents/boards.agent.md).
/// </summary>
public sealed partial class WorkbenchBoardFiles(ILogger<WorkbenchBoardFiles> logger)
{
    /// <summary>What a board's store is named after its board's id: board-{id}.</summary>
    public const string StorePrefix = "board-";

    // The envelope Blex saves a store with a version of its own in.
    private const string StoreVersionKey = "__blexStoreVersion";

    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // The file is read by people and agents, so a board in Persian keeps its words as they are.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised with a board's id after its file changed.</summary>
    public event Action<string>? Changed;

    /// <summary>The file of a board.</summary>
    public static string FilePath(string boardId) => Path.Combine(WorkbenchFolders.Boards, $"{boardId}.json");

    /// <summary>Whether a board's id is safe as a file name, as every id of the catalog's should be.</summary>
    public static bool IsFileName([NotNullWhen(true)] string? boardId) => boardId is not null && IdPattern().IsMatch(boardId);

    /// <summary>The name of a board's store.</summary>
    public static string StoreName(string boardId) => $"{StorePrefix}{boardId}";

    /// <summary>
    /// The id of the board whose store a storage key belongs to, like blex:board-payments; null for any other store and for a
    /// board the catalog doesn't have, so nothing else gets a file.
    /// </summary>
    public static string? BoardOf(string storageKey)
    {
        return StoreKeyPattern().Match(storageKey) is { Success: true } match && WorkbenchBoardCatalog.Find(match.Groups["id"].Value) is { } board && IsFileName(board.Id)
            ? board.Id
            : null;
    }

    /// <summary>The board's file as it is; null when nothing was saved for it yet, or when it can't be read.</summary>
    public async Task<WorkbenchBoardPicks?> Read(string boardId, CancellationToken cancellationToken = default)
    {
        var path = FilePath(boardId);

        if (File.Exists(path) is false)
            return null;

        try
        {
            // Shared, so an agent reading the file at the same time never stops a save, nor a save the agent.
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            return await JsonSerializer.DeserializeAsync<WorkbenchBoardPicks>(file, jsonOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(exception, "The picks of the board {BoardId} can't be read.", boardId);
            return null;
        }
    }

    /// <summary>
    /// Writes a board's store as it was just saved, taken out of the envelope a store with a version of its own is saved in.
    /// A save that changes nothing leaves the file as it is.
    /// </summary>
    public async Task Save(string boardId, string payload, CancellationToken cancellationToken = default)
    {
        if (ParsePayload(payload) is not var (state, version))
            return;

        await Edit(boardId, picks => picks is not null && picks.StoreVersion == version && JsonNode.DeepEquals(picks.State, state)
            ? null
            : new(boardId, DateTimeOffset.UtcNow, state, picks?.Submitted, version), cancellationToken);
    }

    /// <summary>
    /// Keeps the board's picks as the reader submits them: <paramref name="state"/> is the board's store as it is on their
    /// screen, which may differ from the last save when another browser changed the board meanwhile.
    /// </summary>
    public async Task<WorkbenchBoardPicks?> Submit(string boardId, JsonObject? state, int storeVersion, string by, CancellationToken cancellationToken = default)
    {
        return await Edit(boardId, _ =>
        {
            var now = DateTimeOffset.UtcNow;

            return new(boardId, now, state, new(now, by, state?.DeepClone().AsObject()), storeVersion);
        }, cancellationToken);
    }

    /// <summary>Removes the board's file, as when its store's saved state is dropped.</summary>
    public async Task Remove(string boardId, CancellationToken cancellationToken = default)
    {
        var gate = locks.GetOrAdd(boardId, _ => new(1, 1));

        await gate.WaitAsync(cancellationToken);

        try
        {
            if (File.Exists(FilePath(boardId)) is false)
                return;

            File.Delete(FilePath(boardId));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The picks of the board {BoardId} can't be removed.", boardId);
            return;
        }
        finally
        {
            gate.Release();
        }

        Changed?.Invoke(boardId);
    }

    /// <summary>Changes the board's file one save at a time; an edit that returns null leaves it as it is.</summary>
    private async Task<WorkbenchBoardPicks?> Edit(string boardId, Func<WorkbenchBoardPicks?, WorkbenchBoardPicks?> edit, CancellationToken cancellationToken)
    {
        var gate = locks.GetOrAdd(boardId, _ => new(1, 1));
        WorkbenchBoardPicks? picks;

        await gate.WaitAsync(cancellationToken);

        try
        {
            if ((picks = edit(await Read(boardId, cancellationToken))) is null)
                return null;

            await Write(FilePath(boardId), JsonSerializer.Serialize(picks, jsonOptions), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The picks of the board {BoardId} can't be written.", boardId);
            return null;
        }
        finally
        {
            gate.Release();
        }

        Changed?.Invoke(boardId);

        return picks;
    }

    /// <summary>Writes next to the file, then puts it in its place, so a reader never finds half of it.</summary>
    private static async Task Write(string path, string json, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";

        await File.WriteAllTextAsync(temporary, json, cancellationToken);

        try
        {
            // A reader that holds the file without sharing its deletion, like an editor, can keep it for a moment.
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(temporary, path, overwrite: true);
                    return;
                }
                catch (IOException) when (attempt < 5)
                {
                    await Task.Delay(50 * attempt, cancellationToken);
                }
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <summary>The store's state and its own version from what Blex saves; null for anything that isn't a store's state.</summary>
    private static (JsonObject State, int Version)? ParsePayload(string payload)
    {
        try
        {
            return JsonNode.Parse(payload) switch
            {
                JsonObject { Count: 2 } envelope when envelope[StoreVersionKey] is JsonValue version && version.TryGetValue<int>(out var number) && envelope["state"] is JsonObject state
                    => (state.DeepClone().AsObject(), number),
                JsonObject state => (state, 0),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^(?:[^:]*:)?board-(?<id>[^:/\\]+)$")]
    private static partial Regex StoreKeyPattern();

    // A board's id that is safe as a file name.
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,99}$")]
    private static partial Regex IdPattern();
}
