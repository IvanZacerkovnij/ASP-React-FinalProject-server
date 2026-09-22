using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Posts;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Repositories.Posts;

public class PostRepository : IPostRepository
{
    private readonly ThreadsDbContext _dbContext;

    public PostRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<PostSummaryReadModel>> GetRandomAsync(
        int count,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var postIds = await _dbContext.Posts
            .AsNoTracking()
            .OrderBy(_ => EF.Functions.Random())
            .Select(post => post.Id)
            .Take(count)
            .ToListAsync(cancellationToken);

        return await GetByOrderedIdsAsync(
            postIds,
            currentUserId,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<PostSummaryReadModel>> GetByAuthorIdAsync(
        Guid authorId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Posts
            .AsNoTracking()
            .Where(post => post.AuthorId == authorId);

        if (cursor is not null)
        {
            query = query.Where(post => EF.Functions.LessThan(
                ValueTuple.Create(post.CreatedAt, post.Id),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        var pageQuery = query
            .OrderByDescending(post => post.CreatedAt)
            .ThenByDescending(post => post.Id)
            .Take(limit + 1);

        return await ProjectToSummaryReadModel(pageQuery, currentUserId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PostSummaryReadModel>> GetLikedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PostLikes
            .AsNoTracking()
            .Where(like => like.UserId == userId);

        if (cursor is not null)
        {
            query = query.Where(like => EF.Functions.LessThan(
                ValueTuple.Create(like.CreatedAt, like.PostId),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        var likedPosts = await query
            .OrderByDescending(like => like.CreatedAt)
            .ThenByDescending(like => like.PostId)
            .Select(like => new OrderedPostReference
            {
                Id = like.PostId,
                ActionAt = like.CreatedAt
            })
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return await GetByOrderedIdsAsync(likedPosts, currentUserId, cancellationToken);
    }

    private async Task<IReadOnlyCollection<PostSummaryReadModel>> GetByOrderedIdsAsync(
        IReadOnlyCollection<Guid> postIds,
        Guid? currentUserId,
        IReadOnlyDictionary<Guid, DateTimeOffset>? actionTimes = null,
        CancellationToken cancellationToken = default)
    {
        if (postIds.Count == 0)
        {
            return [];
        }

        var posts = await ProjectToSummaryReadModel(
                _dbContext.Posts
                    .AsNoTracking()
                    .Where(post => postIds.Contains(post.Id)),
                currentUserId)
            .ToListAsync(cancellationToken);

        var postOrder = postIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);

        var orderedPosts = posts
            .OrderBy(post => postOrder[post.Id])
            .ToList();

        if (actionTimes is not null)
        {
            foreach (var post in orderedPosts)
            {
                post.ActionAt = actionTimes.GetValueOrDefault(post.Id);
            }
        }

        return orderedPosts;
    }

    private Task<IReadOnlyCollection<PostSummaryReadModel>> GetByOrderedIdsAsync(
        IReadOnlyCollection<OrderedPostReference> postReferences,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var actionTimes = postReferences.ToDictionary(reference => reference.Id, reference => reference.ActionAt);

        return GetByOrderedIdsAsync(
            postReferences.Select(reference => reference.Id).ToArray(),
            currentUserId,
            actionTimes,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<PostSummaryReadModel>> GetBookmarkedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PostBookmarks
            .AsNoTracking()
            .Where(bookmark => bookmark.UserId == userId);

        if (cursor is not null)
        {
            query = query.Where(bookmark => EF.Functions.LessThan(
                ValueTuple.Create(bookmark.CreatedAt, bookmark.PostId),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        var bookmarkedPosts = await query
            .OrderByDescending(bookmark => bookmark.CreatedAt)
            .ThenByDescending(bookmark => bookmark.PostId)
            .Select(bookmark => new OrderedPostReference
            {
                Id = bookmark.PostId,
                ActionAt = bookmark.CreatedAt
            })
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return await GetByOrderedIdsAsync(bookmarkedPosts, currentUserId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PostSummaryReadModel>> GetRepostedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PostReposts
            .AsNoTracking()
            .Where(repost => repost.UserId == userId);

        if (cursor is not null)
        {
            query = query.Where(repost => EF.Functions.LessThan(
                ValueTuple.Create(repost.CreatedAt, repost.PostId),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        var repostedPosts = await query
            .OrderByDescending(repost => repost.CreatedAt)
            .ThenByDescending(repost => repost.PostId)
            .Select(repost => new OrderedPostReference
            {
                Id = repost.PostId,
                ActionAt = repost.CreatedAt
            })
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return await GetByOrderedIdsAsync(repostedPosts, currentUserId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PostSummaryReadModel>> SearchAsync(
        string query,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var posts = _dbContext.Posts
            .AsNoTracking()
            .Where(post =>
                (post.Content != null && EF.Functions.ILike(post.Content, $"%{query}%")) ||
                (post.LocationName != null && EF.Functions.ILike(post.LocationName, $"%{query}%")) ||
                (post.EmbedTitle != null && EF.Functions.ILike(post.EmbedTitle, $"%{query}%")) ||
                (post.Author.Username != null && EF.Functions.ILike(post.Author.Username, $"%{query}%")) ||
                (post.Author.DisplayName != null && EF.Functions.ILike(post.Author.DisplayName, $"%{query}%")));

        if (cursor is not null)
        {
            posts = posts.Where(post => EF.Functions.LessThan(
                ValueTuple.Create(post.CreatedAt, post.Id),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        var pageQuery = posts
            .OrderByDescending(post => post.CreatedAt)
            .ThenByDescending(post => post.Id)
            .Take(limit + 1);

        return await ProjectToSummaryReadModel(pageQuery, currentUserId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await BuildTrackedPostWithRelationsQuery()
            .FirstOrDefaultAsync(post => post.Id == id, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Posts
            .AsNoTracking()
            .AnyAsync(post => post.Id == id, cancellationToken);
    }

    public async Task<PostContentReadModel?> GetContentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Posts
            .AsNoTracking()
            .Where(post => post.Id == id)
            .Select(post => new PostContentReadModel
            {
                Id = post.Id,
                Content = post.Content,
                AuthorId = post.AuthorId,
                Media = post.Media
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
                Poll = post.Poll == null
                    ? null
                    : new PostPollContentReadModel
                    {
                        Id = post.Poll.Id,
                        EndsAt = post.Poll.EndsAt,
                        Options = post.Poll.Options
                            .OrderBy(option => option.Position)
                            .Select(option => new PostPollOptionContentReadModel
                            {
                                Id = option.Id,
                                Text = option.Text,
                                Position = option.Position
                            })
                            .ToList()
                    },
                LocationPlaceId = post.LocationPlaceId,
                LocationName = post.LocationName,
                LocationCountry = post.LocationCountry,
                LocationLatitude = post.LocationLatitude,
                LocationLongitude = post.LocationLongitude,
                EmbedUrl = post.EmbedUrl,
                EmbedTitle = post.EmbedTitle,
                EmbedDescription = post.EmbedDescription,
                EmbedThumbnailUrl = post.EmbedThumbnailUrl,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PostEngagementReadModel?> GetEngagementByIdAsync(
        Guid id,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var hasCurrentUser = currentUserId.HasValue;
        var effectiveCurrentUserId = currentUserId ?? Guid.Empty;

        return await _dbContext.Posts
            .AsNoTracking()
            .Where(post => post.Id == id)
            .Select(post => new PostEngagementReadModel
            {
                UpdatedAt = post.UpdatedAt,
                LikesCount = post.PostLikes.Count,
                CommentsCount = post.Comments.Count,
                RepostsCount = post.PostReposts.Count,
                BookmarksCount = post.PostBookmarks.Count,
                ViewsCount = post.PostViews.Count,
                IsLikedByCurrentUser = hasCurrentUser &&
                    post.PostLikes.Any(like => like.UserId == effectiveCurrentUserId),
                IsRepostedByCurrentUser = hasCurrentUser &&
                    post.PostReposts.Any(repost => repost.UserId == effectiveCurrentUserId),
                IsBookmarkedByCurrentUser = hasCurrentUser &&
                    post.PostBookmarks.Any(bookmark => bookmark.UserId == effectiveCurrentUserId),
                Poll = post.Poll == null
                    ? null
                    : new PostPollEngagementReadModel
                    {
                        TotalVotes = post.Poll.Votes.Count,
                        SelectedOptionId = hasCurrentUser
                            ? post.Poll.Votes
                                .Where(vote => vote.UserId == effectiveCurrentUserId)
                                .Select(vote => (Guid?)vote.PollOptionId)
                                .FirstOrDefault()
                            : null,
                        Options = post.Poll.Options
                            .OrderBy(option => option.Position)
                            .Select(option => new PostPollOptionEngagementReadModel
                            {
                                Id = option.Id,
                                VotesCount = option.Votes.Count
                            })
                            .ToList()
                    }
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int?> RecordViewAsync(Guid id, Guid viewerId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "PostViews" ("PostId", "UserId", "CreatedAt")
            SELECT post."Id", {viewerId}, CURRENT_TIMESTAMP
            FROM "Posts" AS post
            WHERE post."Id" = {id}
            ON CONFLICT ("PostId", "UserId") DO NOTHING
            """,
            cancellationToken);

        var viewsCount = await _dbContext.Posts
            .AsNoTracking()
            .Where(post => post.Id == id)
            .Select(post => (int?)post.PostViews.Count)
            .FirstOrDefaultAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return viewsCount;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetViewCountsAsync(
        IReadOnlyCollection<Guid> postIds,
        CancellationToken cancellationToken = default)
    {
        if (postIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await _dbContext.PostViews
            .AsNoTracking()
            .Where(view => postIds.Contains(view.PostId))
            .GroupBy(view => view.PostId)
            .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);
    }

    public async Task AddAsync(Post post, CancellationToken cancellationToken = default)
    {
        await _dbContext.Posts.AddAsync(post, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Post post, CancellationToken cancellationToken = default)
    {
        _dbContext.Posts.Update(post);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Post post, CancellationToken cancellationToken = default)
    {
        _dbContext.Posts.Remove(post);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<PostSummaryReadModel> ProjectToSummaryReadModel(
        IQueryable<Post> query,
        Guid? currentUserId)
    {
        var hasCurrentUser = currentUserId.HasValue;
        var effectiveCurrentUserId = currentUserId ?? Guid.Empty;

        return query
            .AsSplitQuery()
            .Select(post => new PostSummaryReadModel
            {
                Id = post.Id,
                Content = post.Content,
                Author = new UserSummaryReadModel
                {
                    Id = post.Author.Id,
                    Username = post.Author.Username,
                    DisplayName = post.Author.DisplayName,
                    LocationPlaceId = post.Author.LocationPlaceId,
                    LocationName = post.Author.Location,
                    LocationCountry = post.Author.LocationCountry,
                    LocationLatitude = post.Author.LocationLatitude,
                    LocationLongitude = post.Author.LocationLongitude,
                    AvatarObjectKey = post.Author.AvatarObjectKey,
                    IsVerified = post.Author.IsVerified
                },
                Media = post.Media
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
                Poll = post.Poll == null
                    ? null
                    : new PostPollSummaryReadModel
                    {
                        Id = post.Poll.Id,
                        EndsAt = post.Poll.EndsAt,
                        TotalVotes = post.Poll.Votes.Count,
                        SelectedOptionId = hasCurrentUser
                            ? post.Poll.Votes
                                .Where(vote => vote.UserId == effectiveCurrentUserId)
                                .Select(vote => (Guid?)vote.PollOptionId)
                                .FirstOrDefault()
                            : null,
                        Options = post.Poll.Options
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
                LocationPlaceId = post.LocationPlaceId,
                LocationName = post.LocationName,
                LocationCountry = post.LocationCountry,
                LocationLatitude = post.LocationLatitude,
                LocationLongitude = post.LocationLongitude,
                EmbedUrl = post.EmbedUrl,
                EmbedTitle = post.EmbedTitle,
                EmbedDescription = post.EmbedDescription,
                EmbedThumbnailUrl = post.EmbedThumbnailUrl,
                LikesCount = post.PostLikes.Count,
                CommentsCount = post.Comments.Count,
                RepostsCount = post.PostReposts.Count,
                BookmarksCount = post.PostBookmarks.Count,
                ViewsCount = post.PostViews.Count,
                IsLikedByCurrentUser = hasCurrentUser &&
                    post.PostLikes.Any(like => like.UserId == effectiveCurrentUserId),
                IsRepostedByCurrentUser = hasCurrentUser &&
                    post.PostReposts.Any(repost => repost.UserId == effectiveCurrentUserId),
                IsBookmarkedByCurrentUser = hasCurrentUser &&
                    post.PostBookmarks.Any(bookmark => bookmark.UserId == effectiveCurrentUserId),
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt
            });
    }

    private IQueryable<Post> BuildTrackedPostWithRelationsQuery()
    {
        return IncludePostRelations(_dbContext.Posts);
    }

    private static IQueryable<Post> IncludePostRelations(IQueryable<Post> query)
    {
        return query
            .AsSplitQuery()
            .Include(post => post.Author)
            .Include(post => post.Media)
            .Include(post => post.Comments)
            .Include(post => post.PostLikes)
            .Include(post => post.PostBookmarks)
            .Include(post => post.PostReposts)
            .Include(post => post.Poll)
                .ThenInclude(poll => poll!.Options)
                    .ThenInclude(option => option.Votes)
            .Include(post => post.Poll)
                .ThenInclude(poll => poll!.Votes);
    }

    private sealed class OrderedPostReference
    {
        public Guid Id { get; init; }

        public DateTimeOffset ActionAt { get; init; }
    }
}
