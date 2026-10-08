using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Validation;

namespace Threads.Application.DTOs.ScheduledPosts;

public sealed class UpdateScheduledPostRequest
{
    private string? _content;

    [StringLength(2000, MinimumLength = 1)]
    public string? Content
    {
        get => _content;
        init
        {
            _content = value;
            HasContentValue = true;
        }
    }

    [JsonIgnore]
    public bool HasContentValue { get; private init; }

    [MaxLength(20), UniqueNotEmptyGuids]
    public IReadOnlyCollection<Guid>? MediaIds { get; init; }

    private LinkPreviewRequest? _linkPreview;

    public LinkPreviewRequest? LinkPreview
    {
        get => _linkPreview;
        init
        {
            _linkPreview = value;
            HasLinkPreviewValue = true;
        }
    }

    [JsonIgnore]
    public bool HasLinkPreviewValue { get; private init; }

    private DateTimeOffset? _scheduledAt;

    [FutureDateTime]
    public DateTimeOffset? ScheduledAt
    {
        get => _scheduledAt;
        init
        {
            _scheduledAt = value;
            HasScheduledAtValue = true;
        }
    }

    [JsonIgnore]
    public bool HasScheduledAtValue { get; private init; }
}
