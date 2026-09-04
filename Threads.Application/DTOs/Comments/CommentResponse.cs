using Threads.Application.DTOs.Users;

namespace Threads.Application.DTOs.Comments;

public class CommentResponse
{
    public Guid Id { get; init; }

    public Guid PostId { get; init; }

    public Guid? ParentCommentId { get; init; }

    public required string Content { get; init; }

    public required UserShortResponse Author { get; init; }

    public int LikesCount { get; init; }

    public bool IsLikedByCurrentUser { get; init; }

    public int RepliesCount { get; init; }

    public bool IsBookmarkedByCurrentUser { get; init; }

    public int RepostsCount { get; init; }

    public bool IsRepostedByCurrentUser { get; init; }

    public int ViewsCount { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}
