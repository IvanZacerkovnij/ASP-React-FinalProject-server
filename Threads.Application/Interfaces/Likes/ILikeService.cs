using Threads.Application.DTOs.Likes;
using Threads.Application.DTOs.Pagination;

namespace Threads.Application.Interfaces.Likes;

public interface ILikeService
{
    Task<UserLikesPageResponse> GetByUserIdAsync(
        Guid userId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<bool> AddLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
    Task<bool> RemoveLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
}
