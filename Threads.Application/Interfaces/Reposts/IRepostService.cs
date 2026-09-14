using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Reposts;

namespace Threads.Application.Interfaces.Reposts;

public interface IRepostService
{
    Task<UserRepostsPageResponse> GetByUserIdAsync(
        Guid userId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<bool> AddRepostAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
    Task<bool> RemoveRepostAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
}
