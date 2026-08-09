namespace Threads.Application.DTOs.Polls;

public class PollVoteResult
{
    public PollVoteStatus Status { get; init; }

    public PollResponse? Poll { get; init; }
}
