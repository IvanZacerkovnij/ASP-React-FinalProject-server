using Microsoft.EntityFrameworkCore;
using Threads.Application.Interfaces.Posts;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Repositories.Posts;

public class PostRepository : IPostRepository
{
    private readonly ThreadsDbContext _dbContext;

    public PostRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<Post>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await BuildPostQuery(trackChanges: false)
            .OrderByDescending(post => post.CreatedAt)
            .ToListAsync(cancellationToken);
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
            .Where(like => like.UserId == userId)
            .OrderByDescending(like => like.CreatedAt)
            .Select(like => like.PostId)
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

    public async Task<int?> RecordViewAsync(Guid id, Guid viewerId, CancellationToken cancellationToken = default)
    {
        var post = await _dbContext.Posts
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (post is null)
        {
            return null;
        }

        var alreadyViewed = await _dbContext.PostViews
            .AsNoTracking()
            .AnyAsync(
                item => item.PostId == id && item.ViewerId == viewerId,
                cancellationToken);

        if (alreadyViewed)
        {
            return post.ViewsCount;
        }

        await _dbContext.PostViews.AddAsync(
            new PostView
            {
                PostId = id,
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
            .Include(post => post.Poll)
                .ThenInclude(poll => poll!.Options)
                    .ThenInclude(option => option.Votes)
            .Include(post => post.Poll)
                .ThenInclude(poll => poll!.Votes);
    }
}
