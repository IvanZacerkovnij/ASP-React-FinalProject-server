namespace Threads.Application.Interfaces.Bookmarks;

public interface IBookmarkService
{
    Task<bool> AddBookmarkAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
    Task<bool> RemoveBookmarkAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
}