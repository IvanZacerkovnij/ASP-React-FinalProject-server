using Threads.Application.DTOs.Bookmarks;
using Threads.Application.DTOs.Pagination;

namespace Threads.Application.Interfaces.Bookmarks;

public interface IBookmarkService
{
    Task<UserBookmarksPageResponse> GetByUserIdAsync(
        Guid userId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<bool> AddCommentBookmarkAsync(Guid userId, Guid commentId, CancellationToken cancellationToken = default);
    Task<bool> AddPostBookmarkAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
    Task<bool> RemoveCommentBookmarkAsync(Guid userId, Guid commentId, CancellationToken cancellationToken = default);
    Task<bool> RemovePostBookmarkAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
}
