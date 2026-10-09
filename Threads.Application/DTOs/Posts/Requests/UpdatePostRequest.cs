using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Validation;

namespace Threads.Application.DTOs.Posts.Requests;

public class UpdatePostRequest
{
    [StringLength(2000, MinimumLength = 1)]
    public string? Content { get; init; }

    [UniqueNotEmptyGuids]
    public IReadOnlyCollection<Guid>? MediaIds { get; init; }

    [DefaultValue(false)]
    public bool RemovePoll { get; init; }

    public CreatePostPollRequest? Poll { get; init; }

    [DefaultValue(false)]
    public bool RemoveLocation { get; init; }

    public PostLocationRequest? Location { get; init; }

    [DefaultValue(false)]
    public bool RemoveLinkPreview { get; init; }

    public LinkPreviewRequest? LinkPreview { get; init; }
}
