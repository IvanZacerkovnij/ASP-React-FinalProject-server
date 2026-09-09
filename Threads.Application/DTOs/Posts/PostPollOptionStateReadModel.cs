namespace Threads.Application.DTOs.Posts;

public sealed class PostPollOptionStateReadModel
{
    public Guid Id { get; init; }

    public int VotesCount { get; init; }
}
