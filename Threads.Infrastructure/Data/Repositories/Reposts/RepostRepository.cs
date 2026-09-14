using Microsoft.EntityFrameworkCore;
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

    public async Task<PostRepost?> GetByUserAndPostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PostReposts
            .FirstOrDefaultAsync(
                repost => repost.UserId == userId && repost.PostId == postId,
                cancellationToken);
    }

    public async Task<CommentRepost?> GetByUserAndCommentAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CommentReposts
            .FirstOrDefaultAsync(
                repost => repost.UserId == userId && repost.CommentId == commentId,
                cancellationToken);
    }

    public async Task AddAsync(PostRepost repost, CancellationToken cancellationToken = default)
    {
        await _dbContext.PostReposts.AddAsync(repost, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAsync(CommentRepost repost, CancellationToken cancellationToken = default)
    {
        await _dbContext.CommentReposts.AddAsync(repost, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PostRepost repost, CancellationToken cancellationToken = default)
    {
        _dbContext.PostReposts.Remove(repost);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CommentRepost repost, CancellationToken cancellationToken = default)
    {
        _dbContext.CommentReposts.Remove(repost);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
