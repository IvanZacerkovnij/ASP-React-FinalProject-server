using Threads.Application.DTOs.Users;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Media;

namespace Threads.Application.Services.Users;

public sealed class ProfileImageManager
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private const long MaxImageSizeInBytes = 10 * 1024 * 1024;

    private readonly IObjectStorageService _objectStorageService;

    public ProfileImageManager(IObjectStorageService objectStorageService)
    {
        _objectStorageService = objectStorageService;
    }

    public Task<string> UploadAvatarAsync(
        Guid userId,
        UserFileUploadRequest file,
        CancellationToken cancellationToken = default)
    {
        return UploadAsync(userId, "avatar", "Avatar", file, cancellationToken);
    }

    public Task<string> UploadBannerAsync(
        Guid userId,
        UserFileUploadRequest file,
        CancellationToken cancellationToken = default)
    {
        return UploadAsync(userId, "banner", "Banner", file, cancellationToken);
    }

    private async Task<string> UploadAsync(
        Guid userId,
        string kind,
        string fieldName,
        UserFileUploadRequest file,
        CancellationToken cancellationToken)
    {
        Validate(file, fieldName);

        var extension = Path.GetExtension(file.FileName);
        var safeExtension = string.IsNullOrWhiteSpace(extension)
            ? ".bin"
            : extension.ToLowerInvariant();
        var objectKey = $"users/{userId}/profile/{kind}-{Guid.NewGuid():N}{safeExtension}";

        await _objectStorageService.UploadAsync(
            file.Content,
            objectKey,
            file.ContentType,
            cancellationToken);

        return objectKey;
    }

    public async Task TryDeleteAsync(
        IEnumerable<string?> objectKeys,
        CancellationToken cancellationToken = default)
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

    public static IReadOnlyCollection<string?> GetReplacedObjectKeys(
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

    private static void Validate(UserFileUploadRequest file, string fieldName)
    {
        if (file.SizeInBytes <= 0)
        {
            throw new RequestValidationException($"{fieldName} file must not be empty.");
        }

        if (file.SizeInBytes > MaxImageSizeInBytes)
        {
            throw new RequestValidationException($"{fieldName} file is too large.");
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType))
        {
            throw new RequestValidationException($"{fieldName} content type is not supported.");
        }
    }
}
