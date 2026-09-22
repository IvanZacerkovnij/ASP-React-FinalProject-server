using Microsoft.EntityFrameworkCore;
using Npgsql;
using Threads.Application.Interfaces.Polls;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Repositories.Polls;

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
            .AsNoTracking()
            .Include(poll => poll.Options.OrderBy(option => option.Position))
            .ThenInclude(option => option.Votes)
            .Include(poll => poll.Votes)
            .FirstOrDefaultAsync(poll => poll.PostId == postId, cancellationToken);
    }

    public async Task<bool> TryAddVoteAsync(
        PollVote vote,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.PollVotes.AddAsync(vote, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  {
                      SqlState: PostgresErrorCodes.UniqueViolation,
                      ConstraintName: "IX_PollVotes_PollId_UserId"
                  })
        {
            _dbContext.Entry(vote).State = EntityState.Detached;
            return false;
        }
    }
}
