using Threads.Application.DTOs.Media;
using Threads.Application.Interfaces.Media;
using Threads.Domain.Enums;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.Services;

public class MediaService : IMediaService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
        "video/mp4",
        "video/quicktime",
        "video/webm"
    };

    private const long MaxImageSizeInBytes = 10 * 1024 * 1024;
    private const long MaxVideoSizeInBytes = 100 * 1024 * 1024;

    private readonly IMediaRepository _mediaRepository;
    private readonly IMediaProcessingService _mediaProcessingService;
    private readonly IObjectStorageService _objectStorageService;

    public MediaService(
        IMediaRepository mediaRepository,
        IMediaProcessingService mediaProcessingService,
        IObjectStorageService objectStorageService)
    {
        _mediaRepository = mediaRepository;
        _mediaProcessingService = mediaProcessingService;
        _objectStorageService = objectStorageService;
    }

    public async Task<MediaUrlResponse?> GetUrlAsync(
        Guid mediaId,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var media = await _mediaRepository.GetByIdAsync(mediaId, cancellationToken);

        if (media is null)
        {
            return null;
        }

        var canAccess = media.PostId.HasValue || currentUserId == media.UploadedByUserId;

        if (!canAccess)
        {
            return null;
        }

        return new MediaUrlResponse
        {
            Id = media.Id,
            Url = _objectStorageService.GetReadUrl(media.StorageKey)
        };
    }

    public async Task<UploadMediaResponse> UploadAsync(
        Guid uploadedByUserId,
        Stream content,
        string fileName,
        string contentType,
        long sizeInBytes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type is required.", nameof(contentType));
        }

        if (sizeInBytes <= 0)
        {
            throw new ArgumentException("File must not be empty.", nameof(sizeInBytes));
        }

        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException("Unsupported media content type.");
        }

        var mediaType = ResolveMediaType(contentType);
        ValidateFileSize(sizeInBytes, mediaType);
        var tempSourceFilePath = await SaveToTemporaryFileAsync(content, fileName, cancellationToken);
        string? tempProcessedFilePath = null;
        string? tempThumbnailFilePath = null;

        try
        {
            var processedMedia = await _mediaProcessingService.ProcessAsync(
                tempSourceFilePath,
                contentType,
                cancellationToken);
            tempProcessedFilePath = processedMedia.ProcessedFilePath;
            tempThumbnailFilePath = processedMedia.ThumbnailFilePath;
            var uploadFilePath = string.IsNullOrWhiteSpace(tempProcessedFilePath)
                ? tempSourceFilePath
                : tempProcessedFilePath;
            var storedContentType = string.IsNullOrWhiteSpace(processedMedia.OutputContentType)
                ? contentType
                : processedMedia.OutputContentType;
            var storedFileName = string.IsNullOrWhiteSpace(processedMedia.OutputFileName)
                ? ResolveStoredFileName(fileName, storedContentType)
                : processedMedia.OutputFileName;
            var storedSizeInBytes = processedMedia.OutputSizeInBytes ?? sizeInBytes;

            var media = new MediaEntity
            {
                FileName = storedFileName,
                ContentType = storedContentType,
                Type = mediaType,
                SizeInBytes = storedSizeInBytes,
                Width = processedMedia.Width,
                Height = processedMedia.Height,
                DurationSeconds = processedMedia.DurationSeconds,
                SortOrder = 0,
                UploadedByUserId = uploadedByUserId
            };

            media.StorageKey = GenerateObjectKey(uploadedByUserId, media.Id, storedFileName, mediaType);
            media.ThumbnailStorageKey = GenerateThumbnailObjectKey(uploadedByUserId, media, tempThumbnailFilePath);

            try
            {
                await using var uploadStream = File.OpenRead(uploadFilePath);
                await _objectStorageService.UploadAsync(
                    uploadStream,
                    media.StorageKey,
                    storedContentType,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(tempThumbnailFilePath) &&
                    !string.IsNullOrWhiteSpace(media.ThumbnailStorageKey))
                {
                    await using var thumbnailStream = File.OpenRead(tempThumbnailFilePath);
                    await _objectStorageService.UploadAsync(
                        thumbnailStream,
                        media.ThumbnailStorageKey,
                        "image/jpeg",
                        cancellationToken);
                }

                await _mediaRepository.AddAsync(media, cancellationToken);
            }
            catch
            {
                await TryDeleteObjectAsync(media.StorageKey, cancellationToken);
                await TryDeleteObjectAsync(media.ThumbnailStorageKey, cancellationToken);
                throw;
            }

            var mediaUrl = _objectStorageService.GetReadUrl(media.StorageKey);
            var responseType = ResolveResponseType(media.ContentType, media.Type);

            return new UploadMediaResponse
            {
                Id = media.Id,
                Type = responseType,
                Url = mediaUrl,
                ThumbnailUrl = GetThumbnailUrl(media, mediaUrl, responseType),
                Width = media.Width,
                Height = media.Height,
                Duration = media.DurationSeconds,
                MimeType = media.ContentType,
                StorageKey = media.StorageKey,
                FileName = media.FileName,
                SizeInBytes = media.SizeInBytes
            };
        }
        finally
        {
            TryDeleteLocalFile(tempSourceFilePath);
            if (!string.Equals(tempProcessedFilePath, tempSourceFilePath, StringComparison.Ordinal))
            {
                TryDeleteLocalFile(tempProcessedFilePath);
            }
            TryDeleteLocalFile(tempThumbnailFilePath);
        }
    }

    private static MediaType ResolveMediaType(string contentType)
    {
        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? MediaType.Image
            : MediaType.Video;
    }

    private static string ResolveStoredFileName(string originalFileName, string storedContentType)
    {
        var safeFileName = Path.GetFileName(originalFileName);

        if (storedContentType.Equals("video/mp4", StringComparison.OrdinalIgnoreCase))
        {
            return $"{Path.GetFileNameWithoutExtension(safeFileName)}.mp4";
        }

        return safeFileName;
    }

    private static void ValidateFileSize(long sizeInBytes, MediaType mediaType)
    {
        var maxSize = mediaType == MediaType.Image
            ? MaxImageSizeInBytes
            : MaxVideoSizeInBytes;

        if (sizeInBytes > maxSize)
        {
            throw new InvalidOperationException("File is too large.");
        }
    }

    private static string GenerateObjectKey(Guid uploadedByUserId, Guid mediaId, string fileName, MediaType mediaType)
    {
        var extension = Path.GetExtension(fileName);
        var category = mediaType == MediaType.Image ? "images" : "videos";

        return string.Join('/',
            "users",
            uploadedByUserId.ToString(),
            category,
            $"{mediaId}{extension.ToLowerInvariant()}");
    }

    private static string? GenerateThumbnailObjectKey(
        Guid uploadedByUserId,
        MediaEntity media,
        string? thumbnailFilePath)
    {
        if (string.IsNullOrWhiteSpace(thumbnailFilePath))
        {
            return null;
        }

        var extension = Path.GetExtension(thumbnailFilePath);

        return string.Join('/',
            "users",
            uploadedByUserId.ToString(),
            "video-thumbnails",
            $"{media.Id}{extension.ToLowerInvariant()}");
    }

    private async Task<string> SaveToTemporaryFileAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName);
        var normalizedExtension = string.IsNullOrWhiteSpace(extension)
            ? ".bin"
            : extension.ToLowerInvariant();
        var temporaryFilePath = Path.Combine(
            Path.GetTempPath(),
            $"threads-media-{Guid.NewGuid():N}{normalizedExtension}");

        await using var temporaryFileStream = File.Create(temporaryFilePath);
        await content.CopyToAsync(temporaryFileStream, cancellationToken);
        await temporaryFileStream.FlushAsync(cancellationToken);

        return temporaryFilePath;
    }

    private string? GetThumbnailUrl(MediaEntity media, string mediaUrl, string responseType)
    {
        if (!string.IsNullOrWhiteSpace(media.ThumbnailStorageKey))
        {
            return _objectStorageService.GetReadUrl(media.ThumbnailStorageKey);
        }

        return responseType is "image" or "gif"
            ? mediaUrl
            : null;
    }

    private static string ResolveResponseType(string contentType, MediaType mediaType)
    {
        if (contentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
        {
            return "gif";
        }

        return mediaType == MediaType.Video
            ? "video"
            : "image";
    }

    private async Task TryDeleteObjectAsync(string? objectKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return;
        }

        try
        {
            await _objectStorageService.DeleteAsync(objectKey, cancellationToken);
        }
        catch
        {
            // Best-effort cleanup after a failed metadata write.
        }
    }

    private static void TryDeleteLocalFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Best-effort cleanup for temporary files.
        }
    }
}
