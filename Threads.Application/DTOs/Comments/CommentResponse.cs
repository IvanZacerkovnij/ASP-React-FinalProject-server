using Threads.Application.DTOs.Users;

namespace Threads.Application.DTOs.Comments;

public class CommentResponse
{
    public Guid Id { get; init; }

    public Guid PostId { get; init; }

    public required string Content { get; init; }

    public required UserShortResponse Author { get; init; }

    public int LikesCount { get; init; }

    public bool IsLikedByCurrentUser { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}