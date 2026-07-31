namespace Threads.Application.DTOs.Posts;

public class CreatePostRequest
{
    public required string Content { get; init; }

    public IReadOnlyCollection<Guid> MediaIds { get; init; } = [];
}