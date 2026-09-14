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
    Task<bool> AddBookmarkAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
    Task<bool> RemoveBookmarkAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
}
