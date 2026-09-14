using Microsoft.EntityFrameworkCore;
using Threads.Application.Interfaces.Bookmarks;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Repositories.Bookmarks;

public class BookmarkRepository : IBookmarkRepository
{
    private readonly ThreadsDbContext _dbContext;

    public BookmarkRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public Task<PostBookmark?> GetByUserAndPostId(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PostBookmarks.FirstOrDefaultAsync(
            bookmark => bookmark.UserId == userId && bookmark.PostId == postId,
            cancellationToken);
    }

    public Task<CommentBookmark?> GetByUserAndCommentId(Guid userId, Guid commentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.CommentBookmarks.FirstOrDefaultAsync(
            bookmark => bookmark.UserId == userId && bookmark.CommentId == commentId,
            cancellationToken);
    }

    public Task AddAsync(PostBookmark bookmark, CancellationToken cancellationToken = default)
    {
        _dbContext.PostBookmarks.Add(bookmark);
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task AddAsync(CommentBookmark bookmark, CancellationToken cancellationToken = default)
    {
        _dbContext.CommentBookmarks.Add(bookmark);
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(PostBookmark bookmark, CancellationToken cancellationToken = default)
    {
        _dbContext.PostBookmarks.Remove(bookmark);
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(CommentBookmark bookmark, CancellationToken cancellationToken = default)
    {
        _dbContext.CommentBookmarks.Remove(bookmark);
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
