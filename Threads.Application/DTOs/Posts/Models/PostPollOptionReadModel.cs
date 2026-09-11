namespace Threads.Application.DTOs.Posts.Models;

public sealed class PostPollOptionReadModel
{
    public Guid Id { get; init; }

    public required string Text { get; init; }

    public int Position { get; init; }
}
