using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Validation;
using Threads.Application.Services.Common;

namespace Threads.Application.DTOs.Posts.Requests;

public class CreatePostRequest
{
    [StringLength(PostContentPolicy.MaximumLength, MinimumLength = PostContentPolicy.MinimumLength)]
    public string? Content { get; init; }

    [Required, MaxLength(MediaAttachmentPolicy.MaximumCount), UniqueNotEmptyGuids]
    public IReadOnlyCollection<Guid> MediaIds { get; init; } = [];

    public CreatePostPollRequest? Poll { get; init; }

    public PostLocationRequest? Location { get; init; }

    public LinkPreviewRequest? LinkPreview { get; init; }

    [NotEmptyGuid]
    public Guid? QuotedPostId { get; init; }

    [NotEmptyGuid]
    public Guid? QuotedCommentId { get; init; }

    [NotEmptyGuid]
    public Guid? QuotedTargetVersionId { get; init; }
}
