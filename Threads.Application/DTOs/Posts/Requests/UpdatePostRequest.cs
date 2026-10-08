using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Validation;

namespace Threads.Application.DTOs.Posts.Requests;

public class UpdatePostRequest
{
    [StringLength(2000, MinimumLength = 1)]
    public string? Content { get; init; }

    [UniqueNotEmptyGuids]
    public IReadOnlyCollection<Guid>? MediaIds { get; init; }

    public CreatePostPollRequest? Poll { get; init; }

    [DefaultValue(false)]
    public bool RemoveLocation { get; init; }

    public PostLocationRequest? Location { get; init; }

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
}
