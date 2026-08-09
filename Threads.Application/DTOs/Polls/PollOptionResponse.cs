namespace Threads.Application.DTOs.Polls;

public class PollOptionResponse
{
    public Guid Id { get; init; }

    public required string Text { get; init; }

    public int Position { get; init; }

    public int VotesCount { get; init; }
}
