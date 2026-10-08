using Threads.Application.DTOs.Media;
using Threads.Application.DTOs.ScheduledPosts;
using Threads.Application.Interfaces.Media;
using Threads.Application.Services.LinkPreviews;
using Threads.Domain.Entities;
using Threads.Domain.Enums;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.Services.ScheduledPosts;

public sealed class ScheduledPostResponseFactory
{
    private readonly IObjectStorageService _objectStorageService;

    public ScheduledPostResponseFactory(IObjectStorageService objectStorageService)
    {
        _objectStorageService = objectStorageService;
    }

    public ScheduledPostResponse Create(ScheduledPost scheduledPost)
    {
        var media = scheduledPost.Media
            .OrderBy(item => item.SortOrder)
            .Select(CreateMediaResponse)
            .ToList();

        return new ScheduledPostResponse
        {
            Id = scheduledPost.Id,
            Content = scheduledPost.Content,
            MediaIds = media.Select(item => item.Id).ToList(),
            Media = media,
            LinkPreview = string.IsNullOrWhiteSpace(scheduledPost.LinkPreviewUrl)
                ? null
                : LinkPreviewResponseFactory.Create(
                    scheduledPost.LinkPreviewUrl,
                    scheduledPost.LinkPreviewTitle,
                    scheduledPost.LinkPreviewImageUrl),
            ScheduledAt = scheduledPost.ScheduledAt,
            CreatedAt = scheduledPost.CreatedAt,
            UpdatedAt = scheduledPost.UpdatedAt
        };
    }

    private MediaAttachmentResponse CreateMediaResponse(MediaEntity media)
    {
        var type = media.ContentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase)
            ? "gif"
            : media.Type == MediaType.Video
                ? "video"
                : "image";
        var url = _objectStorageService.GetReadUrl(media.StorageKey);

        return new MediaAttachmentResponse
        {
            Id = media.Id,
            Type = type,
            Url = url,
            ThumbnailUrl = !string.IsNullOrWhiteSpace(media.ThumbnailStorageKey)
                ? _objectStorageService.GetReadUrl(media.ThumbnailStorageKey)
                : type is "image" or "gif"
                    ? url
                    : null,
            Width = media.Width,
            Height = media.Height,
            Duration = media.DurationSeconds,
            MimeType = media.ContentType,
            FileName = media.FileName,
            SizeInBytes = media.SizeInBytes,
            SortOrder = media.SortOrder
        };
    }
}
