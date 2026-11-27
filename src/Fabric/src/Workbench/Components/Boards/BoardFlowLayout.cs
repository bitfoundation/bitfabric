namespace Bit.Fabric.Workbench.Components.Boards;

/// <summary>Places the screens of a board's flow (BoardFlow).</summary>
public static class BoardFlowLayout
{
    private const int ColumnGap = 420;

    private const int RowGap = 200;

    private const int CaptionHeight = 160;

    /// <summary>The space a screen takes: its framed device and the caption under it.</summary>
    public static (int Width, int Height) SizeOf(WorkbenchScenario scenario)
    {
        var (width, height) = DeviceFrame.GetFrameSize(scenario.DevicePreset, scenario.Rotated, showFrame: true);

        return (width, height + CaptionHeight);
    }

    /// <summary>
    /// Lays the screens out in columns by how many arrows lead to them from a screen no arrow points at, top to bottom in a
    /// column, the first column on the left, or on the right for a flow that reads right to left.
    /// </summary>
    public static Dictionary<string, (double X, double Y)> Arrange(IReadOnlyList<(string Id, WorkbenchScenario Scenario)> screens, IReadOnlyList<(string From, string To)> links, bool rightToLeft = false)
    {
        if (screens.Count == 0)
            return [];

        if (screens.Count == 0)
            return [];

        var depth = new Dictionary<string, int>();
        var targets = links.Select(link => link.To).ToHashSet();

        // Breadth-first from each screen no arrow points at; a screen still unreached, as on a loop, starts a walk of its own.
        foreach (var (start, _) in screens.Where(screen => targets.Contains(screen.Id) is false).Concat(screens))
        {
            if (depth.ContainsKey(start))
                continue;

            depth[start] = 0;
            var queue = new Queue<string>([start]);

            while (queue.TryDequeue(out var current))
            {
                foreach (var (_, to) in links.Where(link => link.From == current))
                {
                    if (depth.ContainsKey(to) || screens.Any(screen => screen.Id == to) is false)
                        continue;

                    depth[to] = depth[current] + 1;
                    queue.Enqueue(to);
                }
            }
        }

        var columns = screens.GroupBy(screen => depth[screen.Id]).OrderBy(column => column.Key).ToList();
        var tallest = columns.Max(HeightOf);
        var positions = new Dictionary<string, (double X, double Y)>();
        double x = 0;

        foreach (var column in columns)
        {
            // A shorter column is centered on the tallest one, so a single screen sits level with the two a fork leads to.
            var y = (tallest - HeightOf(column)) / 2;

            foreach (var (id, scenario) in column)
            {
                var (width, height) = SizeOf(scenario);

                positions[id] = (rightToLeft ? -x - width : x, y);
                y += height + RowGap;
            }

            x += column.Max(screen => SizeOf(screen.Scenario).Width) + ColumnGap;
        }

        return positions;
    }

    private static double HeightOf(IEnumerable<(string Id, WorkbenchScenario Scenario)> column) =>
        column.Sum(screen => SizeOf(screen.Scenario).Height + RowGap) - RowGap;
}
