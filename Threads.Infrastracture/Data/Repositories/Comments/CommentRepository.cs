using Microsoft.EntityFrameworkCore;
using Threads.Application.Interfaces.Comments;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Repositories.Comments;

public class CommentRepository : ICommentRepository
{
    private readonly ThreadsDbContext _dbContext;

    public CommentRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<Comment>> GetByPostIdAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Comments
            .AsNoTracking()
            .Include(comment => comment.Author)
            .Include(comment => comment.Replies)
            .Include(comment => comment.Likes)
            .Include(comment => comment.Bookmarks)
            .Include(comment => comment.Reposts)
            .Include(comment => comment.Views)
            .Where(comment => comment.PostId == postId)
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Comment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Comments
            .Include(comment => comment.Author)
            .Include(comment => comment.Replies)
            .Include(comment => comment.Likes)
            .Include(comment => comment.Bookmarks)
            .Include(comment => comment.Reposts)
            .Include(comment => comment.Views)
            .FirstOrDefaultAsync(comment => comment.Id == id, cancellationToken);
    }

    public async Task AddAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        await _dbContext.Comments.AddAsync(comment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public void AttachView(View view)
    {
        _dbContext.Views.Add(view);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        _dbContext.Comments.Update(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        _dbContext.Comments.Remove(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
