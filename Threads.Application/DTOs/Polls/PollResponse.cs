namespace Threads.Application.DTOs.Polls;

public class PollResponse
{
    public Guid Id { get; init; }

    public Guid PostId { get; init; }

    public DateTime? EndsAt { get; init; }

    public int TotalVotes { get; init; }

    public bool HasVotedByCurrentUser { get; init; }

    public Guid? SelectedOptionId { get; init; }

    public IReadOnlyCollection<PollOptionResponse> Options { get; init; } = [];
}
