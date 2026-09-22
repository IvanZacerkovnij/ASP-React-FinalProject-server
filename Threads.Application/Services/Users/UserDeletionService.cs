using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Common;
using Threads.Application.Services.Posts;

namespace Threads.Application.Services.Users;

public sealed class UserDeletionService
{
    private readonly IUserRepository _userRepository;
    private readonly IMediaRepository _mediaRepository;
    private readonly ProfileImageManager _profileImageManager;
    private readonly HybridCache _cache;
    private readonly ILogger<UserDeletionService> _logger;

    public UserDeletionService(
        IUserRepository userRepository,
        IMediaRepository mediaRepository,
        ProfileImageManager profileImageManager,
        HybridCache cache,
        ILogger<UserDeletionService> logger)
    {
        _userRepository = userRepository;
        _mediaRepository = mediaRepository;
        _profileImageManager = profileImageManager;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return false;
        }

        var uploadedMediaStorageKeys = await _mediaRepository.GetStorageKeysByUploaderIdAsync(
            id,
            cancellationToken);
        var affectedCacheKeys = user.FollowingRelations
            .Select(relation => relation.FollowingId)
            .Concat(user.FollowerRelations.Select(relation => relation.FollowerId))
            .Append(id)
            .Distinct()
            .Select(UserProfileCache.GetProfileKey)
            .Append(UserProfileCache.GetUsernameKey(user.Username.ToLowerInvariant()))
            .Concat(user.Posts.Select(post => PostCache.GetKey(post.Id)))
            .ToArray();

        await _userRepository.DeleteAsync(user, cancellationToken);
        await CacheInvalidation.TryRemoveAsync(_cache, _logger, affectedCacheKeys);
        await _profileImageManager.TryDeleteAsync(
            uploadedMediaStorageKeys.Concat([user.AvatarObjectKey, user.BannerObjectKey]),
            cancellationToken);

        _logger.LogInformation(
            "User {UserId} database data deleted and storage cleanup attempted",
            id);

        return true;
    }
}
