using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Bookmarks;

public interface IBookmarkRepository
{
    Task<Bookmark?> GetByUserAndPostId(Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default);
    
    Task AddAsync(Bookmark bookmark, CancellationToken cancellationToken = default);
    Task DeleteAsync(Bookmark bookmark, CancellationToken cancellationToken = default);
}