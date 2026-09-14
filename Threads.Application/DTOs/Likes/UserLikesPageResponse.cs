using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Posts.Responses;

namespace Threads.Application.DTOs.Likes;

public sealed class UserLikesPageResponse
{
    public required IReadOnlyCollection<PostResponse> Posts { get; init; }

    public required IReadOnlyCollection<CommentResponse> Comments { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}
