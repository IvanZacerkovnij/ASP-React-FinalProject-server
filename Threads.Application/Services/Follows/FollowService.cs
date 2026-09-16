using AutoMapper;
using Microsoft.Extensions.Caching.Hybrid;
using Threads.Application.DTOs.Locations;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Users;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Follows;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Common;
using Threads.Application.Services.Users;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Follows;

public class FollowService : IFollowService
{
    private readonly IFollowRepository _followRepository;
    private readonly IUserRepository _userRepository;
    private readonly IObjectStorageService _objectStorageService;
    private readonly IMapper _mapper;
    private readonly HybridCache _cache;

    public FollowService(
        IFollowRepository followRepository,
        IUserRepository userRepository,
        IObjectStorageService objectStorageService,
        IMapper mapper,
        HybridCache cache)
    {
        _followRepository = followRepository;
        _userRepository = userRepository;
        _objectStorageService = objectStorageService;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<bool> AddFollowAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default)
    {
        if (followerId == followingId)
        {
            return false;
        }

        var follower = await _userRepository.GetByIdAsync(followerId, cancellationToken);
        var following = await _userRepository.GetByIdAsync(followingId, cancellationToken);

        if (follower is null || following is null)
        {
            return false;
        }

        var existingFollow = await _followRepository.GetByFollowerAndFollowingAsync(
            followerId,
            followingId,
            cancellationToken);

        if (existingFollow is not null)
        {
            return false;
        }

        var follow = new Follow
        {
            FollowerId = followerId,
            FollowingId = followingId
        };

        try
        {
            await _followRepository.AddAsync(follow, cancellationToken);
        }
        catch (Exception exception) when (IsDuplicateWriteException(exception))
        {
            return false;
        }

        await InvalidateProfileCacheAsync(followerId, followingId);
        return true;
    }

    public async Task<bool> RemoveFollowAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default)
    {
        var existingFollow = await _followRepository.GetByFollowerAndFollowingAsync(
            followerId,
            followingId,
            cancellationToken);

        if (existingFollow is null)
        {
            return false;
        }

        await _followRepository.DeleteAsync(existingFollow, cancellationToken);

        await InvalidateProfileCacheAsync(followerId, followingId);
        return true;
    }

    public async Task RemoveFollowerAsync(
        Guid currentUserId,
        Guid userId,
        Guid followerId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId != userId)
        {
            throw new ForbiddenException("You cannot remove followers from another user.");
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("User was not found.");
        }

        var follower = await _userRepository.GetByIdAsync(followerId, cancellationToken);

        if (follower is null)
        {
            throw new NotFoundException("Follower was not found.");
        }

        var wasRemoved = await RemoveFollowAsync(followerId, userId, cancellationToken);

        if (!wasRemoved)
        {
            throw new NotFoundException("Follow was not found.");
        }
    }

    private async Task InvalidateProfileCacheAsync(Guid followerId, Guid followingId)
    {
        await CacheInvalidation.TryRemoveAsync(
            _cache,
            UserProfileCache.GetProfileKey(followerId),
            UserProfileCache.GetProfileKey(followingId));
    }

    public async Task<CursorPageResponse<UserShortResponse>> GetFollowersAsync(
        Guid userId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var cursor = CursorCodec.Decode(pagination.Cursor);
        var follows = await _followRepository.GetFollowersAsync(
            userId,
            pagination.Limit,
            cursor,
            cancellationToken);

        return MapFollowPage(follows, pagination.Limit, follow => follow.Follower);
    }

    public async Task<CursorPageResponse<UserShortResponse>> GetFollowingAsync(
        Guid userId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var cursor = CursorCodec.Decode(pagination.Cursor);
        var follows = await _followRepository.GetFollowingAsync(
            userId,
            pagination.Limit,
            cursor,
            cancellationToken);

        return MapFollowPage(follows, pagination.Limit, follow => follow.Following);
    }

    private CursorPageResponse<UserShortResponse> MapFollowPage(
        IReadOnlyCollection<Follow> follows,
        int limit,
        Func<Follow, User> selectUser)
    {
        var hasMore = follows.Count > limit;
        var pageFollows = follows.Take(limit).ToList();

        return new CursorPageResponse<UserShortResponse>
        {
            Items = pageFollows
                .Select(follow => MapUserShortResponse(selectUser(follow)))
                .ToList(),
            HasMore = hasMore,
            NextCursor = hasMore
                ? CursorCodec.Encode(pageFollows[^1].CreatedAt, pageFollows[^1].Id)
                : null
        };
    }

    private UserShortResponse MapUserShortResponse(User user)
    {
        var response = _mapper.Map<UserShortResponse>(user);

        return new UserShortResponse
        {
            Id = response.Id,
            Username = response.Username,
            DisplayName = response.DisplayName,
            Location = MapLocation(user),
            AvatarUrl = string.IsNullOrWhiteSpace(user.AvatarObjectKey)
                ? null
                : _objectStorageService.GetReadUrl(user.AvatarObjectKey),
            IsVerified = response.IsVerified
        };
    }

    private static LocationResponse? MapLocation(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Location))
        {
            return null;
        }

        return new LocationResponse
        {
            Id = string.IsNullOrWhiteSpace(user.LocationPlaceId)
                ? user.Location
                : user.LocationPlaceId,
            Name = user.Location,
            Country = user.LocationCountry ?? string.Empty,
            Latitude = user.LocationLatitude ?? 0,
            Longitude = user.LocationLongitude ?? 0
        };
    }

    private static bool IsDuplicateWriteException(Exception exception)
    {
        return exception.GetType().Name == "DbUpdateException";
    }
}
