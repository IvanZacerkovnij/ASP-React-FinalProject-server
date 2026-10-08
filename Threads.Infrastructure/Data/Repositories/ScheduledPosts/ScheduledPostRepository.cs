using Microsoft.EntityFrameworkCore;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.ScheduledPosts;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Repositories.ScheduledPosts;

public sealed class ScheduledPostRepository : IScheduledPostRepository
{
    private readonly ThreadsDbContext _dbContext;

    public ScheduledPostRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<ScheduledPost>> GetByAuthorIdAsync(
        Guid authorId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ScheduledPosts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(scheduledPost => scheduledPost.Media.OrderBy(media => media.SortOrder))
            .Where(scheduledPost => scheduledPost.AuthorId == authorId)
            .OrderBy(scheduledPost => scheduledPost.ScheduledAt)
            .ThenBy(scheduledPost => scheduledPost.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<ScheduledPost?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ScheduledPosts
            .AsSplitQuery()
            .Include(scheduledPost => scheduledPost.Media.OrderBy(media => media.SortOrder))
            .FirstOrDefaultAsync(scheduledPost => scheduledPost.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ScheduledPost>> GetDueAsync(
        DateTimeOffset dueAt,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ScheduledPosts
            .AsSplitQuery()
            .Include(scheduledPost => scheduledPost.Media.OrderBy(media => media.SortOrder))
            .Where(scheduledPost => scheduledPost.ScheduledAt <= dueAt &&
                                    scheduledPost.Author.IsActive)
            .OrderBy(scheduledPost => scheduledPost.ScheduledAt)
            .ThenBy(scheduledPost => scheduledPost.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        ScheduledPost scheduledPost,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.ScheduledPosts.AddAsync(scheduledPost, cancellationToken);
        await SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(
        ScheduledPost scheduledPost,
        CancellationToken cancellationToken = default)
    {
        if (_dbContext.Entry(scheduledPost).State == EntityState.Detached)
        {
            _dbContext.ScheduledPosts.Update(scheduledPost);
        }

        return SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(
        ScheduledPost scheduledPost,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ScheduledPosts.Remove(scheduledPost);
        return SaveChangesAsync(cancellationToken);
    }

    public async Task PublishAsync(
        ScheduledPost scheduledPost,
        Post post,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Posts.AddAsync(post, cancellationToken);
        _dbContext.ScheduledPosts.Remove(scheduledPost);
        await SaveChangesAsync(cancellationToken);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Scheduled post was modified by another request.");
        }
    }
}
