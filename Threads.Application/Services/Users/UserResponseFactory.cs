using AutoMapper;
using Threads.Application.DTOs.Locations;
using Threads.Application.DTOs.Users;
using Threads.Application.DTOs.Admin;
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
            Bio = response.Bio,
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
            Bio = user.Bio,
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
            BirthDate = user.DateOfBirth,
            BirthDateVisibility = user.BirthDateVisibility,
            BirthYearVisibility = user.BirthYearVisibility,
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
            Role = UserRoleContract.Serialize(user.Role),
            CreatedAt = response.CreatedAt
        };
    }

    public UserResponse Create(
        UserProfileReadModel publicProfile,
        bool isFollowedByCurrentUser,
        bool canSeeBirthDate)
    {
        return Create(publicProfile, isFollowedByCurrentUser, canSeeBirthDate, email: null);
    }

    public UserResponse CreateCurrent(User user, UserProfileReadModel profile)
    {
        return Create(
            profile,
            isFollowedByCurrentUser: false,
            canSeeBirthDate: true,
            email: user.Email,
            role: UserRoleContract.Serialize(user.Role));
    }

    public AdminUserResponse CreateAdmin(User user)
    {
        var response = Create(user, currentUserId: null);

        return new AdminUserResponse
        {
            Id = response.Id,
            Username = response.Username,
            Email = response.Email,
            DisplayName = response.DisplayName,
            Bio = response.Bio,
            BirthDate = response.BirthDate,
            BirthDateVisibility = response.BirthDateVisibility,
            BirthYearVisibility = response.BirthYearVisibility,
            Location = response.Location,
            AvatarUrl = response.AvatarUrl,
            BannerUrl = response.BannerUrl,
            FollowersCount = response.FollowersCount,
            FollowingCount = response.FollowingCount,
            PostsCount = response.PostsCount,
            IsFollowedByCurrentUser = false,
            IsVerified = response.IsVerified,
            Role = response.Role,
            CreatedAt = response.CreatedAt,
            IsBlocked = !user.IsActive
        };
    }

    public UserResponse CreatePublic(UserProfileReadModel profile)
    {
        return Create(profile, isFollowedByCurrentUser: false, canSeeBirthDate: false, email: null);
    }

    private UserResponse Create(
        UserProfileReadModel publicProfile,
        bool isFollowedByCurrentUser,
        bool canSeeBirthDate,
        string? email,
        string role = UserRoleContract.User)
    {
        return new UserResponse
        {
            Id = publicProfile.Id,
            Username = publicProfile.Username,
            Email = email,
            DisplayName = publicProfile.DisplayName,
            Bio = publicProfile.Bio,
            BirthDate = canSeeBirthDate
                ? publicProfile.DateOfBirth
                : null,
            BirthDateVisibility = publicProfile.BirthDateVisibility,
            BirthYearVisibility = publicProfile.BirthYearVisibility,
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
            Role = role,
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
