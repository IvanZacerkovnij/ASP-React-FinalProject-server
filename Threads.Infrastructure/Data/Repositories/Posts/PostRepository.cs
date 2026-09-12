using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Posts.Models;
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

    public async Task<IReadOnlyCollection<Post>> GetRandomAsync(int count, CancellationToken cancellationToken = default)
    {
        var postIds = await _dbContext.Posts
            .AsNoTracking()
            .OrderBy(_ => EF.Functions.Random())
            .Select(post => post.Id)
            .Take(count)
            .ToListAsync(cancellationToken);

        if (postIds.Count == 0)
        {
            return [];
        }

        var posts = await BuildPostQuery(trackChanges: false)
            .Where(post => postIds.Contains(post.Id))
            .ToListAsync(cancellationToken);

        var postOrder = postIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);

        return posts
            .OrderBy(post => postOrder[post.Id])
            .ToList();
    }

    public async Task<IReadOnlyCollection<Post>> GetByAuthorIdAsync(Guid authorId, CancellationToken cancellationToken = default)
    {
        return await BuildPostQuery(trackChanges: false)
            .Where(post => post.AuthorId == authorId)
            .OrderByDescending(post => post.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Post>> GetLikedByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var likedPostIds = await _dbContext.Likes
            .AsNoTracking()
            .Where(like => like.UserId == userId && like.PostId.HasValue && like.CommentId == null)
            .OrderByDescending(like => like.CreatedAt)
            .Select(like => like.PostId!.Value)
            .ToListAsync(cancellationToken);

        if (likedPostIds.Count == 0)
        {
            return [];
        }

        var posts = await BuildPostQuery(trackChanges: false)
            .Where(post => likedPostIds.Contains(post.Id))
            .ToListAsync(cancellationToken);

        var postOrder = likedPostIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);

        return posts
            .OrderBy(post => postOrder[post.Id])
            .ToList();
    }

    public async Task<IReadOnlyCollection<Post>> GetBookmarkedByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var bookmarkedPostIds = await _dbContext.Bookmarks
            .AsNoTracking()
            .Where(bookmark => bookmark.UserId == userId && bookmark.PostId.HasValue && bookmark.CommentId == null)
            .OrderByDescending(bookmark => bookmark.CreatedAt)
            .Select(bookmark => bookmark.PostId!.Value)
            .ToListAsync(cancellationToken);

        if (bookmarkedPostIds.Count == 0)
        {
            return [];
        }

        var posts = await BuildPostQuery(trackChanges: false)
            .Where(post => bookmarkedPostIds.Contains(post.Id))
            .ToListAsync(cancellationToken);

        var postOrder = bookmarkedPostIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);

        return posts
            .OrderBy(post => postOrder[post.Id])
            .ToList();
    }

    public async Task<IReadOnlyCollection<Post>> GetRepostedByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var repostedPostIds = await _dbContext.Reposts
            .AsNoTracking()
            .Where(repost => repost.UserId == userId && repost.PostId.HasValue && repost.CommentId == null)
            .OrderByDescending(repost => repost.CreatedAt)
            .Select(repost => repost.PostId!.Value)
            .ToListAsync(cancellationToken);

        repostedPostIds = repostedPostIds
            .Distinct()
            .ToList();

        if (repostedPostIds.Count == 0)
        {
            return [];
        }

        var posts = await BuildPostQuery(trackChanges: false)
            .Where(post => repostedPostIds.Contains(post.Id))
            .ToListAsync(cancellationToken);

        var postOrder = repostedPostIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);

        return posts
            .OrderBy(post => postOrder[post.Id])
            .ToList();
    }

    public async Task<IReadOnlyCollection<Post>> SearchAsync(
        string query,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await BuildPostQuery(trackChanges: false)
            .Where(post =>
                (post.Content != null && EF.Functions.ILike(post.Content, $"%{query}%")) ||
                (post.LocationName != null && EF.Functions.ILike(post.LocationName, $"%{query}%")) ||
                (post.EmbedTitle != null && EF.Functions.ILike(post.EmbedTitle, $"%{query}%")) ||
                (post.Author.Username != null && EF.Functions.ILike(post.Author.Username, $"%{query}%")) ||
                (post.Author.DisplayName != null && EF.Functions.ILike(post.Author.DisplayName, $"%{query}%")))
            .OrderByDescending(post => post.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await BuildPostQuery(trackChanges: true)
            .FirstOrDefaultAsync(post => post.Id == id, cancellationToken);
    }

    public async Task<PostReadModel?> GetReadModelByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Posts
            .AsNoTracking()
            .Where(post => post.Id == id)
            .Select(post => new PostReadModel
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
                    : new PostPollReadModel
                    {
                        Id = post.Poll.Id,
                        EndsAt = post.Poll.EndsAt,
                        Options = post.Poll.Options
                            .OrderBy(option => option.Position)
                            .Select(option => new PostPollOptionReadModel
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

    public async Task<PostStateReadModel?> GetStateByIdAsync(
        Guid id,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var hasCurrentUser = currentUserId.HasValue;
        var effectiveCurrentUserId = currentUserId ?? Guid.Empty;

        return await _dbContext.Posts
            .AsNoTracking()
            .Where(post => post.Id == id)
            .Select(post => new PostStateReadModel
            {
                UpdatedAt = post.UpdatedAt,
                LikesCount = post.Likes.Count,
                CommentsCount = post.Comments.Count,
                RepostsCount = post.Reposts.Count,
                BookmarksCount = post.Bookmarks.Count,
                ViewsCount = post.ViewsCount,
                IsLikedByCurrentUser = hasCurrentUser &&
                    post.Likes.Any(like => like.UserId == effectiveCurrentUserId),
                IsRepostedByCurrentUser = hasCurrentUser &&
                    post.Reposts.Any(repost => repost.UserId == effectiveCurrentUserId),
                IsBookmarkedByCurrentUser = hasCurrentUser &&
                    post.Bookmarks.Any(bookmark => bookmark.UserId == effectiveCurrentUserId),
                Poll = post.Poll == null
                    ? null
                    : new PostPollStateReadModel
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
                            .Select(option => new PostPollOptionStateReadModel
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
        var post = await _dbContext.Posts
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (post is null)
        {
            return null;
        }

        var alreadyViewed = await _dbContext.Views
            .AsNoTracking()
            .AnyAsync(
                item => item.PostId == id && item.ViewerId == viewerId && item.CommentId == null,
                cancellationToken);

        if (alreadyViewed)
        {
            return post.ViewsCount;
        }

        await _dbContext.Views.AddAsync(
            new View
            {
                PostId = id,
                CommentId = null,
                ViewerId = viewerId
            },
            cancellationToken);

        post.ViewsCount++;
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var persistedPost = await _dbContext.Posts
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (persistedPost is not null)
            {
                return persistedPost.ViewsCount;
            }

            throw;
        }

        return post.ViewsCount;
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

    private IQueryable<Post> BuildPostQuery(bool trackChanges)
    {
        var query = trackChanges
            ? _dbContext.Posts.AsQueryable()
            : _dbContext.Posts.AsNoTracking();

        return query
            .AsSplitQuery()
            .Include(post => post.Author)
            .Include(post => post.Media)
            .Include(post => post.Comments)
            .Include(post => post.Likes)
            .Include(post => post.Bookmarks)
            .Include(post => post.Reposts)
            .Include(post => post.Poll)
                .ThenInclude(poll => poll!.Options)
                    .ThenInclude(option => option.Votes)
            .Include(post => post.Poll)
                .ThenInclude(poll => poll!.Votes);
    }
}
