using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Validation;

namespace Threads.Application.DTOs.ScheduledPosts;

public sealed class CreateScheduledPostRequest
{
    [StringLength(2000, MinimumLength = 1)]
    public string? Content { get; init; }

    [Required, MaxLength(20), UniqueNotEmptyGuids]
    public IReadOnlyCollection<Guid> MediaIds { get; init; } = [];

    public LinkPreviewRequest? LinkPreview { get; init; }

    [FutureDateTime]
    public DateTimeOffset ScheduledAt { get; init; }
}
