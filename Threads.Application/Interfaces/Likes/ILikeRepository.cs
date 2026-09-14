using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Likes;

public interface ILikeRepository
{
    Task<PostLike?> GetByUserAndPostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default);

    Task<CommentLike?> GetByUserAndCommentAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(PostLike like, CancellationToken cancellationToken = default);
    Task AddAsync(CommentLike like, CancellationToken cancellationToken = default);
    Task DeleteAsync(PostLike like, CancellationToken cancellationToken = default);
    Task DeleteAsync(CommentLike like, CancellationToken cancellationToken = default);
}
