using Microsoft.EntityFrameworkCore;
using Threads.Application.Interfaces.Polls;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Repositories.Polls;

public class PollRepository : IPollRepository
{
    private readonly ThreadsDbContext _dbContext;

    public PollRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Poll?> GetByPostIdAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Polls
            .Include(poll => poll.Options.OrderBy(option => option.Position))
            .ThenInclude(option => option.Votes)
            .Include(poll => poll.Votes)
            .FirstOrDefaultAsync(poll => poll.PostId == postId, cancellationToken);
    }

    public async Task AddVoteAsync(PollVote vote, CancellationToken cancellationToken = default)
    {
        await _dbContext.PollVotes.AddAsync(vote, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
