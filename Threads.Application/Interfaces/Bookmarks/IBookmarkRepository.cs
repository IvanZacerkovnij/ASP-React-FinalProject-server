using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Bookmarks;

public interface IBookmarkRepository
{
    Task<PostBookmark?> GetByUserAndPostId(Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default);

    Task<CommentBookmark?> GetByUserAndCommentId(Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default);
    
    Task AddAsync(PostBookmark bookmark, CancellationToken cancellationToken = default);
    Task AddAsync(CommentBookmark bookmark, CancellationToken cancellationToken = default);
    Task DeleteAsync(PostBookmark bookmark, CancellationToken cancellationToken = default);
    Task DeleteAsync(CommentBookmark bookmark, CancellationToken cancellationToken = default);
}
