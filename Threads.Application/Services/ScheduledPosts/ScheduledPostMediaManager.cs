using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Media;
using Threads.Domain.Entities;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.Services.ScheduledPosts;

public sealed class ScheduledPostMediaManager
{
    private readonly IMediaRepository _mediaRepository;

    public ScheduledPostMediaManager(IMediaRepository mediaRepository)
    {
        _mediaRepository = mediaRepository;
    }

    public async Task ApplyAsync(
        ScheduledPost scheduledPost,
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
                              item.CommentId.HasValue ||
                              item.ScheduledPostId.HasValue && item.ScheduledPostId != scheduledPost.Id))
        {
            throw new ConflictException("One or more media items are already attached to another content item.");
        }

        foreach (var existingMedia in scheduledPost.Media
                     .Where(item => !distinctMediaIds.Contains(item.Id))
                     .ToList())
        {
            existingMedia.ScheduledPostId = null;
            existingMedia.SortOrder = 0;
        }

        scheduledPost.Media.Clear();

        for (var index = 0; index < distinctMediaIds.Length; index++)
        {
            var currentMedia = media.Single(item => item.Id == distinctMediaIds[index]);
            currentMedia.ScheduledPostId = scheduledPost.Id;
            currentMedia.SortOrder = index;
            scheduledPost.Media.Add(currentMedia);
        }
    }
}
