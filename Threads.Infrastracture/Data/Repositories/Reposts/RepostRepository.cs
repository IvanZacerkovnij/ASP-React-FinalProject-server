using Microsoft.EntityFrameworkCore;
using Threads.Application.Interfaces.Reposts;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Repositories.Reposts;

public class RepostRepository : IRepostRepository
{
    private readonly ThreadsDbContext _dbContext;

    public RepostRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Repost?> GetByUserAndPostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reposts
            .FirstOrDefaultAsync(
                repost => repost.UserId == userId && repost.PostId == postId,
                cancellationToken);
    }

    public async Task AddAsync(Repost repost, CancellationToken cancellationToken = default)
    {
        await _dbContext.Reposts.AddAsync(repost, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Repost repost, CancellationToken cancellationToken = default)
    {
        _dbContext.Reposts.Remove(repost);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
