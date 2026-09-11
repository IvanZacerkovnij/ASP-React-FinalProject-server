namespace Threads.Application.DTOs.Posts.Models;

public sealed class PostPollOptionStateReadModel
{
    public Guid Id { get; init; }

    public int VotesCount { get; init; }
}
