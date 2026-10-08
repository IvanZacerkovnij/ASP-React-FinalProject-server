using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Quotes;
using Threads.Application.DTOs.Users;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Posts;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Repositories.Posts;

public class PostRepository : IPostRepository
{
    private const double EarthRadiusKilometers = 6371d;
    private const double NearRadiusKilometers = 50d;
    private const double DegreesToRadians = Math.PI / 180d;

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

    public Task<IReadOnlyCollection<PostSummaryReadModel>> GetSummariesByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        return GetByOrderedIdsAsync(
            ids.Distinct().ToArray(),
            currentUserId,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<PostVersion>> GetVersionsAsync(
        Guid postId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PostVersions
            .AsNoTracking()
            .Where(version => version.PostId == postId);

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

    public async Task<IReadOnlyCollection<PostVersion>> GetVersionsByIdsAsync(
        IReadOnlyCollection<Guid> versionIds,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = versionIds.Distinct().ToArray();

        if (distinctIds.Length == 0)
        {
            return [];
        }

        return await _dbContext.PostVersions
            .AsNoTracking()
            .Where(version => distinctIds.Contains(version.Id))
            .ToListAsync(cancellationToken);
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
        return await SearchAsync(
            query,
            null,
            null,
            null,
            [],
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            limit,
            cursor,
            currentUserId,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<PostSummaryReadModel>> SearchAsync(
        string? query,
        string? people,
        string? location,
        string? exactPhrase,
        IReadOnlyCollection<string> anyWords,
        IReadOnlyCollection<string> excludeWords,
        string? from,
        int? minReplies,
        int? minLikes,
        int? minReposts,
        DateOnly? fromDate,
        DateOnly? toDate,
        bool? hasMedia,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var posts = _dbContext.Posts
            .AsNoTracking();

        if (query is not null)
        {
            var matchingAuthorIds = _dbContext.Users
                .AsNoTracking()
                .Where(user => EF
                    .Property<NpgsqlTsVector>(user, PostgresSearch.VectorProperty)
                    .Matches(EF.Functions.WebSearchToTsQuery(
                        PostgresSearch.Configuration,
                        query)))
                .Select(user => user.Id);
            posts = posts.Where(post =>
                EF.Property<NpgsqlTsVector>(post, PostgresSearch.VectorProperty)
                    .Matches(EF.Functions.WebSearchToTsQuery(
                        PostgresSearch.Configuration,
                        query)) ||
                matchingAuthorIds.Contains(post.AuthorId));
        }

        if (people == "following")
        {
            if (!currentUserId.HasValue)
            {
                return [];
            }

            posts = posts.Where(post => _dbContext.Follows.Any(follow =>
                follow.FollowerId == currentUserId.Value &&
                follow.FollowingId == post.AuthorId));
        }

        if (location == "near")
        {
            if (!currentUserId.HasValue)
            {
                return [];
            }

            posts = posts.Where(post =>
                post.LocationLatitude.HasValue &&
                post.LocationLongitude.HasValue &&
                _dbContext.Users.Any(currentUser =>
                    currentUser.Id == currentUserId.Value &&
                    currentUser.LocationLatitude.HasValue &&
                    currentUser.LocationLongitude.HasValue &&
                    2d * EarthRadiusKilometers * Math.Asin(Math.Sqrt(
                        Math.Pow(Math.Sin(
                            (post.LocationLatitude.Value - currentUser.LocationLatitude.Value) *
                            DegreesToRadians / 2d), 2d) +
                        Math.Cos(currentUser.LocationLatitude.Value * DegreesToRadians) *
                        Math.Cos(post.LocationLatitude.Value * DegreesToRadians) *
                        Math.Pow(Math.Sin(
                            (post.LocationLongitude.Value - currentUser.LocationLongitude.Value) *
                            DegreesToRadians / 2d), 2d))) <= NearRadiusKilometers));
        }

        if (exactPhrase is not null)
        {
            var normalizedPhrase = exactPhrase.ToLowerInvariant();
            posts = posts.Where(post =>
                post.Content != null && post.Content.ToLower().Contains(normalizedPhrase));
        }

        foreach (var word in excludeWords)
        {
            var excludedWord = word;
            posts = posts.Where(post =>
                post.Content == null || !post.Content.ToLower().Contains(excludedWord));
        }

        if (from is not null)
        {
            posts = posts.Where(post => post.Author.Username == from);
        }

        if (minReplies.HasValue)
        {
            posts = posts.Where(post => post.Comments.Count >= minReplies.Value);
        }

        if (minLikes.HasValue)
        {
            posts = posts.Where(post => post.PostLikes.Count >= minLikes.Value);
        }

        if (minReposts.HasValue)
        {
            posts = posts.Where(post => post.PostReposts.Count >= minReposts.Value);
        }

        if (fromDate.HasValue)
        {
            var fromUtc = new DateTimeOffset(
                fromDate.Value.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);
            posts = posts.Where(post => post.CreatedAt >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtcExclusive = new DateTimeOffset(
                toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);
            posts = posts.Where(post => post.CreatedAt < toUtcExclusive);
        }

        if (hasMedia.HasValue)
        {
            posts = hasMedia.Value
                ? posts.Where(post => post.Media.Any())
                : posts.Where(post => !post.Media.Any());
        }

        if (anyWords.Count > 0)
        {
            IQueryable<Post>? matchingWords = null;
            foreach (var word in anyWords)
            {
                var includedWord = word;
                var wordQuery = posts.Where(post =>
                    post.Content != null && post.Content.ToLower().Contains(includedWord));
                matchingWords = matchingWords is null
                    ? wordQuery
                    : matchingWords.Union(wordQuery);
            }

            posts = matchingWords!;
        }

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

    public Task<bool> VersionExistsAsync(Guid postId, Guid versionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PostVersions
            .AsNoTracking()
            .AnyAsync(
                version => version.PostId == postId &&
                           version.Id == versionId,
                cancellationToken);
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
                VersionId = post.CurrentVersionId,
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
                Quote = post.Quote == null
                    ? null
                    : new QuoteReadModel
                    {
                        TargetType = post.Quote.TargetType,
                        TargetId = post.Quote.TargetId,
                        TargetVersionId = post.Quote.TargetVersionId
                    },
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
        await ConcurrencySaveChanges.SaveAsync(
            _dbContext,
            "post",
            cancellationToken);
    }

    public async Task UpdateAsync(Post post, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Entry(post).State == EntityState.Detached)
        {
            _dbContext.Posts.Update(post);
        }

        var currentVersion = post.Versions.SingleOrDefault(version =>
            version.Id == post.CurrentVersionId);

        if (currentVersion is not null &&
            _dbContext.Entry(currentVersion).State != EntityState.Added)
        {
            await _dbContext.PostVersions.AddAsync(currentVersion, cancellationToken);
        }

        await ConcurrencySaveChanges.SaveAsync(
            _dbContext,
            "post",
            cancellationToken);
    }

    public async Task DeleteAsync(Post post, CancellationToken cancellationToken = default)
    {
        post.DeletedAt ??= DateTimeOffset.UtcNow;
        post.UpdatedAt = DateTimeOffset.UtcNow;
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
                VersionId = post.CurrentVersionId,
                Content = post.Content,
                Author = new UserSummaryReadModel
                {
                    Id = post.Author.Id,
                    Username = post.Author.Username,
                    DisplayName = post.Author.DisplayName,
                    Bio = post.Author.Bio,
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
                Quote = post.Quote == null
                    ? null
                    : new QuoteReadModel
                    {
                        TargetType = post.Quote.TargetType,
                        TargetId = post.Quote.TargetId,
                        TargetVersionId = post.Quote.TargetVersionId
                    },
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
            .Include(post => post.Quote)
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
