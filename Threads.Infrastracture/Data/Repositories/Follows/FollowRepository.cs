using Microsoft.EntityFrameworkCore;
using Threads.Application.Interfaces.Follows;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Repositories.Follows;

public class FollowRepository : IFollowRepository
{
    private readonly ThreadsDbContext _dbContext;

    public FollowRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Follow?> GetByFollowerAndFollowingAsync(
        Guid followerId,
        Guid followingId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Follows
            .FirstOrDefaultAsync(
                follow => follow.FollowerId == followerId && follow.FollowingId == followingId,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<User>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Follows
            .AsNoTracking()
            .Where(follow => follow.FollowingId == userId)
            .Select(follow => follow.Follower)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<User>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Follows
            .AsNoTracking()
            .Where(follow => follow.FollowerId == userId)
            .Select(follow => follow.Following)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Follow follow, CancellationToken cancellationToken = default)
    {
        await _dbContext.Follows.AddAsync(follow, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Follow follow, CancellationToken cancellationToken = default)
    {
        _dbContext.Follows.Remove(follow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
