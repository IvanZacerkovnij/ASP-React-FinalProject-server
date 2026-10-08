using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Media;

namespace Threads.Application.DTOs.ScheduledPosts;

public sealed class ScheduledPostResponse
{
    public Guid Id { get; init; }

    public string? Content { get; init; }

    public IReadOnlyCollection<Guid> MediaIds { get; init; } = [];

    public IReadOnlyCollection<MediaAttachmentResponse> Media { get; init; } = [];

    public LinkPreviewResponse? LinkPreview { get; init; }

    public DateTimeOffset ScheduledAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
