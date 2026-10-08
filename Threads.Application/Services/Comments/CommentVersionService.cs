using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Media;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Versions;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Media;
using Threads.Application.Services.Common;
using Threads.Domain.Enums;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.Services.Comments;

public sealed class CommentVersionService
{
    private readonly ICommentRepository _commentRepository;
    private readonly CommentVersionResponseFactory _responseFactory;
    private readonly IMediaRepository _mediaRepository;
    private readonly IObjectStorageService _objectStorageService;

    public CommentVersionService(
        ICommentRepository commentRepository,
        CommentVersionResponseFactory responseFactory,
        IMediaRepository mediaRepository,
        IObjectStorageService objectStorageService)
    {
        _commentRepository = commentRepository;
        _responseFactory = responseFactory;
        _mediaRepository = mediaRepository;
        _objectStorageService = objectStorageService;
    }

    public async Task<EditHistoryResponse<CommentResponse>?> GetEditHistoryAsync(
        Guid commentId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        var currentComments = await _commentRepository.GetSummariesByIdsAsync(
            [commentId],
            currentUserId,
            cancellationToken);
        var currentComment = currentComments.SingleOrDefault(comment => comment.Id == commentId);

        if (currentComment is null)
        {
            return null;
        }

        var cursor = CursorCodec.Decode(pagination.Cursor);
        var versions = await _commentRepository.GetVersionsAsync(
            commentId,
            pagination.Limit,
            cursor,
            cancellationToken);
        var hasMore = versions.Count > pagination.Limit;
        var pageVersions = versions.Take(pagination.Limit).ToList();
        var versionSnapshots = pageVersions
            .Select(version => (
                Version: version,
                Snapshot: _responseFactory.DeserializeSnapshot(version)))
            .ToList();
        var mediaIds = versionSnapshots
            .SelectMany(item => item.Snapshot.MediaIds)
            .Distinct()
            .ToArray();
        var mediaById = await GetMediaByIdAsync(mediaIds, cancellationToken);

        return new EditHistoryResponse<CommentResponse>
        {
            TargetType = ContentTargetType.Comment,
            TargetId = commentId,
            Versions = versionSnapshots
                .Select(item => _responseFactory.Create(
                    currentComment,
                    item.Version,
                    item.Snapshot,
                    mediaById))
                .ToList(),
            HasMore = hasMore,
            NextCursor = hasMore
                ? CursorCodec.Encode(pageVersions[^1].CreatedAt, pageVersions[^1].Id)
                : null
        };
    }

    private async Task<IReadOnlyDictionary<Guid, MediaAttachmentResponse>> GetMediaByIdAsync(
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken)
    {
        if (mediaIds.Count == 0)
        {
            return new Dictionary<Guid, MediaAttachmentResponse>();
        }

        var media = await _mediaRepository.GetByIdsAsync(mediaIds, cancellationToken);
        return media.ToDictionary(item => item.Id, CreateMediaResponse);
    }

    private MediaAttachmentResponse CreateMediaResponse(MediaEntity media)
    {
        var url = _objectStorageService.GetReadUrl(media.StorageKey);
        var type = media.ContentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase)
            ? "gif"
            : media.Type == MediaType.Video
                ? "video"
                : "image";

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
