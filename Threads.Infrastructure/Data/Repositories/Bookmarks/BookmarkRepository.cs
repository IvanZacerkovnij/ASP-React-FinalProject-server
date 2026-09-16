using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    public async Task<bool> TryAddAsync(PostBookmark bookmark, CancellationToken cancellationToken = default)
    {
        await _dbContext.PostBookmarks.AddAsync(bookmark, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  {
                      SqlState: PostgresErrorCodes.UniqueViolation,
                      ConstraintName: "PK_PostBookmarks"
                  })
        {
            _dbContext.Entry(bookmark).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> TryAddAsync(CommentBookmark bookmark, CancellationToken cancellationToken = default)
    {
        await _dbContext.CommentBookmarks.AddAsync(bookmark, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  {
                      SqlState: PostgresErrorCodes.UniqueViolation,
                      ConstraintName: "PK_CommentBookmarks"
                  })
        {
            _dbContext.Entry(bookmark).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> TryDeletePostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _dbContext.PostBookmarks
            .Where(bookmark => bookmark.UserId == userId && bookmark.PostId == postId)
            .ExecuteDeleteAsync(cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> TryDeleteCommentAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _dbContext.CommentBookmarks
            .Where(bookmark => bookmark.UserId == userId && bookmark.CommentId == commentId)
            .ExecuteDeleteAsync(cancellationToken);

        return affectedRows == 1;
    }
}
