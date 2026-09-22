using Threads.Application.DTOs.Pagination;
using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Follows;

public interface IFollowRepository
{
    Task<Follow?> GetByFollowerAndFollowingAsync(
        Guid followerId,
        Guid followingId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Follow>> GetFollowersAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Follow>> GetFollowingAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddAsync(Follow follow, CancellationToken cancellationToken = default);
    Task DeleteAsync(Follow follow, CancellationToken cancellationToken = default);
}
