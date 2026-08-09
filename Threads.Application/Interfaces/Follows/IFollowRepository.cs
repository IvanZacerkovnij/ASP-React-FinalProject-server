using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Follows;

public interface IFollowRepository
{
    Task<Follow?> GetByFollowerAndFollowingAsync(
        Guid followerId,
        Guid followingId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<User>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<User>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(Follow follow, CancellationToken cancellationToken = default);
    Task DeleteAsync(Follow follow, CancellationToken cancellationToken = default);
}
