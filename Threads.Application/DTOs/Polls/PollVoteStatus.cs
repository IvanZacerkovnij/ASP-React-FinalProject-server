namespace Threads.Application.DTOs.Polls;

public enum PollVoteStatus
{
    Success = 0,
    PostNotFound = 1,
    PollNotFound = 2,
    InvalidOption = 3,
    AlreadyVoted = 4,
    PollClosed = 5
}
