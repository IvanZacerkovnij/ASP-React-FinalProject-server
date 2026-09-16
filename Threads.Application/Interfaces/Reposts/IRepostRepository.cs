using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Reposts;

public interface IRepostRepository
{
    Task<bool> TryAddAsync(PostRepost repost, CancellationToken cancellationToken = default);
    Task<bool> TryAddAsync(CommentRepost repost, CancellationToken cancellationToken = default);
    Task<bool> TryDeletePostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default);
    Task<bool> TryDeleteCommentAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default);
}
