using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Pagination;
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
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Comments
            .AsNoTracking()
            .AsSplitQuery()
            .Include(comment => comment.Author)
            .Include(comment => comment.Replies)
            .Include(comment => comment.CommentLikes)
            .Include(comment => comment.CommentBookmarks)
            .Include(comment => comment.CommentReposts)
            .Where(comment => comment.PostId == postId);

        if (cursor is not null)
        {
            query = query.Where(comment => EF.Functions.GreaterThan(
                ValueTuple.Create(comment.CreatedAt, comment.Id),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        return await query
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
    }

    public async Task<Comment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        bool trackChanges = true)
    {
        var query = trackChanges
            ? _dbContext.Comments.AsQueryable()
            : _dbContext.Comments.AsNoTracking();

        return await query
            .AsSplitQuery()
            .Include(comment => comment.Author)
            .Include(comment => comment.Replies)
            .Include(comment => comment.CommentLikes)
            .Include(comment => comment.CommentBookmarks)
            .Include(comment => comment.CommentReposts)
            .FirstOrDefaultAsync(comment => comment.Id == id, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Comments
            .AsNoTracking()
            .AnyAsync(comment => comment.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Comment>> GetBookmarkedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CommentBookmarks
            .AsNoTracking()
            .Where(bookmark => bookmark.UserId == userId);

        if (cursor is not null)
        {
            query = query.Where(bookmark => EF.Functions.LessThan(
                ValueTuple.Create(bookmark.CreatedAt, bookmark.CommentId),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        var bookmarkedCommentIds = await query
            .OrderByDescending(bookmark => bookmark.CreatedAt)
            .ThenByDescending(bookmark => bookmark.CommentId)
            .Select(bookmark => bookmark.CommentId)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return await GetByOrderedIdsAsync(bookmarkedCommentIds, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Comment>> GetLikedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CommentLikes
            .AsNoTracking()
            .Where(like => like.UserId == userId);

        if (cursor is not null)
        {
            query = query.Where(like => EF.Functions.LessThan(
                ValueTuple.Create(like.CreatedAt, like.CommentId),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        var likedCommentIds = await query
            .OrderByDescending(like => like.CreatedAt)
            .ThenByDescending(like => like.CommentId)
            .Select(like => like.CommentId)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return await GetByOrderedIdsAsync(likedCommentIds, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Comment>> GetRepostedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CommentReposts
            .AsNoTracking()
            .Where(repost => repost.UserId == userId);

        if (cursor is not null)
        {
            query = query.Where(repost => EF.Functions.LessThan(
                ValueTuple.Create(repost.CreatedAt, repost.CommentId),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        var repostedCommentIds = await query
            .OrderByDescending(repost => repost.CreatedAt)
            .ThenByDescending(repost => repost.CommentId)
            .Select(repost => repost.CommentId)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return await GetByOrderedIdsAsync(repostedCommentIds, cancellationToken);
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
            .Include(comment => comment.CommentLikes)
            .Include(comment => comment.CommentBookmarks)
            .Include(comment => comment.CommentReposts)
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

    public async Task<IReadOnlyDictionary<Guid, int>> GetViewCountsAsync(
        IReadOnlyCollection<Guid> commentIds,
        CancellationToken cancellationToken = default)
    {
        if (commentIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _dbContext.CommentViews
            .AsNoTracking()
            .Where(view => commentIds.Contains(view.CommentId))
            .GroupBy(view => view.CommentId)
            .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);
    }

    public async Task<int?> RecordViewAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "CommentViews" ("CommentId", "UserId", "CreatedAt")
            SELECT comment."Id", {userId}, CURRENT_TIMESTAMP
            FROM "Comments" AS comment
            WHERE comment."Id" = {id}
            ON CONFLICT ("CommentId", "UserId") DO NOTHING
            """,
            cancellationToken);

        var viewsCount = await _dbContext.Comments
            .AsNoTracking()
            .Where(comment => comment.Id == id)
            .Select(comment => (int?)comment.CommentViews.Count)
            .FirstOrDefaultAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return viewsCount;
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
