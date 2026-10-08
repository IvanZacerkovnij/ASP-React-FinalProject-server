using Threads.Application.DTOs.Polls;

namespace Threads.Application.Interfaces.Polls;

public interface IPollService
{
    Task<PollVoteResult> VoteAsync(
        Guid userId,
        Guid postId,
        VotePollRequest request,
        CancellationToken cancellationToken = default);
    Task<PollVoteResult> VoteCommentAsync(
        Guid userId,
        Guid commentId,
        VotePollRequest request,
        CancellationToken cancellationToken = default);
}
