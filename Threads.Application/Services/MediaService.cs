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
    private readonly IObjectStorageService _objectStorageService;

    public MediaService(IMediaRepository mediaRepository, IObjectStorageService objectStorageService)
    {
        _mediaRepository = mediaRepository;
        _objectStorageService = objectStorageService;
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

        var objectKey = GenerateObjectKey(uploadedByUserId, fileName, mediaType);

        await _objectStorageService.UploadAsync(content, objectKey, contentType, cancellationToken);

        var media = new MediaEntity
        {
            StorageKey = objectKey,
            FileName = Path.GetFileName(fileName),
            ContentType = contentType,
            Type = mediaType,
            SizeInBytes = sizeInBytes,
            SortOrder = 0,
            UploadedByUserId = uploadedByUserId
        };

        await _mediaRepository.AddAsync(media, cancellationToken);

        return new UploadMediaResponse
        {
            Id = media.Id,
            StorageKey = media.StorageKey,
            FileName = media.FileName,
            ContentType = media.ContentType,
            SizeInBytes = media.SizeInBytes,
            Type = media.Type.ToString()
        };
    }

    private static MediaType ResolveMediaType(string contentType)
    {
        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? MediaType.Image
            : MediaType.Video;
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

    private static string GenerateObjectKey(Guid uploadedByUserId, string fileName, MediaType mediaType)
    {
        var extension = Path.GetExtension(fileName);
        var category = mediaType == MediaType.Image ? "images" : "videos";

        return string.Join('/',
            "users",
            uploadedByUserId.ToString(),
            category,
            $"{Guid.NewGuid()}{extension.ToLowerInvariant()}");
    }
}
