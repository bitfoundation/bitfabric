using Bit.Cli.Infrastructure;

namespace Bit.Cli.Projects;

/// <param name="Unfinished">The time ran out before every file and folder was tried.</param>
public sealed record FolderDeletion(IReadOnlyList<PathFailure> Failures, bool OnlyEmptyFoldersLeft, bool Unfinished)
{
    public bool IsGone => Failures.Count == 0 && OnlyEmptyFoldersLeft is false && Unfinished is false;
}

public static class FolderRemover
{
    public static (IReadOnlyList<PathFailure> Failures, bool OnlyEmptyFoldersLeft) Delete(string folder)
    {
        var deletion = Delete(folder, null);
        return (deletion.Failures, deletion.OnlyEmptyFoldersLeft);
    }

    /// <summary>Deletes the folder, and stops where it got to once the deadline is over.</summary>
    public static FolderDeletion Delete(string folder, Deadline? deadline)
    {
        var failures = new List<PathFailure>();
        var root = new DirectoryInfo(folder);
        var unfinished = false;

        if (root.Exists && root.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            TryDelete(root, failures);
        }
        else
        {
            DeleteTree(root, failures, deadline, ref unfinished);
        }

        if (Directory.Exists(folder) is false)
            return new([], false, false);

        if (unfinished)
            return new(failures, false, true);

        return HasFiles(folder) ? new(failures, false, false) : new([], true, false);
    }

    private static void DeleteTree(DirectoryInfo folder, List<PathFailure> failures, Deadline? deadline, ref bool unfinished)
    {
        var before = failures.Count;
        List<FileSystemInfo> entries;

        try
        {
            ClearReadOnly(folder);
            entries = [.. folder.EnumerateFileSystemInfos()];
        }
        catch (Exception exp) when (exp is IOException or UnauthorizedAccessException)
        {
            failures.Add(new PathFailure(folder.FullName, exp.Message));
            return;
        }

        foreach (var entry in entries)
        {
            if (unfinished || deadline?.IsOver is true)
            {
                unfinished = true;
                return;
            }

            if (entry is DirectoryInfo directory && directory.Attributes.HasFlag(FileAttributes.ReparsePoint) is false)
            {
                DeleteTree(directory, failures, deadline, ref unfinished);
            }
            else
            {
                TryDelete(entry, failures);
            }
        }

        if (unfinished)
            return;

        if (failures.Count == before)
        {
            TryDelete(folder, failures);
        }
    }

    private static void TryDelete(FileSystemInfo entry, List<PathFailure> failures)
    {
        try
        {
            ClearReadOnly(entry);
            Delete(entry);
        }
        catch (Exception exp) when (exp is IOException or UnauthorizedAccessException)
        {
            failures.Add(new PathFailure(entry.FullName, exp.Message));
        }
    }

    private static void Delete(FileSystemInfo entry)
    {
        var path = OperatingSystem.IsWindows() is false || entry.FullName.StartsWith(@"\\?\", StringComparison.Ordinal) || entry.FullName.StartsWith(@"\\.\", StringComparison.Ordinal) ? entry.FullName
            : entry.FullName.StartsWith(@"\\", StringComparison.Ordinal) ? $@"\\?\UNC\{entry.FullName[2..]}"
            : $@"\\?\{entry.FullName}";

        if (entry is DirectoryInfo)
        {
            Directory.Delete(path);
        }
        else
        {
            File.Delete(path);
        }
    }

    private static void ClearReadOnly(FileSystemInfo entry)
    {
        var attributes = entry.Attributes;

        if (attributes.HasFlag(FileAttributes.ReadOnly) && attributes.HasFlag(FileAttributes.ReparsePoint) is false)
        {
            entry.Attributes = attributes & ~FileAttributes.ReadOnly;
        }
    }

    private static bool HasFiles(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Any();
        }
        catch (Exception exp) when (exp is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
