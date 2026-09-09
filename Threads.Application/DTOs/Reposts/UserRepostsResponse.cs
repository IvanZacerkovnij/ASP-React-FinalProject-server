using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Posts;

namespace Threads.Application.DTOs.Reposts;

public sealed class UserRepostsResponse
{
    public required IReadOnlyCollection<PostResponse> Posts { get; init; }

    public required IReadOnlyCollection<CommentResponse> Comments { get; init; }
}
