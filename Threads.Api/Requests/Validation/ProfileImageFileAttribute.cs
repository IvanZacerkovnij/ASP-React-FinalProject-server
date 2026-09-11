using System.ComponentModel.DataAnnotations;

namespace Threads.Api.Requests.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ProfileImageFileAttribute : ValidationAttribute
{
    private const long MaxImageSizeInBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public ProfileImageFileAttribute()
        : base("The {0} field must be a supported, non-empty image of at most 10 MB.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is IFormFile file &&
               file.Length is > 0 and <= MaxImageSizeInBytes &&
               !string.IsNullOrWhiteSpace(file.FileName) &&
               file.FileName.Length <= 255 &&
               AllowedContentTypes.Contains(file.ContentType);
    }
}
