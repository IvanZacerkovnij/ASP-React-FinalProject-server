using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Reposts;

public interface IRepostRepository
{
    Task<PostRepost?> GetByUserAndPostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken = default);

    Task<CommentRepost?> GetByUserAndCommentAsync(
        Guid userId,
        Guid commentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(PostRepost repost, CancellationToken cancellationToken = default);
    Task AddAsync(CommentRepost repost, CancellationToken cancellationToken = default);
    Task DeleteAsync(PostRepost repost, CancellationToken cancellationToken = default);
    Task DeleteAsync(CommentRepost repost, CancellationToken cancellationToken = default);
}
