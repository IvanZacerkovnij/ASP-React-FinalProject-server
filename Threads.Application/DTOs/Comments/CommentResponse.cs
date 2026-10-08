using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Media;
using Threads.Application.DTOs.Polls;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Quotes;
using Threads.Application.DTOs.Users;

namespace Threads.Application.DTOs.Comments;

public class CommentResponse
{
    public Guid Id { get; init; }

    public Guid VersionId { get; init; }

    public Guid PostId { get; init; }

    public Guid? ParentCommentId { get; init; }

    public required string Content { get; init; }

    public required UserShortResponse Author { get; init; }

    public IReadOnlyCollection<MediaAttachmentResponse> Attachments { get; init; } = [];

    public PollResponse? Poll { get; init; }

    public PostLocationResponse? Location { get; init; }

    public LinkPreviewResponse? LinkPreview { get; init; }

    public QuoteResponse? Quote { get; init; }

    public int LikesCount { get; init; }

    public bool IsLikedByCurrentUser { get; init; }

    public int RepliesCount { get; init; }

    public bool IsBookmarkedByCurrentUser { get; init; }

    public int RepostsCount { get; init; }

    public bool IsRepostedByCurrentUser { get; init; }

    public int ViewsCount { get; init; }

    public DateTimeOffset? ActionAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
