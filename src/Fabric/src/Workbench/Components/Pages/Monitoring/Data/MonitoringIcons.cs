using System.IO.Compression;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// The sample apps' own icons, as bit Fabric's server keeps the one each app's web app manifest names (ProjectIcons): small
/// PNGs drawn here, each a rounded square in the app's color with a mark of its own. The Workbench serves them at the
/// attachment address the app's cards and status pages ask for.
/// </summary>
public static class MonitoringIcons
{
    private const int Size = 96;
    private const int Samples = 4;

    /// <summary>The apps that have an icon: its two colors and its mark.</summary>
    private static readonly Dictionary<string, (uint From, uint To, Mark Mark)> icons = new()
    {
        ["yourapp"] = (0x2563eb, 0x60a5fa, Mark.Ring),
        ["brightcart"] = (0xea580c, 0xfb923c, Mark.Bag),
        ["fieldnote"] = (0x0f766e, 0x2dd4bf, Mark.Lines),
        ["crewboard"] = (0x4338ca, 0x818cf8, Mark.Columns),
        ["parcelpoint"] = (0xb45309, 0xfbbf24, Mark.Diamond),
        ["inkpad"] = (0x6d28d9, 0xc084fc, Mark.Drop),
        ["tablewise"] = (0x0e7490, 0x22d3ee, Mark.Dots),
        ["fleetline"] = (0x9a3412, 0xf97316, Mark.Play),
        ["learnly"] = (0xbe185d, 0xf472b6, Mark.Ring)
    };

    private static readonly ConcurrentDictionary<string, byte[]> drawn = [];

    private enum Mark { Ring, Bag, Lines, Columns, Diamond, Drop, Dots, Play }

    public static Guid IdOf(string key) => MonitoringNoise.IdOf($"icon:{key}");

    /// <summary>The icon behind an attachment id, as a PNG; null for an id that is none of them.</summary>
    public static byte[]? Find(Guid id)
    {
        var key = icons.Keys.FirstOrDefault(key => IdOf(key) == id);

        return key is null ? null : drawn.GetOrAdd(key, Draw);
    }

    private static byte[] Draw(string key)
    {
        var (from, to, mark) = icons[key];
        var pixels = new byte[Size * Size * 4];

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                double r = 0, g = 0, b = 0, a = 0;

                // Supersampled, so the edges are smooth.
                for (var sy = 0; sy < Samples; sy++)
                {
                    for (var sx = 0; sx < Samples; sx++)
                    {
                        var u = (x + (sx + 0.5) / Samples) / Size;
                        var v = (y + (sy + 0.5) / Samples) / Size;

                        if (InRoundedSquare(u, v, 0.22) is false)
                            continue;

                        var t = (u + v) / 2;
                        var (cr, cg, cb) = Mix(from, to, t);

                        if (InMark(mark, u, v))
                        {
                            (cr, cg, cb) = (255, 255, 255);
                        }

                        r += cr;
                        g += cg;
                        b += cb;
                        a += 1;
                    }
                }

                var offset = (y * Size + x) * 4;

                if (a > 0)
                {
                    pixels[offset] = (byte)Math.Round(r / a);
                    pixels[offset + 1] = (byte)Math.Round(g / a);
                    pixels[offset + 2] = (byte)Math.Round(b / a);
                }

                pixels[offset + 3] = (byte)Math.Round(255 * a / (Samples * Samples));
            }
        }

        return Png(Size, Size, pixels);
    }

    private static bool InRoundedSquare(double u, double v, double radius)
    {
        var dx = Math.Max(Math.Max(radius - u, u - (1 - radius)), 0);
        var dy = Math.Max(Math.Max(radius - v, v - (1 - radius)), 0);

        return dx * dx + dy * dy <= radius * radius;
    }

    private static bool InMark(Mark mark, double u, double v)
    {
        double Distance(double cx, double cy) => Math.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));

        return mark switch
        {
            Mark.Ring => Distance(0.5, 0.5) is > 0.17 and < 0.29,
            Mark.Bag => (u is > 0.28 and < 0.72 && v is > 0.4 and < 0.75) || (Distance(0.5, 0.4) is > 0.1 and < 0.16 && v < 0.4),
            Mark.Lines => u is > 0.27 and < 0.73 && (v is > 0.3 and < 0.36 || v is > 0.45 and < 0.51 || (v is > 0.6 and < 0.66 && u < 0.58)),
            Mark.Columns => v is > 0.28 and < 0.72 && (u is > 0.26 and < 0.38 || (u is > 0.44 and < 0.56 && v < 0.58) || (u is > 0.62 and < 0.74 && v < 0.48)),
            Mark.Diamond => Math.Abs(u - 0.5) + Math.Abs(v - 0.5) < 0.24,
            Mark.Drop => Distance(0.5, 0.58) < 0.17 || (v is > 0.24 and < 0.58 && Math.Abs(u - 0.5) < (v - 0.24) * 0.5),
            Mark.Dots => Distance(0.34, 0.5) < 0.09 || Distance(0.5, 0.5) < 0.09 || Distance(0.66, 0.5) < 0.09,
            _ => u > 0.36 && u < 0.72 && Math.Abs(v - 0.5) < (0.72 - u) * 0.62
        };
    }

    private static (double R, double G, double B) Mix(uint from, uint to, double t)
    {
        static double Channel(uint color, int shift) => (color >> shift) & 0xff;

        return (Channel(from, 16) + (Channel(to, 16) - Channel(from, 16)) * t,
                Channel(from, 8) + (Channel(to, 8) - Channel(from, 8)) * t,
                Channel(from, 0) + (Channel(to, 0) - Channel(from, 0)) * t);
    }

    /// <summary>An RGBA image as a PNG: a header, the rows deflated with no filter, and the end.</summary>
    private static byte[] Png(int width, int height, byte[] rgba)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        var header = new byte[13];
        WriteBigEndian(header, 0, (uint)width);
        WriteBigEndian(header, 4, (uint)height);
        header[8] = 8; // bits a channel
        header[9] = 6; // RGBA
        Chunk(output, "IHDR", header);

        using (var rows = new MemoryStream())
        {
            using (var zlib = new ZLibStream(rows, CompressionLevel.Optimal, leaveOpen: true))
            {
                for (var y = 0; y < height; y++)
                {
                    zlib.WriteByte(0);
                    zlib.Write(rgba, y * width * 4, width * 4);
                }
            }

            Chunk(output, "IDAT", rows.ToArray());
        }

        Chunk(output, "IEND", []);

        return output.ToArray();
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, (uint)data.Length);
        output.Write(length);

        var typeAndData = new byte[4 + data.Length];
        for (var i = 0; i < 4; i++)
        {
            typeAndData[i] = (byte)type[i];
        }

        data.CopyTo(typeAndData, 4);
        output.Write(typeAndData);

        var crc = new byte[4];
        WriteBigEndian(crc, 0, Crc32(typeAndData));
        output.Write(crc);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static readonly uint[] crcTable = [.. Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++)
        {
            c = (c & 1) is not 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
        }

        return c;
    })];

    private static uint Crc32(byte[] bytes)
    {
        var c = 0xffffffff;

        foreach (var b in bytes)
        {
            c = crcTable[(c ^ b) & 0xff] ^ (c >> 8);
        }

        return c ^ 0xffffffff;
    }
}
