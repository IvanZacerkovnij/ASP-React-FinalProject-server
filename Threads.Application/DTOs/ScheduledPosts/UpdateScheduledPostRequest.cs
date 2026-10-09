using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Validation;
using Threads.Application.Services.Common;

namespace Threads.Application.DTOs.ScheduledPosts;

[JsonConverter(typeof(UpdateScheduledPostRequestConverter))]
public sealed class UpdateScheduledPostRequest
{
    [StringLength(PostContentPolicy.MaximumLength, MinimumLength = PostContentPolicy.MinimumLength)]
    public string? Content { get; init; }

    [JsonIgnore]
    public bool HasContentValue { get; init; }

    [MaxLength(MediaAttachmentPolicy.MaximumCount), UniqueNotEmptyGuids]
    public IReadOnlyCollection<Guid>? MediaIds { get; init; }

    public LinkPreviewRequest? LinkPreview { get; init; }

    [JsonIgnore]
    public bool HasLinkPreviewValue { get; init; }

    [FutureDateTime]
    public DateTimeOffset? ScheduledAt { get; init; }

    [JsonIgnore]
    public bool HasScheduledAtValue { get; init; }
}
