using Microsoft.EntityFrameworkCore;
using Npgsql;
using Threads.Application.Interfaces.Likes;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Repositories.Likes;

public class LikeRepository : ILikeRepository
{
    private readonly ThreadsDbContext _dbContext;

    public LikeRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryAddAsync(PostLike like, CancellationToken cancellationToken = default)
    {
        await _dbContext.PostLikes.AddAsync(like, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  {
                      SqlState: PostgresErrorCodes.UniqueViolation,
                      ConstraintName: "PK_PostLikes"
                  })
        {
            _dbContext.Entry(like).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> TryAddAsync(CommentLike like, CancellationToken cancellationToken = default)
    {
        
        await _dbContext.CommentLikes.AddAsync(like, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  {
                      SqlState: PostgresErrorCodes.UniqueViolation,
                      ConstraintName: "PK_CommentLikes"
                  })
        {
            _dbContext.Entry(like).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> TryDeletePostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _dbContext.PostLikes
            .Where(like => like.UserId == userId && like.PostId == postId)
            .ExecuteDeleteAsync(cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> TryDeleteCommentAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _dbContext.CommentLikes
            .Where(like => like.UserId == userId && like.CommentId == commentId)
            .ExecuteDeleteAsync(cancellationToken);

        return affectedRows == 1;
    }
}
