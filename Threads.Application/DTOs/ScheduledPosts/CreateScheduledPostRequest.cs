using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Validation;
using Threads.Application.Services.Common;

namespace Threads.Application.DTOs.ScheduledPosts;

public sealed class CreateScheduledPostRequest
{
    [StringLength(PostContentPolicy.MaximumLength, MinimumLength = PostContentPolicy.MinimumLength)]
    public string? Content { get; init; }

    [Required, MaxLength(MediaAttachmentPolicy.MaximumCount), UniqueNotEmptyGuids]
    public IReadOnlyCollection<Guid> MediaIds { get; init; } = [];

    public LinkPreviewRequest? LinkPreview { get; init; }

    [FutureDateTime]
    public DateTimeOffset ScheduledAt { get; init; }
}
