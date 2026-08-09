using Threads.Application.DTOs.Users;

namespace Threads.Application.Interfaces.Follows;

public interface IFollowService
{
    Task<bool> AddFollowAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default);
    Task<bool> RemoveFollowAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UserShortResponse>> GetFollowersAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UserShortResponse>> GetFollowingAsync(Guid userId, CancellationToken cancellationToken = default);
}
