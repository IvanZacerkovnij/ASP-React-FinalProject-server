using AutoMapper;
using Microsoft.Extensions.Caching.Hybrid;
using Threads.Application.DTOs.Locations;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Follows;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Users;
using Threads.Domain.Entities;

namespace Threads.Application.Services;

public class UserService : IUserService
{
    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private const long MaxImageSizeInBytes = 10 * 1024 * 1024;
    private const int MaxUsernameLength = 50;
    private const int MaxDisplayNameLength = 100;
    private const int MaxBioLength = 500;
    private const int MaxLocationLength = 255;
    private const int MaxLocationCountryLength = 255;
    private const int MaxLocationIdLength = 1024;
    private readonly IMediaRepository _mediaRepository;
    private readonly IUserRepository _userRepository;
    private readonly IFollowRepository _followRepository;
    private readonly IObjectStorageService _objectStorageService;
    private readonly IMapper _mapper;
    private readonly HybridCache _cache;

    public UserService(
        IUserRepository userRepository,
        IFollowRepository followRepository,
        IMediaRepository mediaRepository,
        IObjectStorageService objectStorageService,
        IMapper mapper,
        HybridCache cache)
    {
        _userRepository = userRepository;
        _followRepository = followRepository;
        _mediaRepository = mediaRepository;
        _objectStorageService = objectStorageService;
        _mapper = mapper;
        _cache = cache;
    }

    public async Task<IReadOnlyCollection<UserShortResponse>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var users = await _userRepository.SearchAsync(query.Trim(), cancellationToken: cancellationToken);

        return users
            .Select(MapUserShortResponse)
            .ToList();
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

        return await AddCurrentUserStateAsync(publicProfile, currentUserId, cancellationToken);
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

    public async Task<UserResponse?> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        UserFileUploadRequest? avatar = null,
        UserFileUploadRequest? banner = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var originalAvatarObjectKey = user.AvatarObjectKey;
        var originalBannerObjectKey = user.BannerObjectKey;
        string? uploadedAvatarObjectKey = null;
        string? uploadedBannerObjectKey = null;

        if (request.DisplayName is not null)
        {
            user.DisplayName = NormalizeOptionalText(request.DisplayName, MaxDisplayNameLength, "Display name");
        }

        if (request.Bio is not null)
        {
            user.Bio = NormalizeOptionalText(request.Bio, MaxBioLength, "Bio");
        }

        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new InvalidOperationException("Date of birth cannot be in the future.");
        }

        if (request.RemoveDateOfBirth)
        {
            user.DateOfBirth = null;
        }
        else if (request.DateOfBirth.HasValue)
        {
            user.DateOfBirth = request.DateOfBirth;
        }

        if (request.RemoveLocation)
        {
            ClearLocation(user);
        }
        else if (request.Location is not null)
        {
            ApplyLocation(user, request.Location);
        }

        if (request.RemoveAvatar)
        {
            user.AvatarObjectKey = null;
        }

        if (request.RemoveBanner)
        {
            user.BannerObjectKey = null;
        }

        if (avatar is not null)
        {
            ValidateImageUpload(avatar, "Avatar");
            uploadedAvatarObjectKey = await UploadProfileImageAsync(id, "avatar", avatar, cancellationToken);
            user.AvatarObjectKey = uploadedAvatarObjectKey;
        }

        if (banner is not null)
        {
            ValidateImageUpload(banner, "Banner");
            uploadedBannerObjectKey = await UploadProfileImageAsync(id, "banner", banner, cancellationToken);
            user.BannerObjectKey = uploadedBannerObjectKey;
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _userRepository.UpdateAsync(user, cancellationToken);
        }
        catch
        {
            await TryDeleteObjectsAsync(
                [
                    uploadedAvatarObjectKey,
                    uploadedBannerObjectKey
                ],
                cancellationToken);

            throw;
        }

        await TryDeleteObjectsAsync(
            GetReplacedObjectKeys(
                originalAvatarObjectKey,
                originalBannerObjectKey,
                user.AvatarObjectKey,
                user.BannerObjectKey),
            cancellationToken);

        await CacheInvalidation.TryRemoveAsync(_cache, UserProfileCache.GetProfileKey(id));

        return MapUserResponse(user, id);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return false;
        }

        var uploadedMediaStorageKeys = await _mediaRepository.GetStorageKeysByUploaderIdAsync(id, cancellationToken);
        var affectedProfileCacheKeys = user.FollowingRelations
            .Select(relation => relation.FollowingId)
            .Concat(user.FollowerRelations.Select(relation => relation.FollowerId))
            .Append(id)
            .Distinct()
            .Select(UserProfileCache.GetProfileKey)
            .Append(UserProfileCache.GetUsernameKey(user.Username.ToLowerInvariant()))
            .Concat(user.Posts.Select(post => PostCache.GetKey(post.Id)))
            .ToArray();

        await _userRepository.DeleteAsync(user, cancellationToken);
        await CacheInvalidation.TryRemoveAsync(
            _cache,
            affectedProfileCacheKeys);
        await TryDeleteObjectsAsync(
            uploadedMediaStorageKeys
                .Concat(
                    [
                        user.AvatarObjectKey,
                        user.BannerObjectKey
                    ]),
            cancellationToken);

        return true;
    }

    private async Task<UserResponse> AddCurrentUserStateAsync(
        UserProfileReadModel publicProfile,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var isFollowedByCurrentUser = false;

        if (currentUserId.HasValue && currentUserId.Value != publicProfile.Id)
        {
            isFollowedByCurrentUser = await _followRepository.GetByFollowerAndFollowingAsync(
                currentUserId.Value,
                publicProfile.Id,
                cancellationToken) is not null;
        }

        return new UserResponse
        {
            Id = publicProfile.Id,
            Username = publicProfile.Username,
            DisplayName = publicProfile.DisplayName,
            Bio = publicProfile.Bio,
            DateOfBirth = publicProfile.DateOfBirth,
            Location = MapLocation(
                publicProfile.LocationPlaceId,
                publicProfile.LocationName,
                publicProfile.LocationCountry,
                publicProfile.LocationLatitude,
                publicProfile.LocationLongitude),
            AvatarUrl = string.IsNullOrWhiteSpace(publicProfile.AvatarObjectKey)
                ? null
                : _objectStorageService.GetReadUrl(publicProfile.AvatarObjectKey),
            BannerUrl = string.IsNullOrWhiteSpace(publicProfile.BannerObjectKey)
                ? null
                : _objectStorageService.GetReadUrl(publicProfile.BannerObjectKey),
            FollowersCount = publicProfile.FollowersCount,
            FollowingCount = publicProfile.FollowingCount,
            PostsCount = publicProfile.PostsCount,
            IsFollowedByCurrentUser = isFollowedByCurrentUser,
            IsVerified = publicProfile.IsVerified,
            CreatedAt = publicProfile.CreatedAt.UtcDateTime
        };
    }

    private async Task<string> UploadProfileImageAsync(
        Guid userId,
        string kind,
        UserFileUploadRequest file,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName);
        var safeExtension = string.IsNullOrWhiteSpace(extension)
            ? ".bin"
            : extension.ToLowerInvariant();
        var objectKey = $"users/{userId}/profile/{kind}-{Guid.NewGuid():N}{safeExtension}";

        await _objectStorageService.UploadAsync(file.Content, objectKey, file.ContentType, cancellationToken);

        return objectKey;
    }

    private static void ValidateImageUpload(UserFileUploadRequest file, string fieldName)
    {
        if (file.SizeInBytes <= 0)
        {
            throw new InvalidOperationException($"{fieldName} file must not be empty.");
        }

        if (file.SizeInBytes > MaxImageSizeInBytes)
        {
            throw new InvalidOperationException($"{fieldName} file is too large.");
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedImageContentTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException($"{fieldName} content type is not supported.");
        }
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > maxLength)
        {
            throw new InvalidOperationException($"{fieldName} must be {maxLength} characters or less.");
        }

        return normalizedValue;
    }

    private static void ApplyLocation(User user, LocationRequest location)
    {
        if (string.IsNullOrWhiteSpace(location.Name))
        {
            throw new InvalidOperationException("Location name is required.");
        }

        user.Location = NormalizeOptionalText(location.Name, MaxLocationLength, "Location");
        user.LocationPlaceId = NormalizeOptionalText(location.Id, MaxLocationIdLength, "Location id");
        user.LocationCountry = NormalizeOptionalText(location.Country, MaxLocationCountryLength, "Location country");
        user.LocationLatitude = location.Latitude;
        user.LocationLongitude = location.Longitude;
    }

    private static void ClearLocation(User user)
    {
        user.Location = null;
        user.LocationPlaceId = null;
        user.LocationCountry = null;
        user.LocationLatitude = null;
        user.LocationLongitude = null;
    }

    private static LocationResponse? MapLocation(
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

    private static IReadOnlyCollection<string?> GetReplacedObjectKeys(
        string? originalAvatarObjectKey,
        string? originalBannerObjectKey,
        string? currentAvatarObjectKey,
        string? currentBannerObjectKey)
    {
        var objectKeysToDelete = new List<string?>();

        if (!string.Equals(originalAvatarObjectKey, currentAvatarObjectKey, StringComparison.Ordinal))
        {
            objectKeysToDelete.Add(originalAvatarObjectKey);
        }

        if (!string.Equals(originalBannerObjectKey, currentBannerObjectKey, StringComparison.Ordinal))
        {
            objectKeysToDelete.Add(originalBannerObjectKey);
        }

        return objectKeysToDelete;
    }

    private async Task TryDeleteObjectsAsync(
        IEnumerable<string?> objectKeys,
        CancellationToken cancellationToken)
    {
        foreach (var objectKey in objectKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct())
        {
            try
            {
                await _objectStorageService.DeleteAsync(objectKey!, cancellationToken);
            }
            catch
            {
                // Best-effort cleanup after DB state is already persisted.
            }
        }
    }

    private UserShortResponse MapUserShortResponse(User user)
    {
        var response = _mapper.Map<UserShortResponse>(user);

        return new UserShortResponse
        {
            Id = response.Id,
            Username = response.Username,
            DisplayName = response.DisplayName,
            Location = MapLocation(
                user.LocationPlaceId,
                user.Location,
                user.LocationCountry,
                user.LocationLatitude,
                user.LocationLongitude),
            AvatarUrl = string.IsNullOrWhiteSpace(user.AvatarObjectKey)
                ? null
                : _objectStorageService.GetReadUrl(user.AvatarObjectKey),
            IsVerified = response.IsVerified
        };
    }

    private UserResponse MapUserResponse(User user, Guid? currentUserId)
    {
        var response = _mapper.Map<UserResponse>(user);

        return new UserResponse
        {
            Id = response.Id,
            Username = response.Username,
            DisplayName = response.DisplayName,
            Bio = response.Bio,
            DateOfBirth = response.DateOfBirth,
            Location = MapLocation(
                user.LocationPlaceId,
                user.Location,
                user.LocationCountry,
                user.LocationLatitude,
                user.LocationLongitude),
            AvatarUrl = string.IsNullOrWhiteSpace(user.AvatarObjectKey)
                ? null
                : _objectStorageService.GetReadUrl(user.AvatarObjectKey),
            BannerUrl = string.IsNullOrWhiteSpace(user.BannerObjectKey)
                ? null
                : _objectStorageService.GetReadUrl(user.BannerObjectKey),
            FollowersCount = response.FollowersCount,
            FollowingCount = response.FollowingCount,
            PostsCount = response.PostsCount,
            IsFollowedByCurrentUser = currentUserId.HasValue &&
                user.FollowerRelations.Any(relation => relation.FollowerId == currentUserId.Value),
            IsVerified = response.IsVerified,
            CreatedAt = response.CreatedAt
        };
    }
}
