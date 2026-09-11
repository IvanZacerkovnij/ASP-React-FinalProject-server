using System.ComponentModel.DataAnnotations;

namespace Threads.Api.Requests.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MediaUploadFileAttribute : ValidationAttribute
{
    private const long MaxImageSizeInBytes = 10 * 1024 * 1024;
    private const long MaxVideoSizeInBytes = 100 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, long> MaxSizesByContentType =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = MaxImageSizeInBytes,
            ["image/png"] = MaxImageSizeInBytes,
            ["image/webp"] = MaxImageSizeInBytes,
            ["image/gif"] = MaxImageSizeInBytes,
            ["video/mp4"] = MaxVideoSizeInBytes,
            ["video/quicktime"] = MaxVideoSizeInBytes,
            ["video/webm"] = MaxVideoSizeInBytes
        };

    public MediaUploadFileAttribute()
        : base("The {0} field must be a supported, non-empty media file within the allowed size limit.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is IFormFile file &&
               file.Length > 0 &&
               !string.IsNullOrWhiteSpace(file.FileName) &&
               file.FileName.Length <= 255 &&
               MaxSizesByContentType.TryGetValue(file.ContentType, out var maxSizeInBytes) &&
               file.Length <= maxSizeInBytes;
    }
}
