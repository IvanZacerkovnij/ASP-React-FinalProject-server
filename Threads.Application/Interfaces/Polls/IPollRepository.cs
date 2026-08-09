using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Polls;

public interface IPollRepository
{
    Task<Poll?> GetByPostIdAsync(Guid postId, CancellationToken cancellationToken = default);
    Task AddVoteAsync(PollVote vote, CancellationToken cancellationToken = default);
}
