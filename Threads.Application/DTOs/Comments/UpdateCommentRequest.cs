namespace Threads.Application.DTOs.Comments;

public class UpdateCommentRequest
{
    public Guid PostId { get; init; }

    public required string Content { get; init; }
}