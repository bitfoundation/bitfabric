namespace Bit.Fabric.Client.Films.Components;

/// <summary>
/// Base of the films' components: the app's strings, and a cancellation token that is cancelled when the component goes away,
/// for a film's timer. The films need nothing else of the app or the Workbench, which is what lets them load lazily.
/// </summary>
public partial class FilmComponentBase : ComponentBase, IDisposable
{
    [AutoInject] protected IStringLocalizer<AppStrings> Localizer = default!;

    private CancellationTokenSource? cancellationTokenSource = new();

    /// <summary>Cancelled when the component is disposed, and stays cancelled after it.</summary>
    protected CancellationToken CurrentCancellationToken => cancellationTokenSource?.Token ?? new CancellationToken(canceled: true);

    public virtual void Dispose()
    {
        // Let go of the source first, so nothing reads the token of a disposed source while it cancels.
        if (cancellationTokenSource is { } source)
        {
            cancellationTokenSource = null;
            source.Cancel();
            source.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
