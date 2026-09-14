using Microsoft.EntityFrameworkCore;
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

    public async Task<PostLike?> GetByUserAndPostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PostLikes
            .FirstOrDefaultAsync(
                like => like.UserId == userId && like.PostId == postId,
                cancellationToken);
    }

    public async Task<CommentLike?> GetByUserAndCommentAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CommentLikes
            .FirstOrDefaultAsync(
                like => like.UserId == userId && like.CommentId == commentId,
                cancellationToken);
    }

    public async Task AddAsync(PostLike like, CancellationToken cancellationToken = default)
    {
        await _dbContext.PostLikes.AddAsync(like, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAsync(CommentLike like, CancellationToken cancellationToken = default)
    {
        await _dbContext.CommentLikes.AddAsync(like, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PostLike like, CancellationToken cancellationToken = default)
    {
        _dbContext.PostLikes.Remove(like);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CommentLike like, CancellationToken cancellationToken = default)
    {
        _dbContext.CommentLikes.Remove(like);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
