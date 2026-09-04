using Microsoft.EntityFrameworkCore;
using Threads.Application.Interfaces.Likes;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Repositories.Likes;

public class LikeRepository : ILikeRepository
{
    private readonly ThreadsDbContext _dbContext;

    public LikeRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Like?> GetByUserAndPostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Likes
            .FirstOrDefaultAsync(
                like => like.UserId == userId && like.PostId == postId && like.CommentId == null,
                cancellationToken);
    }

    public async Task<Like?> GetByUserAndCommentAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Likes
            .FirstOrDefaultAsync(
                like => like.UserId == userId && like.CommentId == commentId && like.PostId == null,
                cancellationToken);
    }

    public async Task AddAsync(Like like, CancellationToken cancellationToken = default)
    {
        await _dbContext.Likes.AddAsync(like, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Like like, CancellationToken cancellationToken = default)
    {
        _dbContext.Likes.Remove(like);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
