using Microsoft.Extensions.Caching.Hybrid;
using Threads.Application.DTOs.Locations;
using Threads.Application.DTOs.Users;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Common;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Users;

public sealed class UserProfileService
{
    private const int MaxDisplayNameLength = 100;
    private const int MaxBioLength = 500;
    private const int MaxLocationLength = 255;
    private const int MaxLocationCountryLength = 255;
    private const int MaxLocationIdLength = 1024;

    private readonly IUserRepository _userRepository;
    private readonly ProfileImageManager _profileImageManager;
    private readonly UserResponseFactory _responseFactory;
    private readonly HybridCache _cache;

    public UserProfileService(
        IUserRepository userRepository,
        ProfileImageManager profileImageManager,
        UserResponseFactory responseFactory,
        HybridCache cache)
    {
        _userRepository = userRepository;
        _profileImageManager = profileImageManager;
        _responseFactory = responseFactory;
        _cache = cache;
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

        ApplyProfileChanges(user, request);

        try
        {
            if (avatar is not null)
            {
                uploadedAvatarObjectKey = await _profileImageManager.UploadAvatarAsync(
                    id,
                    avatar,
                    cancellationToken);
                user.AvatarObjectKey = uploadedAvatarObjectKey;
            }

            if (banner is not null)
            {
                uploadedBannerObjectKey = await _profileImageManager.UploadBannerAsync(
                    id,
                    banner,
                    cancellationToken);
                user.BannerObjectKey = uploadedBannerObjectKey;
            }

            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _userRepository.UpdateAsync(user, cancellationToken);
        }
        catch
        {
            await _profileImageManager.TryDeleteAsync(
                [uploadedAvatarObjectKey, uploadedBannerObjectKey],
                cancellationToken);
            throw;
        }

        await _profileImageManager.TryDeleteAsync(
            ProfileImageManager.GetReplacedObjectKeys(
                originalAvatarObjectKey,
                originalBannerObjectKey,
                user.AvatarObjectKey,
                user.BannerObjectKey),
            cancellationToken);

        await CacheInvalidation.TryRemoveAsync(_cache, UserProfileCache.GetProfileKey(id));

        return _responseFactory.Create(user, id);
    }

    private static void ApplyProfileChanges(User user, UpdateUserRequest request)
    {
        if (request.DisplayName is not null)
        {
            user.DisplayName = NormalizeOptionalText(
                request.DisplayName,
                MaxDisplayNameLength,
                "Display name");
        }

        if (request.Bio is not null)
        {
            user.Bio = NormalizeOptionalText(request.Bio, MaxBioLength, "Bio");
        }

        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new RequestValidationException("Date of birth cannot be in the future.");
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
            throw new RequestValidationException($"{fieldName} must be {maxLength} characters or less.");
        }

        return normalizedValue;
    }

    private static void ApplyLocation(User user, LocationRequest location)
    {
        if (string.IsNullOrWhiteSpace(location.Name))
        {
            throw new RequestValidationException("Location name is required.");
        }

        user.Location = NormalizeOptionalText(location.Name, MaxLocationLength, "Location");
        user.LocationPlaceId = NormalizeOptionalText(location.Id, MaxLocationIdLength, "Location id");
        user.LocationCountry = NormalizeOptionalText(
            location.Country,
            MaxLocationCountryLength,
            "Location country");
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
}
