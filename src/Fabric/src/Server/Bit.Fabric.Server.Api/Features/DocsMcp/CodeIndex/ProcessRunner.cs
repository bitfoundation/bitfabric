namespace Bit.Fabric.Server.Api.Features.DocsMcp.CodeIndex;

internal readonly record struct ProcessResult(int ExitCode, string Output, string Error, bool TimedOut)
{
    public bool Succeeded => TimedOut is false && ExitCode is 0;

    /// <summary>What to log when it failed: standard error, or standard output when the command said nothing there.</summary>
    public string Diagnostics => string.IsNullOrWhiteSpace(Error) ? Output : Error;
}

internal static class ProcessRunner
{
    /// <summary>Runs a command to completion, or kills its process tree when <paramref name="timeout"/> passes.</summary>
    public static async Task<ProcessResult> Run(ProcessStartInfo startInfo, TimeSpan timeout, CancellationToken cancellationToken)
    {
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;

        Process? started;

        try
        {
            started = Process.Start(startInfo)!;
        }
        catch (Exception exp)
        {
            // A missing or unrunnable executable is reported like any other failure, so it doesn't end the loop that called this.
            return new(-1, "", $"{startInfo.FileName} could not be started: {exp.Message}", TimedOut: false);
        }

        using var process = started;

        // Both pipes are drained at once: a child that fills the one nobody reads blocks for good.
        var outputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var delaySource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var exitTask = process.WaitForExitAsync(CancellationToken.None);
        var timedOut = await Task.WhenAny(exitTask, Task.Delay(timeout, delaySource.Token)) != exitTask;

        await delaySource.CancelAsync();

        if (timedOut || cancellationToken.IsCancellationRequested)
        {
            Kill(process);
        }

        // Killing the process closes the pipes, so these complete rather than hanging on a dead child.
        await Task.WhenAll(outputTask, errorTask, exitTask);

        cancellationToken.ThrowIfCancellationRequested();

        return new(timedOut ? -1 : process.ExitCode, outputTask.Result, errorTask.Result, timedOut);
    }

    private static void Kill(Process process)
    {
        try
        {
            if (process.HasExited is false)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception) { /* Already gone, or gone between the check and the kill. */ }
    }
}
