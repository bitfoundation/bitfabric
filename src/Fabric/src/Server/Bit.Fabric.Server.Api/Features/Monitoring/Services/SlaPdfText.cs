using System.Globalization;
using System.Text;
using HarfBuzzSharp;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// Lays out the SLA report's text the way the app's pages show it: Latin in Inter, Persian and Arabic
/// in Vazirmatn (its Farsi-digits cut for Persian, so 1 reads ۱), Hindi in Noto Sans Devanagari, Chinese in Noto Sans SC,
/// so every language the app speaks draws without the server's fonts, which its image has none of; anything else in a
/// font of the server's if it has one.
/// A line is split into runs of one direction by a simplified Unicode bidi algorithm, enough for labels that mix Persian
/// or Arabic with Latin names and numbers; each run into pieces of one font, each piece shaped by HarfBuzz. One instance
/// per report: it keeps HarfBuzz's fonts, which aren't shared between threads.
/// </summary>
public sealed class SlaPdfText : IDisposable
{
    /// <summary>Marks a line of Chinese never starts with, and those it never ends with (See BreaksBetween).</summary>
    private const string ClosingMarks = "，。、；：！？）」』】》〉”’)]}.,;:!?%";
    private const string OpeningMarks = "（「『【《〈“‘([{";

    private static readonly Lazy<SKTypeface> interRegular = new(() => Load("Inter-Regular.ttf"));
    private static readonly Lazy<SKTypeface> interSemiBold = new(() => Load("Inter-SemiBold.ttf"));
    private static readonly Lazy<SKTypeface> vazirmatnRegular = new(() => Load("Vazirmatn-Regular.ttf"));
    private static readonly Lazy<SKTypeface> vazirmatnSemiBold = new(() => Load("Vazirmatn-SemiBold.ttf"));
    private static readonly Lazy<SKTypeface> vazirmatnFdRegular = new(() => Load("Vazirmatn-FD-Regular.ttf"));
    private static readonly Lazy<SKTypeface> vazirmatnFdSemiBold = new(() => Load("Vazirmatn-FD-SemiBold.ttf"));
    private static readonly Lazy<SKTypeface> devanagariRegular = new(() => Load("NotoSansDevanagari-Regular.ttf"));
    private static readonly Lazy<SKTypeface> devanagariSemiBold = new(() => Load("NotoSansDevanagari-SemiBold.ttf"));
    /// <summary>One weight, 8 MB; bold is drawn by emboldening its outlines (See Draw).</summary>
    private static readonly Lazy<SKTypeface> chinese = new(() => Load("NotoSansSC-Regular.otf"));

    private readonly CultureInfo culture;
    private readonly bool persianDigits;
    private readonly Dictionary<SKTypeface, SKShaper> shapers = [];
    private readonly Dictionary<(int CodePoint, bool Bold), SKTypeface?> fallbacks = [];
    private readonly Dictionary<SKTypeface, SKFont> glyphChecks = [];

    public SlaPdfText(CultureInfo culture)
    {
        this.culture = culture;
        IsRightToLeft = culture.TextInfo.IsRightToLeft;
        // Persian draws digits in its own shapes, as the app does with its Farsi-digits face; Arabic keeps Latin ones.
        persianDigits = culture.TwoLetterISOLanguageName is "fa";
    }

    /// <summary>The fonts the PDF embeds as text; any other, Chinese included, is drawn as outlines (See Draw).</summary>
    private static bool IsEmbedded(SKTypeface typeface)
        => typeface == interRegular.Value || typeface == interSemiBold.Value || typeface == vazirmatnRegular.Value
           || typeface == vazirmatnSemiBold.Value || typeface == vazirmatnFdRegular.Value || typeface == vazirmatnFdSemiBold.Value
           || typeface == devanagariRegular.Value || typeface == devanagariSemiBold.Value;

    /// <summary>The fonts the report brings, rather than the server's.</summary>
    private static bool IsOwn(SKTypeface typeface) => IsEmbedded(typeface) || typeface == chinese.Value;

    /// <summary>
    /// Whether every character of <paramref name="text"/> has a glyph in a font the report brings, so it draws the same on
    /// a server without fonts of its own instead of as empty boxes.
    /// </summary>
    public bool DrawsWithItsOwnFonts(string text, bool bold = false)
        => Pieces(text, bold).All(piece => IsOwn(piece.Typeface) && Shape(piece, 12).Glyphs.All(glyph => glyph is not 0));

    /// <summary>The paragraph direction of the report's language.</summary>
    public bool IsRightToLeft { get; }

    public float Measure(string text, float size, bool bold = false)
        => Pieces(text, bold).Sum(piece => Shape(piece, size).Width);

    /// <summary>Draws a line with its left edge at <paramref name="x"/>.</summary>
    public void Draw(SKCanvas canvas, string text, float x, float baseline, float size, SKColor color, bool bold = false)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        foreach (var piece in Pieces(text, bold))
        {
            var shaped = Shape(piece, size);

            if (shaped.Glyphs.Length > 0 && IsEmbedded(piece.Typeface) is false)
            {
                // A Chinese font is MBs, and the PDF would embed all of it: its glyphs go in as outlines instead, which
                // costs only the characters drawn. The report's Chinese face has one weight, so bold thickens it.
                using var font = new SKFont(piece.Typeface, size) { Embolden = bold && piece.Typeface == chinese.Value };
                for (var i = 0; i < shaped.Glyphs.Length; i++)
                {
                    using var outline = font.GetGlyphPath(shaped.Glyphs[i]);
                    if (outline is null)
                        continue;

                    outline.Offset(x + shaped.Points[i].X, baseline + shaped.Points[i].Y);
                    canvas.DrawPath(outline, paint);
                }
            }
            else if (shaped.Glyphs.Length > 0)
            {
                using var font = new SKFont(piece.Typeface, size);
                using var builder = new SKTextBlobBuilder();
                var run = builder.AllocatePositionedRun(font, shaped.Glyphs.Length);
                shaped.Glyphs.CopyTo(run.Glyphs);
                shaped.Points.CopyTo(run.Positions);
                using var blob = builder.Build();
                canvas.DrawText(blob, x, baseline, paint);
            }

            x += shaped.Width;
        }
    }

    /// <summary>
    /// Draws a line in the box between <paramref name="left"/> and <paramref name="right"/>, aligned where the language
    /// starts a line (left, or right in Persian and Arabic), where it ends, or in the middle.
    /// </summary>
    public void DrawAligned(SKCanvas canvas, string text, float left, float right, float baseline, float size, SKColor color, bool bold = false, SlaPdfAlign align = SlaPdfAlign.Start)
    {
        var width = Measure(text, size, bold);
        var atLeft = (align, IsRightToLeft) switch
        {
            (SlaPdfAlign.Center, _) => null,
            (SlaPdfAlign.Start, false) or (SlaPdfAlign.End, true) => true,
            _ => (bool?)false
        };

        var x = atLeft switch
        {
            true => left,
            false => right - width,
            null => left + (right - left - width) / 2
        };

        Draw(canvas, text, x, baseline, size, color, bold);
    }

    /// <summary>
    /// The lines <paramref name="text"/> takes at most <paramref name="width"/> wide, breaking at spaces, and between the
    /// characters of Chinese, which has none (See BreaksBetween).
    /// </summary>
    public List<string> Wrap(string text, float size, float width, bool bold = false)
    {
        List<string> lines = [];
        var line = new StringBuilder();

        foreach (var (word, afterSpace) in Words(text))
        {
            var candidate = line.Length is 0 ? word : afterSpace ? $"{line} {word}" : $"{line}{word}";

            if (line.Length > 0 && Measure(candidate, size, bold) > width)
            {
                lines.Add(line.ToString());
                line.Clear().Append(word);
            }
            else
            {
                line.Clear().Append(candidate);
            }
        }

        if (line.Length > 0)
        {
            lines.Add(line.ToString());
        }

        return lines;
    }

    /// <summary>What a line may break between: its words, each split further where Chinese allows a break.</summary>
    private static IEnumerable<(string Word, bool AfterSpace)> Words(string text)
    {
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var start = 0;

            for (var i = 1; i < word.Length; i++)
            {
                if (BreaksBetween(word[i - 1], word[i]))
                {
                    yield return (word[start..i], start is 0);
                    start = i;
                }
            }

            yield return (word[start..], start is 0);
        }
    }

    /// <summary>
    /// Whether a line may break between two characters without a space: next to a Chinese one, but never before a closing
    /// mark such as ， or ）, nor after an opening one such as （.
    /// </summary>
    private static bool BreaksBetween(char before, char after)
        => (IsWide(before) || IsWide(after)) && ClosingMarks.Contains(after) is false && OpeningMarks.Contains(before) is false;

    /// <summary>CJK's ideographs, kana and punctuation, and their full-width forms.</summary>
    private static bool IsWide(char value) => value is >= '\u2E80' and <= '\u9FFF' or >= '\uF900' and <= '\uFAFF' or >= '\uFF00' and <= '\uFFEF';

    public void Dispose()
    {
        foreach (var shaper in shapers.Values)
        {
            shaper.Dispose();
        }

        foreach (var font in glyphChecks.Values)
        {
            font.Dispose();
        }

        shapers.Clear();
        glyphChecks.Clear();
    }

    /// <summary>A stretch of one direction and one font, in the order it is drawn from left to right.</summary>
    private readonly record struct Piece(string Text, SKTypeface Typeface, bool RightToLeft);

    private readonly record struct Shaped(ushort[] Glyphs, SKPoint[] Points, float Width);

    private Shaped Shape(Piece piece, float size)
    {
        if (shapers.TryGetValue(piece.Typeface, out var shaper) is false)
        {
            shapers[piece.Typeface] = shaper = new SKShaper(piece.Typeface);
        }

        using var buffer = new HarfBuzzSharp.Buffer();
        buffer.AddUtf16(piece.Text);
        // Set before guessing, which only fills what isn't set: the script and the language.
        buffer.Direction = piece.RightToLeft ? Direction.RightToLeft : Direction.LeftToRight;
        buffer.GuessSegmentProperties();

        using var font = new SKFont(piece.Typeface, size);
        var result = shaper.Shape(buffer, 0, 0, font);

        return new([.. result.Codepoints.Select(glyph => (ushort)glyph)], result.Points, result.Width);
    }

    /// <summary>The line's pieces in visual order: its bidi runs reordered, each split by font.</summary>
    private IEnumerable<Piece> Pieces(string text, bool bold)
    {
        foreach (var (runText, level) in VisualRuns(text))
        {
            var rightToLeft = level % 2 is 1;
            var pieces = SplitByFont(runText, bold, rightToLeft);

            // HarfBuzz orders a right-to-left piece's glyphs itself; the pieces of such a run go right to left too.
            if (rightToLeft)
            {
                pieces.Reverse();
            }

            foreach (var piece in pieces)
            {
                yield return piece;
            }
        }
    }

    private List<Piece> SplitByFont(string text, bool bold, bool rightToLeft)
    {
        List<Piece> pieces = [];
        var current = new StringBuilder();
        SKTypeface? currentTypeface = null;

        foreach (var rune in text.EnumerateRunes())
        {
            var typeface = TypefaceOf(rune, bold, currentTypeface);

            if (currentTypeface is not null && typeface != currentTypeface)
            {
                pieces.Add(new(current.ToString(), currentTypeface, rightToLeft));
                current.Clear();
            }

            currentTypeface = typeface;
            current.Append(rune.ToString());
        }

        if (currentTypeface is not null)
        {
            pieces.Add(new(current.ToString(), currentTypeface, rightToLeft));
        }

        return pieces;
    }

    private SKTypeface TypefaceOf(Rune rune, bool bold, SKTypeface? previous)
    {
        var latin = (bold ? interSemiBold : interRegular).Value;
        var arabic = (persianDigits ? (bold ? vazirmatnFdSemiBold : vazirmatnFdRegular) : (bold ? vazirmatnSemiBold : vazirmatnRegular)).Value;

        if (rune.Value is >= '0' and <= '9')
            return persianDigits ? arabic : latin;

        if (IsRightToLeftScript(rune.Value))
            return arabic;

        // Spaces and punctuation stay in the font around them, so a Persian phrase isn't split at every space.
        if (Rune.IsLetter(rune) is false && previous is not null && Has(previous, rune.Value))
            return previous;

        if (Has(latin, rune.Value))
            return latin;

        if (Has(arabic, rune.Value))
            return arabic;

        var devanagari = (bold ? devanagariSemiBold : devanagariRegular).Value;
        if (Has(devanagari, rune.Value))
            return devanagari;

        if (Has(chinese.Value, rune.Value))
            return chinese.Value;

        return FallbackOf(rune.Value, bold) ?? latin;
    }

    private bool Has(SKTypeface typeface, int codePoint)
    {
        if (glyphChecks.TryGetValue(typeface, out var font) is false)
        {
            glyphChecks[typeface] = font = new SKFont(typeface);
        }

        return font.ContainsGlyph(codePoint);
    }

    /// <summary>A font of the server's own for a script the report doesn't bring, when it has one.</summary>
    private SKTypeface? FallbackOf(int codePoint, bool bold)
    {
        if (fallbacks.TryGetValue((codePoint, bold), out var typeface))
            return typeface;

        return fallbacks[(codePoint, bold)] = SKFontManager.Default.MatchCharacter(null, bold ? SKFontStyleWeight.SemiBold : SKFontStyleWeight.Normal,
                                                                                    SKFontStyleWidth.Normal, SKFontStyleSlant.Upright, [culture.Name], codePoint);
    }

    private enum BidiKind { Left, Right, Number, Neutral }

    /// <summary>
    /// The line as runs of one embedding level, in visual order (the Unicode bidi algorithm's rules W4, W5, W7, N1, N2, I1,
    /// I2 and L2, without explicit embeddings): an even level runs left to right, an odd one right to left.
    /// </summary>
    private List<(string Text, int Level)> VisualRuns(string text)
    {
        var runes = text.EnumerateRunes().ToArray();
        var kinds = runes.Select(KindOf).ToArray();
        var baseLevel = IsRightToLeft ? 1 : 0;

        // W4 and W5: a separator between digits, and a percent sign next to them, belong to the number.
        for (var i = 0; i < runes.Length; i++)
        {
            if (kinds[i] is not BidiKind.Neutral)
                continue;

            var value = runes[i].Value;
            var numberBefore = i > 0 && kinds[i - 1] is BidiKind.Number;
            var numberAfter = i + 1 < runes.Length && kinds[i + 1] is BidiKind.Number;

            if ((value is '.' or ',' or ':' or '/' or 0x066B or 0x066C && numberBefore && numberAfter)
                || (value is '%' or 0x066A or 0x2030 && (numberBefore || numberAfter)))
            {
                kinds[i] = BidiKind.Number;
            }
        }

        // W7: a number after left-to-right text, or at the start of a left-to-right line, reads as that text.
        var lastStrong = IsRightToLeft ? BidiKind.Right : BidiKind.Left;
        for (var i = 0; i < runes.Length; i++)
        {
            if (kinds[i] is BidiKind.Left or BidiKind.Right)
            {
                lastStrong = kinds[i];
            }
            else if (kinds[i] is BidiKind.Number && lastStrong is BidiKind.Left)
            {
                kinds[i] = BidiKind.Left;
            }
        }

        // N1 and N2: neutrals between two of one direction take it (numbers count as right to left), others the line's.
        var levels = new int[runes.Length];
        for (var i = 0; i < runes.Length; i++)
        {
            if (kinds[i] is not BidiKind.Neutral)
            {
                levels[i] = LevelOf(kinds[i], baseLevel);
                continue;
            }

            var end = i;
            while (end < runes.Length && kinds[end] is BidiKind.Neutral)
            {
                end++;
            }

            var before = i > 0 ? DirectionOf(kinds[i - 1]) : (IsRightToLeft ? BidiKind.Right : BidiKind.Left);
            var after = end < runes.Length ? DirectionOf(kinds[end]) : (IsRightToLeft ? BidiKind.Right : BidiKind.Left);
            var resolved = before == after ? before : (IsRightToLeft ? BidiKind.Right : BidiKind.Left);

            for (var j = i; j < end; j++)
            {
                levels[j] = LevelOf(resolved, baseLevel);
            }

            i = end - 1;
        }

        List<(string Text, int Level)> runs = [];
        for (var i = 0; i < runes.Length;)
        {
            var start = i;
            var run = new StringBuilder();
            while (i < runes.Length && levels[i] == levels[start])
            {
                run.Append(runes[i].ToString());
                i++;
            }

            runs.Add((run.ToString(), levels[start]));
        }

        // L2: from the highest level down to the lowest odd one, reverse every stretch of runs at that level or higher.
        var highest = runs.Count is 0 ? 0 : runs.Max(run => run.Level);
        for (var level = highest; level >= 1; level--)
        {
            for (var i = 0; i < runs.Count;)
            {
                if (runs[i].Level < level)
                {
                    i++;
                    continue;
                }

                var end = i;
                while (end < runs.Count && runs[end].Level >= level)
                {
                    end++;
                }

                runs.Reverse(i, end - i);
                i = end;
            }
        }

        return runs;
    }

    private static BidiKind DirectionOf(BidiKind kind) => kind is BidiKind.Number ? BidiKind.Right : kind;

    /// <summary>I1 and I2: the level a resolved kind takes in a line of <paramref name="baseLevel"/>.</summary>
    private static int LevelOf(BidiKind kind, int baseLevel) => (kind, baseLevel) switch
    {
        (BidiKind.Left, 0) => 0,
        (BidiKind.Left, _) => 2,
        (BidiKind.Right, _) => 1,
        (BidiKind.Number, _) => 2,
        _ => baseLevel
    };

    private static BidiKind KindOf(Rune rune)
    {
        var value = rune.Value;

        if (value is >= '0' and <= '9' or >= 0x0660 and <= 0x0669 or >= 0x06F0 and <= 0x06F9)
            return BidiKind.Number;

        // Arabic's comma, percent sign and number separators (٫ is Persian's decimal point) are punctuation, not letters.
        if (value is 0x060C or 0x066A or 0x066B or 0x066C)
            return BidiKind.Neutral;

        if (IsRightToLeftScript(value))
            return BidiKind.Right;

        return Rune.IsLetter(rune) || Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
            ? BidiKind.Left
            : BidiKind.Neutral;
    }

    /// <summary>Hebrew, Arabic, Syriac, Thaana, NKo and the other right-to-left blocks, their presentation forms included.</summary>
    private static bool IsRightToLeftScript(int value)
        => value is >= 0x0590 and <= 0x08FF or >= 0xFB1D and <= 0xFDFF or >= 0xFE70 and <= 0xFEFE
                 or >= 0x10800 and <= 0x10FFF or >= 0x1E800 and <= 0x1EFFF;

    private static SKTypeface Load(string fileName)
    {
        using var stream = typeof(SlaPdfText).Assembly.GetManifestResourceStream($"SlaFonts.{fileName}")
            ?? throw new InvalidOperationException($"The SLA report's font {fileName} isn't embedded in {typeof(SlaPdfText).Assembly.GetName().Name}.");

        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        // A copy Skia owns, since a typeface reads its file lazily, long after this stream is gone.
        using var data = SKData.CreateCopy(memory.ToArray());

        return SKTypeface.FromData(data)
            ?? throw new InvalidOperationException($"The SLA report's font {fileName} couldn't be read.");
    }
}

public enum SlaPdfAlign
{
    /// <summary>Where the language starts a line: left, or right in Persian and Arabic.</summary>
    Start,
    End,
    Center
}
