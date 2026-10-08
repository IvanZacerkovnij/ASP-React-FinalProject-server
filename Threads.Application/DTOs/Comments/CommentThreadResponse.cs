using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Responses;

namespace Threads.Application.DTOs.Comments;

public sealed class CommentThreadResponse
{
    public required PostResponse Post { get; init; }

    public required IReadOnlyCollection<CommentResponse> Ancestors { get; init; }

    public required CommentResponse Target { get; init; }

    public required CursorPageResponse<CommentResponse> Replies { get; init; }
}
