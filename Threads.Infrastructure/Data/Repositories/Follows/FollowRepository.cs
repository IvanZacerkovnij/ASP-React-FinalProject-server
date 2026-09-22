using Microsoft.EntityFrameworkCore;
using Npgsql;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Interfaces.Follows;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Repositories.Follows;

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

    public async Task<IReadOnlyCollection<Follow>> GetFollowersAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Follows
            .AsNoTracking()
            .Include(follow => follow.Follower)
            .Where(follow => follow.FollowingId == userId);

        if (cursor is not null)
        {
            query = query.Where(follow => EF.Functions.LessThan(
                ValueTuple.Create(follow.CreatedAt, follow.Id),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        return await query
            .OrderByDescending(follow => follow.CreatedAt)
            .ThenByDescending(follow => follow.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Follow>> GetFollowingAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Follows
            .AsNoTracking()
            .Include(follow => follow.Following)
            .Where(follow => follow.FollowerId == userId);

        if (cursor is not null)
        {
            query = query.Where(follow => EF.Functions.LessThan(
                ValueTuple.Create(follow.CreatedAt, follow.Id),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        return await query
            .OrderByDescending(follow => follow.CreatedAt)
            .ThenByDescending(follow => follow.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryAddAsync(
        Follow follow,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Follows.AddAsync(follow, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  {
                      SqlState: PostgresErrorCodes.UniqueViolation,
                      ConstraintName: "IX_Follows_FollowerId_FollowingId"
                  })
        {
            _dbContext.Entry(follow).State = EntityState.Detached;
            return false;
        }
    }

    public async Task DeleteAsync(Follow follow, CancellationToken cancellationToken = default)
    {
        _dbContext.Follows.Remove(follow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
