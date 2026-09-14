using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Posts.Responses;

namespace Threads.Application.DTOs.Bookmarks;

public sealed class UserBookmarksPageResponse
{
    public required IReadOnlyCollection<PostResponse> Posts { get; init; }

    public required IReadOnlyCollection<CommentResponse> Comments { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}
