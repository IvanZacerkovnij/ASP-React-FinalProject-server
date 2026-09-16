using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Bookmarks;

public interface IBookmarkRepository
{
    Task<bool> TryAddAsync(PostBookmark bookmark, CancellationToken cancellationToken = default);
    Task<bool> TryAddAsync(CommentBookmark bookmark, CancellationToken cancellationToken = default);
    Task<bool> TryDeletePostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default);
    Task<bool> TryDeleteCommentAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default);
}
