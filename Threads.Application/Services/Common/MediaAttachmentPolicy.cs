using Threads.Application.Exceptions;

namespace Threads.Application.Services.Common;

public static class MediaAttachmentPolicy
{
    public const int MaximumCount = 4;

    public static void ValidateCount(IReadOnlyCollection<Guid> mediaIds)
    {
        if (mediaIds.Count > MaximumCount)
        {
            throw new RequestValidationException($"A post or comment can contain at most {MaximumCount} media items.");
        }
    }
}
