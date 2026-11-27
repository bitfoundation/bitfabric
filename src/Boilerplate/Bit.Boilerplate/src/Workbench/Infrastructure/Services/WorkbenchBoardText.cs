using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Web;

namespace Boilerplate.Workbench.Infrastructure.Services;

/// <summary>
/// What each board says, for the search of the Boards side panel: the board rendered to HTML on the server once per
/// language, its text kept until hot reload changes a board. Nothing is asked of whoever writes a board, so finding one by
/// what it says costs nothing per board.
/// </summary>
public static partial class WorkbenchBoardText
{
    private static readonly ConcurrentDictionary<(string Culture, string BoardId), string> texts = new();

    // One search renders at a time; the next waits and then reads what the first kept.
    private static readonly SemaphoreSlim reading = new(1, 1);

    /// <summary>Called when hot reload builds the catalog again, as a board's text may have changed with it.</summary>
    public static void Clear() => texts.Clear();

    /// <summary>
    /// The ids of the boards whose text holds <paramref name="term"/>, each board rendered in a service scope of its own, so
    /// its stores and services are new ones and the circuit asking keeps its own as they are.
    /// </summary>
    public static async Task<IReadOnlySet<string>> FindAsync(IServiceProvider services, string term, CancellationToken cancellationToken)
    {
        HashSet<string> matches = [];

        if (string.IsNullOrWhiteSpace(term))
            return matches;

        var culture = CultureInfo.CurrentUICulture.Name;

        await reading.WaitAsync(cancellationToken);

        try
        {
            await using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            await using var renderer = new HtmlRenderer(scope.ServiceProvider, services.GetRequiredService<ILoggerFactory>());

            foreach (var board in WorkbenchBoardCatalog.Boards)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (texts.TryGetValue((culture, board.Id), out var text) is false)
                {
                    text = await Read(renderer, board, services.GetRequiredService<ILogger<WorkbenchBoard>>());
                    texts[(culture, board.Id)] = text;
                }

                if (text.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                {
                    matches.Add(board.Id);
                }
            }
        }
        finally
        {
            reading.Release();
        }

        return matches;
    }

    private static async Task<string> Read(HtmlRenderer renderer, WorkbenchBoard board, ILogger logger)
    {
        try
        {
            var html = await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var output = await renderer.RenderComponentAsync(board.ComponentType);
                return output.ToHtmlString();
            });

            var text = WebUtility.HtmlDecode(TagPattern().Replace(html, " "));

            return Components.Shell.SectionSidebar.Normalize(SpacePattern().Replace(text, " "));
        }
        catch (Exception exception)
        {
            // A board that can't render on its own is still found by its title and summary.
            logger.LogWarning(exception, "The board {BoardId} could not be read for the search.", board.Id);
            return string.Empty;
        }
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpacePattern();
}
