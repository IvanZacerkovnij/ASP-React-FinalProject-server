using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Media;
using Threads.Domain.Entities;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.Services.Posts;

public sealed class PostMediaManager
{
    private readonly IMediaRepository _mediaRepository;
    private readonly IObjectStorageService _objectStorageService;

    public PostMediaManager(
        IMediaRepository mediaRepository,
        IObjectStorageService objectStorageService)
    {
        _mediaRepository = mediaRepository;
        _objectStorageService = objectStorageService;
    }

    public async Task ApplyAsync(
        Post post,
        Guid authorId,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken = default)
    {
        var distinctMediaIds = mediaIds
            .Distinct()
            .ToArray();

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

        if (media.Any(item => item.PostId.HasValue && item.PostId != post.Id))
        {
            throw new ConflictException("One or more media items are already attached to another post.");
        }

        foreach (var existingMedia in post.Media.Where(item => !distinctMediaIds.Contains(item.Id)).ToList())
        {
            existingMedia.PostId = null;
            existingMedia.SortOrder = 0;
        }

        post.Media.Clear();

        for (var index = 0; index < distinctMediaIds.Length; index++)
        {
            var currentMedia = media.Single(item => item.Id == distinctMediaIds[index]);
            currentMedia.PostId = post.Id;
            currentMedia.SortOrder = index;
            post.Media.Add(currentMedia);
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
            catch
            {
            }
        }
    }
}
