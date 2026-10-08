using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.ScheduledPosts;

public interface IScheduledPostRepository
{
    Task<IReadOnlyCollection<ScheduledPost>> GetByAuthorIdAsync(
        Guid authorId,
        CancellationToken cancellationToken = default);
    Task<ScheduledPost?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ScheduledPost>> GetDueAsync(
        DateTimeOffset dueAt,
        int limit,
        CancellationToken cancellationToken = default);
    Task AddAsync(ScheduledPost scheduledPost, CancellationToken cancellationToken = default);
    Task UpdateAsync(ScheduledPost scheduledPost, CancellationToken cancellationToken = default);
    Task DeleteAsync(ScheduledPost scheduledPost, CancellationToken cancellationToken = default);
    Task PublishAsync(
        ScheduledPost scheduledPost,
        Post post,
        CancellationToken cancellationToken = default);
}
