using Microsoft.EntityFrameworkCore;
using Npgsql;
using Threads.Application.Interfaces.Reposts;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Repositories.Reposts;

public class RepostRepository : IRepostRepository
{
    private readonly ThreadsDbContext _dbContext;

    public RepostRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryAddAsync(PostRepost repost, CancellationToken cancellationToken = default)
    {
        await _dbContext.PostReposts.AddAsync(repost, cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  {
                      SqlState: PostgresErrorCodes.UniqueViolation,
                      ConstraintName: "PK_PostReposts"
                  })
        {
            _dbContext.Entry(repost).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> TryAddAsync(CommentRepost repost, CancellationToken cancellationToken = default)
    {
        await _dbContext.CommentReposts.AddAsync(repost, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  {
                      SqlState: PostgresErrorCodes.UniqueViolation,
                      ConstraintName: "PK_CommentReposts"
                  })
        {
            _dbContext.Entry(repost).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> TryDeletePostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _dbContext.PostReposts
            .Where(repost => repost.UserId == userId && repost.PostId == postId)
            .ExecuteDeleteAsync(cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> TryDeleteCommentAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _dbContext.CommentReposts
            .Where(repost => repost.UserId == userId && repost.CommentId == commentId)
            .ExecuteDeleteAsync(cancellationToken);

        return affectedRows == 1;
    }
}
