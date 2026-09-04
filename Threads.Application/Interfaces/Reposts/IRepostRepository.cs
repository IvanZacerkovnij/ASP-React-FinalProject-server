using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Reposts;

public interface IRepostRepository
{
    Task<Repost?> GetByUserAndPostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default);

    Task<Repost?> GetByUserAndCommentAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Repost repost, CancellationToken cancellationToken = default);
    Task DeleteAsync(Repost repost, CancellationToken cancellationToken = default);
}
