using AutoMapper;
using Threads.Application.DTOs.Locations;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Follows;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Users;
using Threads.Domain.Entities;

namespace Threads.Application.Services;

public class FollowService : IFollowService
{
    private readonly IFollowRepository _followRepository;
    private readonly IUserRepository _userRepository;
    private readonly IObjectStorageService _objectStorageService;
    private readonly IMapper _mapper;

    public FollowService(
        IFollowRepository followRepository,
        IUserRepository userRepository,
        IObjectStorageService objectStorageService,
        IMapper mapper)
    {
        _followRepository = followRepository;
        _userRepository = userRepository;
        _objectStorageService = objectStorageService;
        _mapper = mapper;
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

        return true;
    }

    public async Task<IReadOnlyCollection<UserShortResponse>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var followers = await _followRepository.GetFollowersAsync(userId, cancellationToken);

        return followers
            .Select(MapUserShortResponse)
            .ToList();
    }

    public async Task<IReadOnlyCollection<UserShortResponse>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var following = await _followRepository.GetFollowingAsync(userId, cancellationToken);

        return following
            .Select(MapUserShortResponse)
            .ToList();
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
