using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Posts;

namespace Threads.Application.DTOs.Likes;

public sealed class UserLikesResponse
{
    public required IReadOnlyCollection<PostResponse> Posts { get; init; }

    public required IReadOnlyCollection<CommentResponse> Comments { get; init; }
}
