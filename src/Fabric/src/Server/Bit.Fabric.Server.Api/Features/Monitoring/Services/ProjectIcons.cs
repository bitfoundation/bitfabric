using System.Net;
using System.Security.Cryptography;
using Bit.Bmage;
using Bit.Fabric.Server.Api.Features.Attachments;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Attachments;
using FluentStorage.Storage;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// A connected app's own icon on its card and its status page (bitfabric-internal/specs/monitoring.md, The cards show the
/// app's own icon): the one its web app manifest names, read through the monitoring client and its public address guard,
/// shrunk to 128px as a WebP stripped of its metadata, as an uploaded picture is (See AttachmentController), and served
/// from bit Fabric's server, the only origin its Content-Security-Policy allows images from. Each icon is an attachment
/// under an id of its own, never the project's, as the public status page shows it, so a new icon is a new address no
/// cache has seen. Read on the first round, after each deployment and once a day; a manifest that can't be read keeps the
/// icon there is.
/// </summary>
public partial class ProjectIcons(AppDbContext dbContext,
    IStore blobStorage,
    BmageImageProcessor imageProcessor,
    ProjectAddressProbe addressProbe,
    IHttpClientFactory httpClientFactory,
    ServerApiSettings settings,
    TimeProvider timeProvider,
    ILogger<ProjectIcons> logger)
{
    public const int Size = 128;

    /// <summary>Far more than an app icon weighs; the template's is 21 KB at 512px.</summary>
    public const int MaxBytes = 1024 * 1024;

    public static readonly TimeSpan ReadEvery = TimeSpan.FromDays(1);

    /// <summary>The formats a manifest's icon is taken in: raster ones Bmage decodes. An SVG can carry script, so it isn't.</summary>
    private static readonly BmageImageFormat[] formats = [.. AttachmentController.AllowedImageFormats, BmageImageFormat.Ico];

    public static bool IsDue(Project project, DateTimeOffset now) => project.IconReadAt is not { } readAt || now - readAt >= ReadEvery;

    /// <summary>Reads the manifest again and stores its icon when it changed; the caller saves the project.</summary>
    public async Task Refresh(Project project, CancellationToken cancellationToken)
    {
        project.IconReadAt = timeProvider.GetUtcNow();

        var (manifest, manifestUrl) = await addressProbe.ReadManifest(project.Address!, cancellationToken);
        if (manifest is null || manifestUrl is null)
            return;

        if (Pick(manifest, manifestUrl) is not { } iconUrl)
        {
            await Replace(project, null, null, cancellationToken);
            return;
        }

        if (await Download(iconUrl, cancellationToken) is not { } source)
            return;

        var hash = Convert.ToHexStringLower(SHA256.HashData(source));
        if (hash == project.IconSourceHash && project.IconId is not null)
            return;

        if (imageProcessor.Identify(source) is not { } info || formats.Contains(info.Format) is false)
        {
            logger.LogInformation("The icon of project {ProjectId} at {Url} is in a format bit Fabric doesn't take.", project.Id, iconUrl);
            return;
        }

        byte[] webp;
        try
        {
            // The decode turns a sideways image upright, and Bmage's limits bound what an image from another site can cost.
            using var image = await imageProcessor.DecodeAsync(source, cancellationToken: cancellationToken);

            using var resized = await imageProcessor.ProcessAsync(image, BmageImagePipeline.Create().MaxSize(Size, Size), cancellationToken: cancellationToken);

            // Only the ICC profile is kept, as for an uploaded picture: a wide-gamut icon looks washed out without it.
            resized.Metadata = image.Metadata is { IccProfile.IsEmpty: false } metadata ? BmageImageMetadata.Empty.WithIccProfile(metadata.IccProfile) : null;

            webp = (await imageProcessor.EncodeAsync(resized, new BmageImageExportOptions { Format = BmageImageFormat.WebP, Metadata = BmageMetadataPolicy.Preserve }, cancellationToken)).Data;
        }
        catch (BmageImageException exp)
        {
            logger.LogInformation("The icon of project {ProjectId} at {Url} couldn't be read: {Reason}", project.Id, iconUrl, exp.Message);
            return;
        }

        await Replace(project, webp, hash, cancellationToken);
    }

    /// <summary>
    /// The icon a card shows best: one for any purpose rather than only a maskable one, the smallest of 96px or more, else
    /// the largest, in a raster format.
    /// </summary>
    public static Uri? Pick(WebAppManifest manifest, Uri manifestUrl)
    {
        var icons = (manifest.Icons ?? [])
            .Where(icon => string.IsNullOrWhiteSpace(icon.Src) is false)
            .Where(icon => string.IsNullOrWhiteSpace(icon.Purpose) || icon.Purpose.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("any", StringComparer.OrdinalIgnoreCase))
            .Where(icon => icon.Type?.Contains("svg", StringComparison.OrdinalIgnoreCase) is not true && icon.Src!.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) is false)
            .Select(icon => (Icon: icon, Size: SizeOf(icon.Sizes)))
            .ToArray();

        var best = icons.Where(icon => icon.Size >= 96).OrderBy(icon => icon.Size).FirstOrDefault().Icon
                   ?? icons.OrderByDescending(icon => icon.Size).FirstOrDefault().Icon;

        return best is not null && Uri.TryCreate(manifestUrl, best.Src, out var url) && url.Scheme is "https" or "http" ? url : null;
    }

    /// <summary>The largest side its sizes give: "192x192 512x512" is 512, "any" is taken for a large one, none for 0.</summary>
    private static int SizeOf(string? sizes)
    {
        if (string.IsNullOrWhiteSpace(sizes))
            return 0;

        return sizes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(size => size.Equals("any", StringComparison.OrdinalIgnoreCase) ? 512
                : size.Split('x', 'X') is [var width, var height] && int.TryParse(width, out var w) && int.TryParse(height, out var h) ? Math.Max(w, h) : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private async Task<byte[]?> Download(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            var http = httpClientFactory.CreateClient(MonitoringHttp.ClientName);
            using var response = await http.GetFollowingSameOriginRedirects(url.ToString(), cancellationToken);

            if (response.StatusCode is not HttpStatusCode.OK || response.Content.Headers.ContentLength > MaxBytes)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > MaxBytes)
                    return null;
                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
        catch (Exception exp) when (exp is HttpRequestException or TaskCanceledException)
        {
            logger.LogInformation("The icon at {Url} couldn't be downloaded: {Reason}", url, exp.Message);
            return null;
        }
    }

    /// <summary>Stores the new icon under a new id and removes the old one; null for none, and the card shows a monogram.</summary>
    private async Task Replace(Project project, byte[]? webp, string? hash, CancellationToken cancellationToken)
    {
        var old = project.IconId;

        if (webp is not null)
        {
            var id = Guid.NewGuid();
            var path = AttachmentController.GetFilePath(settings, id, AttachmentKind.ProjectIcon);

            using var stream = new MemoryStream(webp);
            await blobStorage.SetObject(path, stream, "image/webp", cancellationToken: cancellationToken);
            await dbContext.Attachments.AddAsync(new() { Id = id, Kind = AttachmentKind.ProjectIcon, Path = path, CreatedOn = timeProvider.GetUtcNow() }, cancellationToken);

            project.IconId = id;
            project.IconSourceHash = hash;
        }
        else
        {
            project.IconId = null;
            project.IconSourceHash = null;
        }

        if (old is { } oldId && await dbContext.Attachments.FirstOrDefaultAsync(attachment => attachment.Id == oldId && attachment.Kind == AttachmentKind.ProjectIcon, cancellationToken) is { } previous)
        {
            await blobStorage.DeleteSingleObject(previous.Path!, cancellationToken);
            dbContext.Attachments.Remove(previous);
        }
    }
}
