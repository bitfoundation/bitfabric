using System.Globalization;

namespace Bit.Fabric.Client.Films.Components;

/// <summary>Draws a scene's parts, the ones in its flow or the ones over it; a dialog draws its own parts with it.</summary>
public partial class AppHomeFilmParts
{
    [Parameter, EditorRequired] public AppHomeFilmPart[] Parts { get; set; } = default!;

    [CascadingParameter(Name = nameof(AppHomeFilm.Logo))] public RenderFragment<string>? Logo { get; set; }

    /// <summary>A part that comes later than its scene slides in at its moment; a code's boxes are there with the scene, and only its digits wait.</summary>
    private static string? Late(AppHomeFilmPart part) => part.At > 0 && part is not (AppHomeFilmOverlay or FilmCode) ? "is-late" : null;

    /// <summary>When a part appears and when its finger taps, as invariant CSS times: a Persian culture would write 1٫2s.</summary>
    private static string? Times(AppHomeFilmPart part, double? tap = null)
    {
        var style = part.At > 0 ? $"--in:{Seconds(part.At)};" : string.Empty;

        if (tap is not null)
            style += $"--tap-at:{Seconds(tap.Value)};";

        return style.Length > 0 ? style : null;
    }

    /// <summary>When the frames come in, and when the change shows on all of them.</summary>
    private static string? FramesStyle(FilmFrames frames)
    {
        var style = Times(frames) + (frames.ChangeAt is { } changeAt ? $"--change-at:{Seconds(changeAt)};" : null);

        return style.Length > 0 ? style : null;
    }

    private static string Seconds(double value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "s";

    private static string ToneOf(AppHomeTone tone) => tone switch
    {
        AppHomeTone.Good => "is-good",
        AppHomeTone.Bad => "is-bad",
        AppHomeTone.Accent => "is-accent",
        _ => "is-neutral"
    };

    private static string RingOf(int score) => score switch
    {
        >= 90 => "is-good",
        >= 50 => "is-fair",
        _ => "is-poor"
    };

    private static string? MarkOf(AppHomeMark mark) => mark switch
    {
        AppHomeMark.New => "is-new",
        AppHomeMark.Picked => "is-picked",
        AppHomeMark.Used => "is-used",
        AppHomeMark.Off => "is-off",
        _ => null
    };

    private static string IconOf(AppHomePicture kind) => kind switch
    {
        AppHomePicture.Blocked => BitIconName.Blocked2,
        AppHomePicture.Scan => BitIconName.QRCode,
        AppHomePicture.Voice => BitIconName.Microphone,
        AppHomePicture.Video => BitIconName.Play,
        _ => BitIconName.Photo2
    };

    /// <summary>The grid's columns, the first one wider, for its name.</summary>
    private static string ColumnsOf(FilmGrid grid) => $"grid-template-columns:1.5fr{string.Concat(Enumerable.Repeat(" 1fr", grid.Columns.Length - 1))};";

    /// <summary>The calendar's month starts on its third cell and has 30 days; the cells before and after it are empty.</summary>
    private const int FirstDay = 3;

    private static string? DayOf(int cell) => cell - FirstDay + 1 is var day and >= 1 and <= 30 ? day.ToString(CultureInfo.CurrentCulture) : null;

    /// <summary>The week's days, Monday first, as short as the language writes them.</summary>
    private static string[] WeekDays
    {
        get
        {
            var names = CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames;
            return [.. Enumerable.Range(1, 7).Select(i => names[i % 7])];
        }
    }

    /// <summary>Where the dragged event starts and ends: its cell's place in the grid of seven columns and five rows.</summary>
    private static string MoveOf(FilmCalendar calendar)
    {
        static string Place(int cell, string name) =>
            $"--{name}-x:{Percent((cell - 1) % 7 * 100 / 7.0)};--{name}-y:{Percent((cell - 1) / 7 * 100 / 5.0)};";

        return Place(calendar.Events[calendar.Moving].Day, "from") + Place(calendar.MovesTo, "to") + $"--move-at:{Seconds(calendar.MoveAt)};";
    }

    private static string Percent(double value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
