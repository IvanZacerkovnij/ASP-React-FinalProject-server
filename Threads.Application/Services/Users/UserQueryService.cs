using Microsoft.Extensions.Caching.Hybrid;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Follows;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Common;

namespace Threads.Application.Services.Users;

public sealed class UserQueryService
{
    private const int MaxUsernameLength = 50;

    private readonly IUserRepository _userRepository;
    private readonly IFollowRepository _followRepository;
    private readonly UserResponseFactory _responseFactory;
    private readonly HybridCache _cache;

    public UserQueryService(
        IUserRepository userRepository,
        IFollowRepository followRepository,
        UserResponseFactory responseFactory,
        HybridCache cache)
    {
        _userRepository = userRepository;
        _followRepository = followRepository;
        _responseFactory = responseFactory;
        _cache = cache;
    }

    public async Task<CursorPageResponse<UserShortResponse>> SearchAsync(
        string query,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new CursorPageResponse<UserShortResponse>
            {
                Items = [],
                HasMore = false,
                NextCursor = null
            };
        }

        var cursor = CursorCodec.DecodeText(pagination.Cursor);
        var users = await _userRepository.SearchAsync(
            query.Trim(),
            pagination.Limit,
            cursor,
            cancellationToken);
        var hasMore = users.Count > pagination.Limit;
        var pageUsers = users.Take(pagination.Limit).ToList();

        return new CursorPageResponse<UserShortResponse>
        {
            Items = pageUsers
                .Select(_responseFactory.CreateShort)
                .ToList(),
            HasMore = hasMore,
            NextCursor = hasMore
                ? CursorCodec.EncodeText(pageUsers[^1].Username, pageUsers[^1].Id)
                : null
        };
    }

    public async Task<UserResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var cacheKey = UserProfileCache.GetProfileKey(id);
        var publicProfile = await _cache.GetOrCreateAsync<UserProfileReadModel?>(
            cacheKey,
            async token => await _userRepository.GetProfileByIdAsync(id, token),
            UserProfileCache.ProfileEntryOptions,
            cancellationToken: cancellationToken);

        if (publicProfile is null)
        {
            await CacheInvalidation.TryRemoveAsync(_cache, cacheKey);
            return null;
        }

        var isFollowedByCurrentUser = false;

        if (currentUserId.HasValue && currentUserId.Value != publicProfile.Id)
        {
            isFollowedByCurrentUser = await _followRepository.GetByFollowerAndFollowingAsync(
                currentUserId.Value,
                publicProfile.Id,
                cancellationToken) is not null;
        }

        return _responseFactory.Create(publicProfile, isFollowedByCurrentUser);
    }

    public async Task<UserResponse?> GetMeAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        return user is null
            ? null
            : _responseFactory.Create(user, id);
    }

    public async Task<UserResponse?> GetByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var normalizedUsername = username.Trim().ToLowerInvariant();

        if (normalizedUsername.Length > MaxUsernameLength)
        {
            return null;
        }

        var cacheKey = UserProfileCache.GetUsernameKey(normalizedUsername);
        var userId = await _cache.GetOrCreateAsync<Guid?>(
            cacheKey,
            async token => await _userRepository.GetIdByUsernameAsync(normalizedUsername, token),
            UserProfileCache.UsernameEntryOptions,
            cancellationToken: cancellationToken);

        if (!userId.HasValue)
        {
            await CacheInvalidation.TryRemoveAsync(_cache, cacheKey);
            return null;
        }

        var user = await GetByIdAsync(userId.Value, cancellationToken, currentUserId);

        if (user is null)
        {
            await CacheInvalidation.TryRemoveAsync(_cache, cacheKey);
        }

        return user;
    }
}
