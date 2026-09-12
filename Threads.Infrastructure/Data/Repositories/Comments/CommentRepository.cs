using Microsoft.EntityFrameworkCore;
using Threads.Application.Interfaces.Comments;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Repositories.Comments;

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

    public async Task<IReadOnlyCollection<Comment>> GetBookmarkedByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var bookmarkedCommentIds = await _dbContext.Bookmarks
            .AsNoTracking()
            .Where(bookmark =>
                bookmark.UserId == userId &&
                bookmark.CommentId.HasValue &&
                bookmark.PostId == null)
            .OrderByDescending(bookmark => bookmark.CreatedAt)
            .Select(bookmark => bookmark.CommentId!.Value)
            .ToListAsync(cancellationToken);

        return await GetByOrderedIdsAsync(bookmarkedCommentIds, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Comment>> GetLikedByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var likedCommentIds = await _dbContext.Likes
            .AsNoTracking()
            .Where(like =>
                like.UserId == userId &&
                like.CommentId.HasValue &&
                like.PostId == null)
            .OrderByDescending(like => like.CreatedAt)
            .Select(like => like.CommentId!.Value)
            .ToListAsync(cancellationToken);

        return await GetByOrderedIdsAsync(likedCommentIds, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Comment>> GetRepostedByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var repostedCommentIds = await _dbContext.Reposts
            .AsNoTracking()
            .Where(repost =>
                repost.UserId == userId &&
                repost.CommentId.HasValue &&
                repost.PostId == null)
            .OrderByDescending(repost => repost.CreatedAt)
            .Select(repost => repost.CommentId!.Value)
            .ToListAsync(cancellationToken);

        return await GetByOrderedIdsAsync(repostedCommentIds.Distinct().ToList(), cancellationToken);
    }

    private async Task<IReadOnlyCollection<Comment>> GetByOrderedIdsAsync(
        IReadOnlyCollection<Guid> commentIds,
        CancellationToken cancellationToken)
    {
        if (commentIds.Count == 0)
        {
            return [];
        }

        var comments = await _dbContext.Comments
            .AsNoTracking()
            .AsSplitQuery()
            .Include(comment => comment.Author)
            .Include(comment => comment.Replies)
            .Include(comment => comment.Likes)
            .Include(comment => comment.Bookmarks)
            .Include(comment => comment.Reposts)
            .Include(comment => comment.Views)
            .Where(comment => commentIds.Contains(comment.Id))
            .ToListAsync(cancellationToken);

        var commentOrder = commentIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);

        return comments
            .OrderBy(comment => commentOrder[comment.Id])
            .ToList();
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
