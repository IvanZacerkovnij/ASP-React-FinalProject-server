using Threads.Application.DTOs.Users;

namespace Threads.Application.DTOs.Comments;

public sealed class CommentSummaryReadModel
{
    public Guid Id { get; init; }

    public Guid PostId { get; init; }

    public Guid? ParentCommentId { get; init; }

    public required string Content { get; init; }

    public required UserSummaryReadModel Author { get; init; }

    public int LikesCount { get; init; }

    public bool IsLikedByCurrentUser { get; init; }

    public int RepliesCount { get; init; }

    public bool IsBookmarkedByCurrentUser { get; init; }

    public int RepostsCount { get; init; }

    public bool IsRepostedByCurrentUser { get; init; }

    public int ViewsCount { get; init; }

    public DateTimeOffset? ActionAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
