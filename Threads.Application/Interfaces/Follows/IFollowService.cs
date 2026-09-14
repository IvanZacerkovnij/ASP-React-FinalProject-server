using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Users;

namespace Threads.Application.Interfaces.Follows;

public interface IFollowService
{
    Task<bool> AddFollowAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default);
    Task<bool> RemoveFollowAsync(Guid followerId, Guid followingId, CancellationToken cancellationToken = default);
    Task<CursorPageResponse<UserShortResponse>> GetFollowersAsync(
        Guid userId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default);
    Task<CursorPageResponse<UserShortResponse>> GetFollowingAsync(
        Guid userId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default);
}
