namespace Threads.Application.DTOs.Posts.Models;

public sealed class PostPollOptionSummaryReadModel
{
    public Guid Id { get; init; }

    public required string Text { get; init; }

    public int Position { get; init; }

    public int VotesCount { get; init; }
}
