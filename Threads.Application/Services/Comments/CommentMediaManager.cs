using Microsoft.Extensions.Logging;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Media;
using Threads.Domain.Entities;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.Services.Comments;

public sealed class CommentMediaManager
{
    private readonly IMediaRepository _mediaRepository;
    private readonly IObjectStorageService _objectStorageService;
    private readonly ILogger<CommentMediaManager> _logger;

    public CommentMediaManager(
        IMediaRepository mediaRepository,
        IObjectStorageService objectStorageService,
        ILogger<CommentMediaManager> logger)
    {
        _mediaRepository = mediaRepository;
        _objectStorageService = objectStorageService;
        _logger = logger;
    }

    public async Task ApplyAsync(
        Comment comment,
        Guid authorId,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        var distinctMediaIds = mediaIds.Distinct().ToArray();

        if (distinctMediaIds.Length != mediaIds.Count)
        {
            throw new RequestValidationException("Media ids must be unique.");
        }

        IReadOnlyCollection<MediaEntity> media = distinctMediaIds.Length == 0
            ? Array.Empty<MediaEntity>()
            : await _mediaRepository.GetByIdsAsync(distinctMediaIds, cancellationToken);

        if (media.Count != distinctMediaIds.Length)
        {
            throw new NotFoundException("One or more media items were not found.");
        }

        if (media.Any(item => item.UploadedByUserId != authorId))
        {
            throw new ForbiddenException("One or more media items do not belong to the current user.");
        }

        if (media.Any(item => item.PostId.HasValue ||
                              item.ScheduledPostId.HasValue ||
                              item.CommentId.HasValue && item.CommentId != comment.Id))
        {
            throw new ConflictException("One or more media items are already attached to another content item.");
        }

        foreach (var existingMedia in comment.Media.Where(item => !distinctMediaIds.Contains(item.Id)).ToList())
        {
            existingMedia.CommentId = null;
            existingMedia.SortOrder = 0;
        }

        comment.Media.Clear();

        for (var index = 0; index < distinctMediaIds.Length; index++)
        {
            var currentMedia = media.Single(item => item.Id == distinctMediaIds[index]);
            currentMedia.CommentId = comment.Id;
            currentMedia.SortOrder = index;
            comment.Media.Add(currentMedia);
        }
    }

    public async Task TryDeleteAsync(
        IEnumerable<string> storageKeys,
        CancellationToken cancellationToken = default)
    {
        foreach (var storageKey in storageKeys)
        {
            try
            {
                await _objectStorageService.DeleteAsync(storageKey, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to delete comment media object {ObjectKey}",
                    storageKey);
            }
        }
    }
}
