using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.DTOs.Validation;
using Threads.Application.Services.Common;

namespace Threads.Application.DTOs.Comments;

public class UpdateCommentRequest
{
    [Required, StringLength(300, MinimumLength = 1)]
    public required string Content { get; init; }

    [MaxLength(MediaAttachmentPolicy.MaximumCount), UniqueNotEmptyGuids]
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
