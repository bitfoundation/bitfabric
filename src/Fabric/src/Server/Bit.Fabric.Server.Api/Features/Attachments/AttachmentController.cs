using System.Diagnostics.Metrics;
using Bit.Bmage;
using Bit.Fabric.Server.Api.Features.Identity;
using Bit.Fabric.Server.Shared.Infrastructure.Services;
using Bit.Fabric.Shared.Features.Attachments;
using FluentStorage.Storage;

namespace Bit.Fabric.Server.Api.Features.Attachments;

[ApiController]
[ApiVersion(1)]
[Route("api/v{v:apiVersion}/[controller]/[action]")]
public partial class AttachmentController : AppControllerBase, IAttachmentController
{
    [AutoInject] private IStore blobStorage = default!;
    [AutoInject] private UserManager<User> userManager = default!;
    [AutoInject] private BmageImageProcessor imageProcessor = default!;

    [AutoInject] private IServiceProvider serviceProvider = default!;
    [AutoInject] private ILogger<AttachmentController> logger = default!;

    [AutoInject] private IHubContext<AppHub> appHubContext = default!;

    [AutoInject] private ResponseCacheService responseCacheService = default!;

    [AutoInject] private IConfiguration configuration = default!;

    /// <summary>The largest upload every endpoint here accepts; the Dev MCP reports this value rather than a copy of it.</summary>
    public const int MaxUploadSizeBytes = 11 * 1024 * 1024;

    public static readonly BmageImageFormat[] AllowedImageFormats = [BmageImageFormat.Jpeg, BmageImageFormat.Png, BmageImageFormat.WebP, BmageImageFormat.Gif, BmageImageFormat.Avif, BmageImageFormat.Bmp];

    // For open telemetry metrics
    private static readonly Histogram<double> updateResizeDurationHistogram = Meter.Current.CreateHistogram<double>("attachment.resize_duration", "ms", "Elapsed time to resize and persist an uploaded image");

    [HttpPost]
    [RequestSizeLimit(MaxUploadSizeBytes)]
    public async Task<IActionResult> UploadUserProfileImage(IFormFile? file, CancellationToken cancellationToken)
    {
        return await UploadAttachment(
             User.GetUserId(),
             [AttachmentKind.UserProfileImageSmall, AttachmentKind.UserProfileImageOriginal],
             file,
             cancellationToken);
    }


    /// <summary>
    /// Takes an image the user attached to an AI chat message and answers with the id it was stored under, which is
    /// all the client needs to build the attachment url it puts in the message.
    /// <para>
    /// The id is minted here rather than accepted from the client: an attachment id is the whole address of a blob
    /// (See <see cref="GetFilePath(Guid, AttachmentKind)"/>), so a caller-chosen one would let a user overwrite somebody else's attachment -
    /// their profile image included - by naming its id.
    /// </para>
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxUploadSizeBytes)]
    public async Task<IActionResult> UploadAiChatImage(IFormFile? file, CancellationToken cancellationToken)
    {
        var attachmentId = Guid.CreateSequentialGuid();

        var result = await UploadAttachment(attachmentId, [AttachmentKind.AiChatImage], file, cancellationToken);

        return result is OkObjectResult ? Ok(attachmentId.ToString()) : result;
    }

    [AllowAnonymous]
    [HttpGet("{attachmentId}/{kind}")]
    [AppResponseCache(MaxAge = 3600 * 24 * 7, UserAgnostic = true, SkipOutputCache = true, CacheTagTemplate = ResponseCacheService.AttachmentCacheTagTemplate)]
    public async Task<IActionResult> GetAttachment(Guid attachmentId, AttachmentKind kind, CancellationToken cancellationToken = default)
    {
        // If the backend is hosted behind a CDN (which is recommended for production), the GetAttachment method's returned stream will be cached on CDN edge servers.
        // Alternatively, you can generate URLs that allow clients to download files directly from the file storage, further reducing the load on the backend.
        // If security is a concern, you can generate short-lived signed URLs for the file storage. These signed URLs can be validated either at the CDN edge or on the file storage server, ensuring that only authorized users can access the files.

        var filePath = GetFilePath(attachmentId, kind);

        if (await blobStorage.ObjectExists(filePath, cancellationToken) is false)
            throw new ResourceNotFoundException().WithData("Reason", "The attachment does not exist.");

        return File(await blobStorage.OpenRead(filePath, cancellationToken), "image/webp", enableRangeProcessing: true);
    }

    [HttpDelete]
    public async Task DeleteUserProfileImage(CancellationToken cancellationToken)
    {
        await DeleteAttachment(User.GetUserId(), [AttachmentKind.UserProfileImageSmall, AttachmentKind.UserProfileImageOriginal], cancellationToken);
    }


    private async Task PublishUserProfileUpdated(User user, CancellationToken cancellationToken)
    {
        // Notify other sessions of the user that user's info has been updated, so they'll update their UI.
        var currentUserSessionId = User.GetSessionId();
        var userSessionIdsExceptCurrentUserSessionId = await DbContext.UserSessions
            .Where(us => us.UserId == user.Id && us.Id != currentUserSessionId && us.SignalRConnectionId != null)
            .Select(us => us.SignalRConnectionId!)
            .ToArrayAsync(cancellationToken);
        await appHubContext.Clients.Clients(userSessionIdsExceptCurrentUserSessionId).Publish(SharedAppMessages.PROFILE_UPDATED, user.Map(), cancellationToken);
    }

    private async Task DeleteAttachment(Guid attachmentId, AttachmentKind[] kinds, CancellationToken cancellationToken)
    {
        var attachments = await DbContext.Attachments.Where(p => p.Id == attachmentId && kinds.Contains(p.Kind)).ToArrayAsync(cancellationToken);

        foreach (var attachment in attachments)
        {
            var filePath = attachment.Path;

            if (await blobStorage.ObjectExists(filePath, cancellationToken) is false)
                throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ImageCouldNotBeFound)]);

            await blobStorage.DeleteSingleObject(filePath, cancellationToken);


            if (attachment.Kind is AttachmentKind.UserProfileImageOriginal)
            {
                var user = await userManager.FindByIdAsync(User.GetUserId().ToString());
                user!.HasProfileImage = false;

                var result = await userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    throw new ResourceValidationException(result.Errors.Select(err => new LocalizedString(err.Code, err.Description)).ToArray());

                await PublishUserProfileUpdated(user, cancellationToken);
            }

            DbContext.Attachments.Remove(attachment);
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        // After the loop: every kind shares one tag, and by now no blob is left for a racing read to re-cache.
        await responseCacheService.PurgeAttachmentCache(attachmentId);
    }

    private async Task<IActionResult> UploadAttachment(Guid attachmentId, AttachmentKind[] kinds, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
            throw new BadRequestException().WithData("Reason", "No file provided.");

        byte[] uploadedBytes;
        await using (var uploadStream = file.OpenReadStream())
        {
            using var buffer = new MemoryStream();
            await uploadStream.CopyToAsync(buffer, cancellationToken);
            uploadedBytes = buffer.ToArray();
        }

        if (imageProcessor.Identify(uploadedBytes) is not { } imageInfo || AllowedImageFormats.Contains(imageInfo.Format) is false)
            return BadRequest(Localizer[nameof(AppStrings.UnsupportedImageFormat)].ToString());

        // The decode turns a sideways phone photo upright; Bmage's limits bound what an untrusted upload can cost.
        BmageImageBuffer decodedImage;
        try
        {
            decodedImage = await imageProcessor.DecodeAsync(uploadedBytes, cancellationToken: cancellationToken);
        }
        catch (BmageImageException)
        {
            return BadRequest(Localizer[nameof(AppStrings.UnsupportedImageFormat)].ToString());
        }
        using BmageImageBuffer sourceImage = decodedImage;

        // Only the ICC profile is kept: EXIF carries GPS and the camera serial, and a wide-gamut photo looks washed out without the profile.
        var storedMetadata = sourceImage.Metadata is { IccProfile.IsEmpty: false } uploadedMetadata
            ? BmageImageMetadata.Empty.WithIccProfile(uploadedMetadata.IccProfile)
            : null;

        string? altText = null; // AI-generated alt text, when the analysis agent is configured.

        var preparedUploads = new List<(Attachment Attachment, byte[] Bytes)>();

        foreach (var kind in kinds)
        {
            var attachment = new Attachment
            {
                Id = attachmentId,
                Kind = kind,
                Path = GetFilePath(attachmentId, kind),
                CreatedOn = TimeProvider.GetUtcNow(),
            };

            // ShrinkOnly makes the size a ceiling instead of a floor: the picture is only scaled down to it, and one
            // that is already smaller is stored exactly as it arrived rather than rejected or blown up.
            (bool NeedsResize, uint Width, uint Height, bool ShrinkOnly) imageResizeContext = kind switch
            {
                AttachmentKind.UserProfileImageSmall => (true, 256, 256, false),
                // Whatever the user had on screen when they attached it - a crop, a screenshot, a phone photo - so
                // there is no size below which it is not worth showing the model.
                AttachmentKind.AiChatImage => (true, 512, 512, true),
                _ => (false, 0, 0, false)
            };

            Stopwatch stopwatch = Stopwatch.StartNew();

            if (imageResizeContext.NeedsResize &&
                imageResizeContext.ShrinkOnly is false &&
                (sourceImage.Width < imageResizeContext.Width || sourceImage.Height < imageResizeContext.Height))
                return BadRequest(Localizer[nameof(AppStrings.ImageTooSmall), imageResizeContext.Width, imageResizeContext.Height, sourceImage.Width, sourceImage.Height].ToString());

            using var resizedImage = imageResizeContext.NeedsResize
                ? await imageProcessor.ProcessAsync(sourceImage, BmageImagePipeline.Create().MaxSize((int)imageResizeContext.Width, (int)imageResizeContext.Height), cancellationToken: cancellationToken)
                : null;
            var storedImage = resizedImage ?? sourceImage;
            storedImage.Metadata = storedMetadata;

            var storedBytes = (await imageProcessor.EncodeAsync(storedImage, new BmageImageExportOptions
            {
                Format = BmageImageFormat.WebP,
                Lossless = imageResizeContext.NeedsResize is false,
                Metadata = BmageMetadataPolicy.Preserve,
            }, cancellationToken)).Data;

            updateResizeDurationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("kind", kind.ToString()));


            preparedUploads.Add((attachment, storedBytes));
        }

        //  ---------------------------------------------------------------------------------------------
        //  PHASE 2 - everything validated; now mutate.
        //
        //  A re-upload keeps the rows it finds instead of deleting and re-inserting them: Attachment's key is
        //  composite - { Id, Kind } (AttachmentConfiguration) - and EF cannot track a second instance with a key it
        //  already tracks, even when the tracked one is marked Deleted. Nothing in an existing row can differ,
        //  since GetFilePath is a pure function of those same two values.
        //
        //  For the same reason there is no stale blob to clean up: the new key IS the old key, so the writes
        //  below overwrite in place.
        //  ---------------------------------------------------------------------------------------------
        var existingKinds = await DbContext.Attachments
            .Where(att => att.Id == attachmentId)
            .Select(att => att.Kind)
            .ToArrayAsync(cancellationToken);

        foreach (var (attachment, _) in preparedUploads.Where(upload => existingKinds.Contains(upload.Attachment.Kind) is false))
        {
            await DbContext.Attachments.AddAsync(attachment, cancellationToken);
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        var wroteAnyBlob = false;

        try
        {
            foreach (var (attachment, storedBytes) in preparedUploads)
            {
                // SetBytes would leave the content type to be guessed from the key.
                using MemoryStream storedStream = new(storedBytes);
                await blobStorage.SetObject(attachment.Path, storedStream, "image/webp", cancellationToken: cancellationToken);
                wroteAnyBlob = true;
            }
        }
        finally
        {
            // The replacement is stored under the very key the old bytes were (See GetFilePath), so nothing else
            // invalidates the copies already on the edge and in browsers. In the finally because a second kind that
            // fails still leaves the first one replaced, and the edge still holding what it replaced.
            if (wroteAnyBlob)
            {
                await responseCacheService.PurgeAttachmentCache(attachmentId);
            }
        }


        if (kinds.Contains(AttachmentKind.UserProfileImageSmall))
        {
            var user = await userManager.FindByIdAsync(User.GetUserId().ToString());
            user!.HasProfileImage = true;

            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new ResourceValidationException(result.Errors.Select(err => new LocalizedString(err.Code, err.Description)).ToArray());

            await PublishUserProfileUpdated(user, cancellationToken);
        }

        return Ok(altText);
    }

    /// <summary>
    /// A pure function of { Id, Kind }: no part of the key comes from the uploaded file, so a re-upload lands on the
    /// very key it replaces and leaves no blob behind that no row names. Every kind is stored as WebP.
    /// <br/>
    /// Environment variables are expanded over the CONFIGURED directory prefix only, never over anything the client
    /// influenced - a file name ending ".%TEMP%" used to expand a server environment value into the storage key.
    /// </summary>
    private string GetFilePath(Guid attachmentId, AttachmentKind kind) => GetFilePath(AppSettings, attachmentId, kind);

    /// <inheritdoc cref="GetFilePath(Guid, AttachmentKind)"/>
    /// <remarks>
    /// Static so that whoever needs a blob can work out where it is without asking this controller or the database
    /// for it - <c>AppChatbot</c> reads an attached image straight off storage this way.
    /// </remarks>
    public static string GetFilePath(ServerApiSettings appSettings, Guid attachmentId, AttachmentKind kind)
    {
        var directory = kind switch
        {
            AttachmentKind.AiChatImage => appSettings.AiChatImagesDir,
            AttachmentKind.UserProfileImageSmall or AttachmentKind.UserProfileImageOriginal => appSettings.UserProfileImagesDir,
            AttachmentKind.ProjectIcon => appSettings.ProjectIconsDir,
            _ => throw new NotImplementedException()
        };

        directory = Environment.ExpandEnvironmentVariables(directory);

        return $"{directory}{attachmentId}_{kind}.webp";
    }

    public record AIImageReviewResponse(bool IsCar, double Confidence, string? Alt, string? Reasoning);
}
