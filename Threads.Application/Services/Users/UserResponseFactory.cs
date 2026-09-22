using AutoMapper;
using Threads.Application.DTOs.Locations;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Media;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Users;

public sealed class UserResponseFactory
{
    private readonly IObjectStorageService _objectStorageService;
    private readonly IMapper _mapper;

    public UserResponseFactory(
        IObjectStorageService objectStorageService,
        IMapper mapper)
    {
        _objectStorageService = objectStorageService;
        _mapper = mapper;
    }

    public UserShortResponse CreateShort(User user)
    {
        var response = _mapper.Map<UserShortResponse>(user);

        return new UserShortResponse
        {
            Id = response.Id,
            Username = response.Username,
            DisplayName = response.DisplayName,
            Location = CreateLocation(
                user.LocationPlaceId,
                user.Location,
                user.LocationCountry,
                user.LocationLatitude,
                user.LocationLongitude),
            AvatarUrl = GetReadUrl(user.AvatarObjectKey),
            IsVerified = response.IsVerified
        };
    }

    public UserShortResponse CreateShort(UserSummaryReadModel user)
    {
        return new UserShortResponse
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Location = CreateLocation(
                user.LocationPlaceId,
                user.LocationName,
                user.LocationCountry,
                user.LocationLatitude,
                user.LocationLongitude),
            AvatarUrl = GetReadUrl(user.AvatarObjectKey),
            IsVerified = user.IsVerified
        };
    }

    public UserResponse Create(User user, Guid? currentUserId)
    {
        var response = _mapper.Map<UserResponse>(user);

        return new UserResponse
        {
            Id = response.Id,
            Username = response.Username,
            Email = response.Email,
            DisplayName = response.DisplayName,
            Bio = response.Bio,
            DateOfBirth = response.DateOfBirth,
            Location = CreateLocation(
                user.LocationPlaceId,
                user.Location,
                user.LocationCountry,
                user.LocationLatitude,
                user.LocationLongitude),
            AvatarUrl = GetReadUrl(user.AvatarObjectKey),
            BannerUrl = GetReadUrl(user.BannerObjectKey),
            FollowersCount = response.FollowersCount,
            FollowingCount = response.FollowingCount,
            PostsCount = response.PostsCount,
            IsFollowedByCurrentUser = currentUserId.HasValue &&
                user.FollowerRelations.Any(relation => relation.FollowerId == currentUserId.Value),
            IsVerified = response.IsVerified,
            CreatedAt = response.CreatedAt
        };
    }

    public UserResponse Create(
        UserProfileReadModel publicProfile,
        bool isFollowedByCurrentUser)
    {
        return new UserResponse
        {
            Id = publicProfile.Id,
            Username = publicProfile.Username,
            Email = null,
            DisplayName = publicProfile.DisplayName,
            Bio = publicProfile.Bio,
            DateOfBirth = publicProfile.DateOfBirth,
            Location = CreateLocation(
                publicProfile.LocationPlaceId,
                publicProfile.LocationName,
                publicProfile.LocationCountry,
                publicProfile.LocationLatitude,
                publicProfile.LocationLongitude),
            AvatarUrl = GetReadUrl(publicProfile.AvatarObjectKey),
            BannerUrl = GetReadUrl(publicProfile.BannerObjectKey),
            FollowersCount = publicProfile.FollowersCount,
            FollowingCount = publicProfile.FollowingCount,
            PostsCount = publicProfile.PostsCount,
            IsFollowedByCurrentUser = isFollowedByCurrentUser,
            IsVerified = publicProfile.IsVerified,
            CreatedAt = publicProfile.CreatedAt
        };
    }

    private string? GetReadUrl(string? objectKey)
    {
        return string.IsNullOrWhiteSpace(objectKey)
            ? null
            : _objectStorageService.GetReadUrl(objectKey);
    }

    private static LocationResponse? CreateLocation(
        string? id,
        string? name,
        string? country,
        double? latitude,
        double? longitude)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new LocationResponse
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? name
                : id,
            Name = name,
            Country = string.IsNullOrWhiteSpace(country)
                ? string.Empty
                : country,
            Latitude = latitude ?? 0,
            Longitude = longitude ?? 0
        };
    }
}
