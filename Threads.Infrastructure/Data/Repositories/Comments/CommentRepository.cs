using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Users;
using Threads.Application.Exceptions;
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

    public async Task<IReadOnlyCollection<CommentSummaryReadModel>> GetByAuthorIdAsync(
        Guid authorId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Comments
            .AsNoTracking()
            .Where(comment => comment.AuthorId == authorId);

        if (cursor is not null)
        {
            query = query.Where(comment => EF.Functions.LessThan(
                ValueTuple.Create(comment.CreatedAt, comment.Id),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        var pageQuery = query
            .OrderByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id)
            .Take(limit + 1);

        return await ProjectToSummaryReadModel(pageQuery, currentUserId)
            .ToListAsync(cancellationToken);
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

    public async Task<IReadOnlyCollection<CommentSummaryReadModel>> GetAncestorsAsync(
        Guid commentId,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var ancestorIds = await _dbContext.Database.SqlQuery<Guid>(
                $"""
                WITH RECURSIVE "Ancestors" AS
                (
                    SELECT parent."Id", parent."ParentCommentId", 1 AS "Depth"
                    FROM "Comments" AS target
                    INNER JOIN "Comments" AS parent
                        ON parent."Id" = target."ParentCommentId"
                    WHERE target."Id" = {commentId}

                    UNION ALL

                    SELECT parent."Id", parent."ParentCommentId", ancestor."Depth" + 1
                    FROM "Ancestors" AS ancestor
                    INNER JOIN "Comments" AS parent
                        ON parent."Id" = ancestor."ParentCommentId"
                )
                SELECT "Id" AS "Value"
                FROM "Ancestors"
                ORDER BY "Depth" DESC
                """)
            .ToListAsync(cancellationToken);

        return await GetSummariesByIdsAsync(ancestorIds, currentUserId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<CommentSummaryReadModel>> GetRepliesAsync(
        Guid parentCommentId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Comments
            .AsNoTracking()
            .Where(comment => comment.ParentCommentId == parentCommentId);

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

    public async Task<IReadOnlyCollection<CommentSummaryReadModel>> GetSummariesByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = ids.Distinct().ToArray();

        if (distinctIds.Length == 0)
        {
            return [];
        }

        var comments = await ProjectToSummaryReadModel(
                _dbContext.Comments
                    .AsNoTracking()
                    .Where(comment => distinctIds.Contains(comment.Id)),
                currentUserId)
            .ToListAsync(cancellationToken);
        var commentOrder = distinctIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);

        return comments
            .OrderBy(comment => commentOrder[comment.Id])
            .ToList();
    }

    public async Task<IReadOnlyCollection<CommentVersion>> GetVersionsAsync(
        Guid commentId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CommentVersions
            .AsNoTracking()
            .Where(version => version.CommentId == commentId);

        if (cursor is not null)
        {
            query = query.Where(version => EF.Functions.GreaterThan(
                ValueTuple.Create(version.CreatedAt, version.Id),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        return await query
            .OrderBy(version => version.CreatedAt)
            .ThenBy(version => version.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CommentVersion>> GetVersionsByIdsAsync(
        IReadOnlyCollection<Guid> versionIds,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = versionIds.Distinct().ToArray();

        if (distinctIds.Length == 0)
        {
            return [];
        }

        return await _dbContext.CommentVersions
            .AsNoTracking()
            .Where(version => distinctIds.Contains(version.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<Comment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Comments
            .AsSplitQuery()
            .Include(comment => comment.Media)
            .Include(comment => comment.Poll)
                .ThenInclude(poll => poll!.Options)
                    .ThenInclude(option => option.Votes)
            .Include(comment => comment.Poll)
                .ThenInclude(poll => poll!.Votes)
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
    public Task<bool> VersionExistsAsync(Guid commentId, Guid versionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.CommentVersions
            .AsNoTracking()
            .AnyAsync(
                version => version.CommentId == commentId &&
                           version.Id == versionId,
                cancellationToken);
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

        return query
            .AsSplitQuery()
            .Select(comment => new CommentSummaryReadModel
            {
            Id = comment.Id,
            VersionId = comment.CurrentVersionId,
            PostId = comment.PostId,
            ParentCommentId = comment.ParentCommentId,
            Content = comment.Content,
            LinkPreviewUrl = comment.LinkPreviewUrl,
            LinkPreviewTitle = comment.LinkPreviewTitle,
            LinkPreviewImageUrl = comment.LinkPreviewImageUrl,
            Author = new UserSummaryReadModel
            {
                Id = comment.Author.Id,
                Username = comment.Author.Username,
                DisplayName = comment.Author.DisplayName,
                Bio = comment.Author.Bio,
                LocationPlaceId = comment.Author.LocationPlaceId,
                LocationName = comment.Author.Location,
                LocationCountry = comment.Author.LocationCountry,
                LocationLatitude = comment.Author.LocationLatitude,
                LocationLongitude = comment.Author.LocationLongitude,
                AvatarObjectKey = comment.Author.AvatarObjectKey,
                IsVerified = comment.Author.IsVerified
            },
            Media = comment.Media
                .OrderBy(media => media.SortOrder)
                .Select(media => new PostMediaReadModel
                {
                    Id = media.Id,
                    StorageKey = media.StorageKey,
                    ThumbnailStorageKey = media.ThumbnailStorageKey,
                    FileName = media.FileName,
                    ContentType = media.ContentType,
                    Type = media.Type,
                    SizeInBytes = media.SizeInBytes,
                    Width = media.Width,
                    Height = media.Height,
                    DurationSeconds = media.DurationSeconds,
                    SortOrder = media.SortOrder
                })
                .ToList(),
            Poll = comment.Poll == null
                ? null
                : new PostPollSummaryReadModel
                {
                    Id = comment.Poll.Id,
                    EndsAt = comment.Poll.EndsAt,
                    TotalVotes = comment.Poll.Votes.Count,
                    SelectedOptionId = hasCurrentUser
                        ? comment.Poll.Votes
                            .Where(vote => vote.UserId == effectiveCurrentUserId)
                            .Select(vote => (Guid?)vote.PollOptionId)
                            .FirstOrDefault()
                        : null,
                    Options = comment.Poll.Options
                        .OrderBy(option => option.Position)
                        .Select(option => new PostPollOptionSummaryReadModel
                        {
                            Id = option.Id,
                            Text = option.Text,
                            Position = option.Position,
                            VotesCount = option.Votes.Count
                        })
                        .ToList()
                },
            LocationPlaceId = comment.LocationPlaceId,
            LocationName = comment.LocationName,
            LocationCountry = comment.LocationCountry,
            LocationLatitude = comment.LocationLatitude,
            LocationLongitude = comment.LocationLongitude,
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

    public void RemovePoll(Poll poll)
    {
        _dbContext.Polls.Remove(poll);
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
        if (_dbContext.Entry(comment).State == EntityState.Detached)
        {
            _dbContext.Comments.Update(comment);
        }

        var currentVersion = comment.Versions.SingleOrDefault(version =>
            version.Id == comment.CurrentVersionId);

        if (currentVersion is not null &&
            _dbContext.Entry(currentVersion).State != EntityState.Added)
        {
            await _dbContext.CommentVersions.AddAsync(currentVersion, cancellationToken);
        }

        await ConcurrencySaveChanges.SaveAsync(
            _dbContext,
            "comment",
            cancellationToken);
    }

    public async Task DeleteAsync(Comment comment, CancellationToken cancellationToken = default)
    {
        var deletedAt = DateTimeOffset.UtcNow;

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            WITH RECURSIVE descendants AS (
                SELECT "Id" FROM "Comments" WHERE "Id" = {comment.Id}
                UNION ALL
                SELECT child."Id"
                FROM "Comments" child
                INNER JOIN descendants parent ON child."ParentCommentId" = parent."Id"
            )
            UPDATE "Comments"
            SET "DeletedAt" = {deletedAt}, "UpdatedAt" = {deletedAt}
            WHERE "Id" IN (SELECT "Id" FROM descendants)
              AND "DeletedAt" IS NULL
            """,
            cancellationToken);

        _dbContext.ChangeTracker.Clear();
    }

    private sealed class OrderedCommentReference
    {
        public Guid Id { get; init; }

        public DateTimeOffset ActionAt { get; init; }
    }
}
