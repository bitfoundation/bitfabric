using Bit.Fabric.Shared.Features.Attachments;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Bmage;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// The app's own icon on its card and its status page (bitfabric-internal/specs/monitoring.md, The cards show the app's own
/// icon).
/// </summary>
public partial class ProjectMonitoringTests
{
    private static readonly BmageRgba32 Teal = new(0, 128, 128), Red = new(255, 0, 0), Navy = new(0, 0, 128);

    /// <summary>
    /// The card shows the icon the app's web app manifest names for any purpose, not its maskable one or its SVG, shrunk
    /// to 128px and served from bit Fabric's own server. A deployment that brings a new icon gets a new address and the
    /// old one is gone; one whose manifest names none goes back to the monogram.
    /// </summary>
    [TestMethod]
    public async Task AnApp_Should_BeShownWithTheIconItsManifestNames_AndTheNewOneAfterADeployment()
    {
        var customerApp = new FakeCustomerApp();
        customerApp.ManifestIcons.AddRange(
        [
            new() { ["src"] = "images/icons/maskable-192.png", ["sizes"] = "192x192", ["type"] = "image/png", ["purpose"] = "maskable" },
            new() { ["src"] = "images/icons/logo.svg", ["sizes"] = "any", ["type"] = "image/svg+xml" },
            new() { ["src"] = "images/icons/icon-512.png", ["sizes"] = "512x512", ["type"] = "image/png" }
        ]);
        customerApp.Files["/images/icons/icon-512.png"] = (Png(512, Teal), "image/png");
        // Served too, so taking the maskable one instead would show.
        customerApp.Files["/images/icons/maskable-192.png"] = (Png(192, Red), "image/png");

        await using var server = await StartServer(customerApp);
        await using var client = await SignInWithATeamOfTheirOwn(server);
        var projects = client.GetController<IMonitoringsController>();
        var project = await Connect(projects, customerApp, "customer.example");

        var first = (await projects.GetMonitorings(null, TestContext.CancellationToken)).Single().IconId;
        Assert.IsNotNull(first, "Read on the first round.");

        // Anonymous, as the public status page and a browser's img ask for it.
        using var http = new HttpClient { BaseAddress = server.ApiAppAddress };
        async Task<HttpResponseMessage> Get(Guid id) => await http.GetAsync($"api/v1/Attachment/GetAttachment/{id}/{AttachmentKind.ProjectIcon}", TestContext.CancellationToken);

        using (var response = await Get(first.Value))
        {
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.CancellationToken);
            var imageProcessor = server.ApiApp.Services.GetRequiredService<BmageImageProcessor>();
            var info = imageProcessor.Identify(bytes);
            Assert.IsNotNull(info, "An image the app can read.");
            Assert.AreEqual(BmageImageFormat.WebP, info.Format);
            using var icon = await imageProcessor.DecodeAsync(bytes, cancellationToken: TestContext.CancellationToken);
            Assert.AreEqual(128, icon.Width, "Shrunk to fit the card.");
            Assert.IsTrue(IsNear(icon.GetRow(64)[64], Teal), "The icon for any purpose, not the maskable one.");
        }

        // A round within the day reads nothing again.
        await ProbeAt(server, project.Id, DateTimeOffset.UtcNow.AddHours(1));
        Assert.AreEqual(first, (await projects.GetMonitorings(null, TestContext.CancellationToken)).Single().IconId);

        // A deployment with a new icon.
        customerApp.Files["/images/icons/icon-512.png"] = (Png(512, Navy), "image/png");
        customerApp.Report.ApplicationVersion = "1.0.1+bbbb222";
        customerApp.Report.StartedAt = DateTimeOffset.UtcNow.AddHours(2);
        await ProbeAt(server, project.Id, DateTimeOffset.UtcNow.AddHours(2).AddMinutes(1));

        var second = (await projects.GetMonitorings(null, TestContext.CancellationToken)).Single().IconId;
        Assert.IsNotNull(second);
        Assert.AreNotEqual(first, second, "A new icon is a new address, which no cache has seen.");
        using (var gone = await Get(first.Value))
        {
            Assert.AreEqual(HttpStatusCode.NotFound, gone.StatusCode, "The old one is deleted.");
        }

        // A deployment whose manifest names no icon.
        customerApp.ManifestIcons.Clear();
        customerApp.Report.ApplicationVersion = "1.0.2+cccc333";
        customerApp.Report.StartedAt = DateTimeOffset.UtcNow.AddHours(4);
        await ProbeAt(server, project.Id, DateTimeOffset.UtcNow.AddHours(4).AddMinutes(1));

        Assert.IsNull((await projects.GetMonitorings(null, TestContext.CancellationToken)).Single().IconId, "The card goes back to the monogram.");
    }

    /// <summary>Within 5% on each channel, as a lossy WebP keeps a color only about.</summary>
    private static bool IsNear(BmageRgba32 actual, BmageRgba32 expected)
        => Math.Abs(actual.R - expected.R) <= 13 && Math.Abs(actual.G - expected.G) <= 13 && Math.Abs(actual.B - expected.B) <= 13;

    /// <summary>
    /// A square PNG of one color, written here rather than with the app's image library, so what the test feeds the app
    /// doesn't depend on the code it tests.
    /// </summary>
    private static byte[] Png(int size, BmageRgba32 color)
    {
        var row = new byte[1 + size * 3];
        for (var x = 0; x < size; x++)
        {
            row[1 + x * 3] = color.R;
            row[2 + x * 3] = color.G;
            row[3 + x * 3] = color.B;
        }

        using var pixels = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(pixels, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            for (var y = 0; y < size; y++)
                zlib.Write(row);
        }

        using var png = new MemoryStream();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(png, "IHDR", [.. BigEndian(size), .. BigEndian(size), 8, 2, 0, 0, 0]);
        Chunk(png, "IDAT", pixels.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();

        static byte[] BigEndian(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

        static void Chunk(Stream stream, string type, byte[] data)
        {
            byte[] typeAndData = [.. System.Text.Encoding.ASCII.GetBytes(type), .. data];
            stream.Write(BigEndian(data.Length));
            stream.Write(typeAndData);
            stream.Write(BigEndian((int)Crc32(typeAndData)));
        }

        static uint Crc32(byte[] bytes)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in bytes)
            {
                crc ^= b;
                for (var bit = 0; bit < 8; bit++)
                    crc = (crc & 1) is 1 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
            return ~crc;
        }
    }
}
