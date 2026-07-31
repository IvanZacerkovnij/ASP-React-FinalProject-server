using Threads.Application.DTOs.Users;

namespace Threads.Application.DTOs.Posts;

public class PostResponse
{
    public Guid Id { get; init; }

    public required string Content { get; init; }

    public required UserShortResponse Author { get; init; }

    public IReadOnlyCollection<string> MediaUrls { get; init; } = [];

    public int LikesCount { get; init; }

    public int CommentsCount { get; init; }

    public int RepostsCount { get; init; }

    public bool IsLikedByCurrentUser { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}