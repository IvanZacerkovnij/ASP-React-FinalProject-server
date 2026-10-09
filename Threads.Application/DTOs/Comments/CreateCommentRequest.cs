using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.DTOs.Validation;

namespace Threads.Application.DTOs.Comments;

public class CreateCommentRequest
{
    [NotEmptyGuid]
    public Guid PostId { get; init; }

    [NotEmptyGuid]
    public Guid? ParentCommentId { get; init; }

    [Required, StringLength(300, MinimumLength = 1)]
    public required string Content { get; init; }

    [Required, MaxLength(20), UniqueNotEmptyGuids]
    public IReadOnlyCollection<Guid> MediaIds { get; init; } = [];

    public CreatePostPollRequest? Poll { get; init; }

    public PostLocationRequest? Location { get; init; }

    public LinkPreviewRequest? LinkPreview { get; init; }
}
