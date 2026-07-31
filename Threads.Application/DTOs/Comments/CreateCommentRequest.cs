namespace Threads.Application.DTOs.Comments;

public class CreateCommentRequest
{
    public Guid PostId { get; init; }

    public required string Content { get; init; }
}