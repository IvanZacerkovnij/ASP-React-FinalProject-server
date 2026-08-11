using Microsoft.EntityFrameworkCore;
using Threads.Application.Interfaces.Bookmarks;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Repositories.Bookmarks;

public class BookmarkRepository : IBookmarkRepository
{
    private readonly ThreadsDbContext _dbContext;

    public BookmarkRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public Task<Bookmark?> GetByUserAndPostId(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Bookmarks.FirstOrDefaultAsync(
            bookmark => bookmark.UserId == userId && bookmark.PostId == postId,
            cancellationToken);
    }

    public Task AddAsync(Bookmark bookmark, CancellationToken cancellationToken = default)
    {
        _dbContext.Add(bookmark);
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(Bookmark bookmark, CancellationToken cancellationToken = default)
    {
        _dbContext.Bookmarks.Remove(bookmark);
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}