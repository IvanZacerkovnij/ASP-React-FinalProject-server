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
        return await _dbContext.Posts
            .AsNoTracking()
            .Include(post => post.Author)
            .Include(post => post.Media)
            .Include(post => post.Comments)
            .Include(post => post.Likes)
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

        var posts = await _dbContext.Posts
            .AsNoTracking()
            .Where(post => postIds.Contains(post.Id))
            .Include(post => post.Author)
            .Include(post => post.Media)
            .Include(post => post.Comments)
            .Include(post => post.Likes)
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
        return await _dbContext.Posts
            .AsNoTracking()
            .Where(post => post.AuthorId == authorId)
            .Include(post => post.Author)
            .Include(post => post.Media)
            .Include(post => post.Comments)
            .Include(post => post.Likes)
            .OrderByDescending(post => post.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Posts
            .Include(post => post.Author)
            .Include(post => post.Media)
            .Include(post => post.Comments)
            .Include(post => post.Likes)
            .FirstOrDefaultAsync(post => post.Id == id, cancellationToken);
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
}
