using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Likes;

public interface ILikeRepository
{
    Task<bool> TryAddAsync(PostLike like, CancellationToken cancellationToken = default);
    Task<bool> TryAddAsync(CommentLike like, CancellationToken cancellationToken = default);
    Task<bool> TryDeletePostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default);
    Task<bool> TryDeleteCommentAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default);
}
