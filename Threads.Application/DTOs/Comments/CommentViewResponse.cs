namespace Threads.Application.DTOs.Comments;

public sealed class CommentViewResponse
{
    public Guid CommentId { get; init; }

    public int ViewsCount { get; init; }
}
