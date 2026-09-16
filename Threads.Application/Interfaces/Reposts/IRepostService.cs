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
    Task<bool> AddCommentRepostAsync(Guid userId, Guid commentId, CancellationToken cancellationToken = default);
    Task<bool> AddPostRepostAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
    Task<bool> RemoveCommentRepostAsync(Guid userId, Guid commentId, CancellationToken cancellationToken = default);
    Task<bool> RemovePostRepostAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
}
