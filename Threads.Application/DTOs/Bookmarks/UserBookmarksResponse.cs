using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Posts;

namespace Threads.Application.DTOs.Bookmarks;

public sealed class UserBookmarksResponse
{
    public required IReadOnlyCollection<PostResponse> Posts { get; init; }

    public required IReadOnlyCollection<CommentResponse> Comments { get; init; }
}
