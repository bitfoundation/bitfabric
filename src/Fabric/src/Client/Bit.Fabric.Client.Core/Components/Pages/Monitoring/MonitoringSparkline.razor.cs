namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>A small line of response times, oldest first; a zero is a gap where no round read them.</summary>
public partial class MonitoringSparkline
{
    [Parameter, EditorRequired] public int[] Values { get; set; } = [];

    [Parameter] public string? AriaLabel { get; set; }

    private IEnumerable<string> Segments()
    {
        if (Values.Length < 2)
            yield break;

        var known = Values.Where(value => value > 0).ToArray();
        if (known.Length is 0)
            yield break;

        // From zero, so a flat line reads as steady rather than as a zoomed-in wobble.
        var max = Math.Max(known.Max() * 1.15, 1);
        var step = 100.0 / (Values.Length - 1);
        var points = new List<string>();

        for (var i = 0; i < Values.Length; i++)
        {
            if (Values[i] <= 0)
            {
                if (points.Count > 1) yield return string.Join(' ', points);
                points.Clear();
                continue;
            }

            var x = (i * step).ToString("0.##", CultureInfo.InvariantCulture);
            var y = (31 - 29 * Values[i] / max).ToString("0.##", CultureInfo.InvariantCulture);
            points.Add($"{x},{y}");
        }

        if (points.Count > 1) yield return string.Join(' ', points);
    }
}
