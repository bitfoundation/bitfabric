namespace Bit.Fabric.Client.Films.Components;

// A feature's film is a few scenes, each a screen drawn from the parts below on one device, with a caption under it. They
// are data, so every feature gets a film the same way two-step sign-in got the first one
// (bitfabric-internal/specs/home-page.md, 2026-11-07), and AppHomeFilm and AppHomeFilmParts draw them all.

/// <summary>What a scene is drawn on.</summary>
public enum AppHomeDevice
{
    Phone,

    /// <summary>A browser window, with the scene's address in its bar; without one, a Mac's window.</summary>
    Browser,

    /// <summary>A Windows screen: the app in a window, with the taskbar under it.</summary>
    Desktop,

    /// <summary>A Windows screen in kiosk mode: the app alone fills it.</summary>
    Kiosk,

    /// <summary>A laptop: a browser on a screen with a keyboard's base under it.</summary>
    Laptop,

    Tablet,

    /// <summary>A code editor laid out like VS Code, with the AI agent's chat on its end side: the scene's parts are that chat.</summary>
    Editor
}

public enum AppHomeTone { Neutral, Good, Bad, Accent }

/// <summary>How a row, a choice or a chip stands out among the others.</summary>
public enum AppHomeMark
{
    None,

    /// <summary>It has just arrived: it slides in, lit.</summary>
    New,

    /// <summary>Picked or current.</summary>
    Picked,

    /// <summary>Taken already, or gone: crossed out.</summary>
    Used,

    /// <summary>Not there for this user: faded.</summary>
    Off
}

public enum AppHomePicture
{
    /// <summary>A drawn photo: a landscape with the sun over hills.</summary>
    Photo,
    Blocked,

    /// <summary>The photo, with the crop's frame closing in on it.</summary>
    Crop,

    /// <summary>The photo, turning a quarter.</summary>
    Rotate,
    Scan,

    /// <summary>The user's face in Face ID's frame, then the check.</summary>
    Face,

    /// <summary>A fingerprint on the sensor, the same ring going round while it reads, then the check.</summary>
    Fingerprint,
    Voice,
    Video
}

/// <summary>The made-up people of the films, each drawn with a face of their own (AppHomeFace).</summary>
public enum AppHomePerson { Sara, Emma, Lucas, Noah }

/// <summary>One scene of a film. Its length follows its caption and its last event unless it is given.</summary>
public sealed record AppHomeFilmScene(string Caption, AppHomeFilmPart[] Parts, AppHomeDevice Device = AppHomeDevice.Phone)
{
    /// <summary>The address in the browser's bar, or a bar drawn at the top of a phone's screen.</summary>
    public string? Url { get; init; }

    /// <summary>What this device is, written under it: "Android", "App installed".</summary>
    public string? Label { get; init; }

    /// <summary>Whose screen this is: their face and name over the device, so a change of person is seen at once.</summary>
    public AppHomePerson? Person { get; init; }

    public bool Rtl { get; init; }

    /// <summary>The app in another brand's look, for Rebrand.</summary>
    public bool Restyled { get; init; }

    public int Milliseconds { get; init; }
}

public abstract record AppHomeFilmPart
{
    /// <summary>Seconds into the scene before it appears; 0 is with the scene.</summary>
    public double At { get; init; }

    /// <summary>The last moment something happens in it, so the scene lasts long enough to see it.</summary>
    public virtual double Ends => At;
}

/// <summary>A part drawn over the screen rather than in its flow: a notification, a sheet, a dialog, a menu.</summary>
public abstract record AppHomeFilmOverlay : AppHomeFilmPart;

public sealed record FilmStep(string Text) : AppHomeFilmPart;

public sealed record FilmTitle(string Text) : AppHomeFilmPart;

public sealed record FilmText(string Text) : AppHomeFilmPart;

/// <summary>The app's top bar: its title, and the signed-in person's face on the end side, which a finger taps.</summary>
public sealed record FilmAppBar(string Title, AppHomePerson? Person = null, string? Name = null, double? Tap = null) : AppHomeFilmPart
{
    public override double Ends => Math.Max(At, Tap ?? 0);
}

/// <summary>A text box with its value, typed in letter by letter when <paramref name="Typing"/>.</summary>
public sealed record FilmField(string Value, string? Label = null, bool Typing = false) : AppHomeFilmPart
{
    public override double Ends => At + (Typing ? 1.8 : 0);
}

/// <summary>A button a drawn finger taps <paramref name="Tap"/> seconds into the scene.</summary>
public sealed record FilmButton(string Text, double? Tap = null, AppHomeTone Tone = AppHomeTone.Accent, bool Outline = false) : AppHomeFilmPart
{
    public override double Ends => Math.Max(At, Tap ?? 0);
}

/// <summary>A one-time code's boxes, empty or filling in digit by digit from its At; the boxes are there with the scene.</summary>
public sealed record FilmCode(string Digits, bool Fill = true) : AppHomeFilmPart
{
    public override double Ends => At + (Fill ? .2 + Digits.Length * .12 : 0);
}

public sealed record FilmNote(string Text, AppHomeTone Tone = AppHomeTone.Good) : AppHomeFilmPart;

public sealed record FilmChoice(string Text, string? Sub = null, AppHomeMark Mark = AppHomeMark.None, double? Tap = null) : AppHomeFilmPart
{
    public override double Ends => Math.Max(At, Tap ?? 0);
}

/// <summary>A list's row: an icon or a person's face, its text, a line under it and a value at its end, which a finger may tap.</summary>
public sealed record FilmRow(string Icon, string Text, string? Sub = null, string? End = null, AppHomeMark Mark = AppHomeMark.None, AppHomeTone EndTone = AppHomeTone.Neutral) : AppHomeFilmPart
{
    public AppHomePerson? Person { get; init; }

    public double? Tap { get; init; }

    public override double Ends => Math.Max(At, Tap ?? 0);
}

/// <summary>A chat message: the user's own on the end side, the app's on the start side.</summary>
public sealed record FilmBubble(string Text, bool Mine = false) : AppHomeFilmPart;

/// <summary>A big check, or a cross when <paramref name="Tone"/> is Bad, with what it means under it.</summary>
public sealed record FilmDone(string Text, AppHomeTone Tone = AppHomeTone.Good, string? Sub = null) : AppHomeFilmPart;

/// <summary>A small bar chart, its values out of 100.</summary>
public sealed record FilmBars(int[] Values) : AppHomeFilmPart;

public sealed record FilmStat(string Number, string Label) : AppHomeFilmPart;

/// <summary>A scanner's score out of 100 in a ring, green from 90, amber from 50 and red under it, as Lighthouse colors them.</summary>
public sealed record FilmScore(int Value, string Label) : AppHomeFilmPart;

public sealed record FilmChips(string[] Items, int Picked = -1) : AppHomeFilmPart;

public sealed record FilmPicture(AppHomePicture Kind, string? Label = null) : AppHomeFilmPart
{
    /// <summary>Whose face Face ID sees.</summary>
    public AppHomePerson Person { get; init; } = AppHomePerson.Sara;

    public override double Ends => At + 1.5;
}

/// <summary>A data grid: its columns, with the sorted one marked, its rows and a pager.</summary>
public sealed record FilmGrid(string[] Columns, string[][] Rows, string Pager, int Sorted = -1) : AppHomeFilmPart;

/// <summary>An event on a calendar's day, 1 to 35 counted from the top start of the month's grid.</summary>
public sealed record FilmEvent(int Day, string Text, AppHomeTone Tone = AppHomeTone.Accent);

/// <summary>A month on a calendar, with its events; the one at <paramref name="Moving"/> is dragged to <paramref name="MovesTo"/>.</summary>
public sealed record FilmCalendar(string Month, FilmEvent[] Events, int Moving = -1, int MovesTo = 0, double MoveAt = 1) : AppHomeFilmPart
{
    public override double Ends => Moving < 0 ? At : MoveAt + 1.2;
}

/// <summary>An image editor's toolbar: the named tools, the picked one lit, then buttons for the tools it doesn't name.</summary>
public sealed record FilmTools(string[] Icons, int Picked = -1, int More = 3) : AppHomeFilmPart;

/// <summary>The well-known sign-in providers' marks, with a line under them.</summary>
public sealed record FilmLogos(string Text) : AppHomeFilmPart;

/// <summary>A device drawn small, with a page in it as wireframe lines and its name under it, as the Workbench shows its views.</summary>
public sealed record FilmFrame(AppHomeDevice Device, string Label)
{
    /// <summary>The page in the dark theme, or in the light one when false, whichever theme the film is in; without it, in the film's.</summary>
    public bool? Dark { get; init; }

    /// <summary>The page right to left.</summary>
    public bool Rtl { get; init; }

    /// <summary>A store screenshot's caption: the device then sits under it, on a colored background.</summary>
    public string? Caption { get; init; }

    /// <summary>A store screenshot's background: the drawn app's accent, or the success color when Good.</summary>
    public AppHomeTone Tone { get; init; } = AppHomeTone.Accent;
}

/// <summary>
/// Devices drawn small, side by side, each coming in after the one before. With <paramref name="ChangeAt"/>, the same change
/// shows on every one of them at that moment, as a change does in the Workbench's views.
/// </summary>
public sealed record FilmFrames(FilmFrame[] Frames, double? ChangeAt = null) : AppHomeFilmPart
{
    public override double Ends => Math.Max(At + .2 + Frames.Length * .25, (ChangeAt ?? 0) + .6);
}

/// <summary>Pages drawn small in a row, each with the button that leads on lit, and an arrow from it to the next page.</summary>
public sealed record FilmFlow(string[] Pages) : AppHomeFilmPart
{
    public override double Ends => At + .2 + Pages.Length * .5;
}

/// <summary>A notification that drops from the top, from an app such as Messages or Mail.</summary>
public sealed record FilmBanner(string From, string Text) : AppHomeFilmOverlay
{
    public override double Ends => At + .5;
}

/// <summary>A sheet that rises from the bottom with a question and the button the finger taps.</summary>
public sealed record FilmSheet(string Text, string Button, double Tap) : AppHomeFilmOverlay
{
    public override double Ends => Math.Max(At, Tap);
}

/// <summary>A dialog over the dimmed screen, drawn from parts of its own.</summary>
public sealed record FilmDialog(string Title, AppHomeFilmPart[] Parts) : AppHomeFilmOverlay
{
    public override double Ends => Parts.Select(p => p.Ends).Append(At).Max();
}

/// <summary>The account menu, open under the top bar's face on the end side, as the app's own does it.</summary>
public sealed record FilmMenu(AppHomeFilmPart[] Parts) : AppHomeFilmOverlay
{
    public override double Ends => Parts.Select(p => p.Ends).Append(At).Max();
}
