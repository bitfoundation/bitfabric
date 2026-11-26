using System.Diagnostics;

namespace Bit.Cli.Infrastructure;

/// <summary>
/// A limit on how long bit waits. Its clock counts bit's own waiting only: <see cref="Pause"/> stops it while a person answers
/// a question. <see cref="Earlier"/> gives a limit that ends sooner on the same clock, for a part of the work that must leave
/// time to the rest.
/// </summary>
public sealed class Deadline
{
    private readonly Stopwatch clock;
    private readonly TimeSpan limit;

    private Deadline(Stopwatch clock, TimeSpan limit)
    {
        this.clock = clock;
        this.limit = limit > TimeSpan.Zero ? limit : TimeSpan.Zero;
    }

    public static Deadline After(TimeSpan limit) => new(Stopwatch.StartNew(), limit);

    public TimeSpan Left => limit - clock.Elapsed is var left && left > TimeSpan.Zero ? left : TimeSpan.Zero;

    public bool IsOver => Left == TimeSpan.Zero;

    /// <summary>The time left, but no more than <paramref name="most"/>.</summary>
    public TimeSpan AtMost(TimeSpan most) => Left < most ? Left : most;

    /// <summary>A limit on the same clock that ends <paramref name="by"/> before this one.</summary>
    public Deadline Earlier(TimeSpan by) => new(clock, limit - by);

    /// <summary>Stops the clock until the result is disposed.</summary>
    public IDisposable Pause()
    {
        clock.Stop();
        return new Resume(clock);
    }

    private sealed class Resume(Stopwatch clock) : IDisposable
    {
        public void Dispose() => clock.Start();
    }
}
