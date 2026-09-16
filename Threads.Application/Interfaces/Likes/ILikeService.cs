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
    Task<bool> AddCommentLikeAsync(Guid userId, Guid commentId, CancellationToken cancellationToken = default);
    Task<bool> AddPostLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
    Task<bool> RemoveCommentLikeAsync(Guid userId, Guid commentId, CancellationToken cancellationToken = default);
    Task<bool> RemovePostLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
}
