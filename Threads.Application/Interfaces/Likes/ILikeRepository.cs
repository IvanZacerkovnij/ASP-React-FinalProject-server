using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Likes;

public interface ILikeRepository
{
    Task<Like?> GetByUserAndPostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default);

    Task<Like?> GetByUserAndCommentAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Like like, CancellationToken cancellationToken = default);
    Task DeleteAsync(Like like, CancellationToken cancellationToken = default);
}
