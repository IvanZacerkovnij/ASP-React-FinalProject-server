using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Users;
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

    public async Task<IReadOnlyCollection<CommentSummaryReadModel>> GetByPostIdAsync(
        Guid postId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Comments
            .AsNoTracking()
            .Where(comment => comment.PostId == postId);

        if (cursor is not null)
        {
            query = query.Where(comment => EF.Functions.GreaterThan(
                ValueTuple.Create(comment.CreatedAt, comment.Id),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        var pageQuery = query
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .Take(limit + 1);

        return await ProjectToSummaryReadModel(pageQuery, currentUserId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Comment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Comments
            .FirstOrDefaultAsync(comment => comment.Id == id, cancellationToken);
    }

    public async Task<CommentSummaryReadModel?> GetSummaryByIdAsync(
        Guid id,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        return await ProjectToSummaryReadModel(
                _dbContext.Comments
                    .AsNoTracking()
                    .Where(comment => comment.Id == id),
                currentUserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid?> GetPostIdByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Comments
            .AsNoTracking()
            .Where(comment => comment.Id == id)
            .Select(comment => (Guid?)comment.PostId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Comments
            .AsNoTracking()
            .AnyAsync(comment => comment.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<CommentSummaryReadModel>> GetBookmarkedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
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

        var bookmarkedComments = await query
            .OrderByDescending(bookmark => bookmark.CreatedAt)
            .ThenByDescending(bookmark => bookmark.CommentId)
            .Select(bookmark => new OrderedCommentReference
            {
                Id = bookmark.CommentId,
                ActionAt = bookmark.CreatedAt
            })
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return await GetByOrderedIdsAsync(bookmarkedComments, currentUserId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<CommentSummaryReadModel>> GetLikedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
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

        var likedComments = await query
            .OrderByDescending(like => like.CreatedAt)
            .ThenByDescending(like => like.CommentId)
            .Select(like => new OrderedCommentReference
            {
                Id = like.CommentId,
                ActionAt = like.CreatedAt
            })
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return await GetByOrderedIdsAsync(likedComments, currentUserId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<CommentSummaryReadModel>> GetRepostedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
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

        var repostedComments = await query
            .OrderByDescending(repost => repost.CreatedAt)
            .ThenByDescending(repost => repost.CommentId)
            .Select(repost => new OrderedCommentReference
            {
                Id = repost.CommentId,
                ActionAt = repost.CreatedAt
            })
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return await GetByOrderedIdsAsync(repostedComments, currentUserId, cancellationToken);
    }

    private async Task<IReadOnlyCollection<CommentSummaryReadModel>> GetByOrderedIdsAsync(
        IReadOnlyCollection<OrderedCommentReference> commentReferences,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        if (commentReferences.Count == 0)
        {
            return [];
        }

        var commentIds = commentReferences.Select(reference => reference.Id).ToArray();
        var comments = await ProjectToSummaryReadModel(
                _dbContext.Comments
                    .AsNoTracking()
                    .Where(comment => commentIds.Contains(comment.Id)),
                currentUserId)
            .ToListAsync(cancellationToken);

        var commentOrder = commentReferences
            .Select((reference, index) => new { reference.Id, index })
            .ToDictionary(item => item.Id, item => item.index);
        var actionTimes = commentReferences.ToDictionary(reference => reference.Id, reference => reference.ActionAt);

        var orderedComments = comments
            .OrderBy(comment => commentOrder[comment.Id])
            .ToList();

        foreach (var comment in orderedComments)
        {
            comment.ActionAt = actionTimes[comment.Id];
        }

        return orderedComments;
    }

    private static IQueryable<CommentSummaryReadModel> ProjectToSummaryReadModel(
        IQueryable<Comment> query,
        Guid? currentUserId)
    {
        var hasCurrentUser = currentUserId.HasValue;
        var effectiveCurrentUserId = currentUserId ?? Guid.Empty;

        return query.Select(comment => new CommentSummaryReadModel
        {
            Id = comment.Id,
            PostId = comment.PostId,
            ParentCommentId = comment.ParentCommentId,
            Content = comment.Content,
            Author = new UserSummaryReadModel
            {
                Id = comment.Author.Id,
                Username = comment.Author.Username,
                DisplayName = comment.Author.DisplayName,
                LocationPlaceId = comment.Author.LocationPlaceId,
                LocationName = comment.Author.Location,
                LocationCountry = comment.Author.LocationCountry,
                LocationLatitude = comment.Author.LocationLatitude,
                LocationLongitude = comment.Author.LocationLongitude,
                AvatarObjectKey = comment.Author.AvatarObjectKey,
                IsVerified = comment.Author.IsVerified
            },
            LikesCount = comment.CommentLikes.Count,
            IsLikedByCurrentUser = hasCurrentUser &&
                comment.CommentLikes.Any(like => like.UserId == effectiveCurrentUserId),
            RepliesCount = comment.Replies.Count,
            IsBookmarkedByCurrentUser = hasCurrentUser &&
                comment.CommentBookmarks.Any(bookmark => bookmark.UserId == effectiveCurrentUserId),
            RepostsCount = comment.CommentReposts.Count,
            IsRepostedByCurrentUser = hasCurrentUser &&
                comment.CommentReposts.Any(repost => repost.UserId == effectiveCurrentUserId),
            ViewsCount = comment.CommentViews.Count,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt
        });
    }

    public async Task AddAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        await _dbContext.Comments.AddAsync(comment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
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
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        _dbContext.Comments.Remove(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class OrderedCommentReference
    {
        public Guid Id { get; init; }

        public DateTimeOffset ActionAt { get; init; }
    }
}
