using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Comments;

public interface ICommentRepository
{
    Task<IReadOnlyCollection<Comment>> GetByPostIdAsync(
        Guid postId,
        CancellationToken cancellationToken = default);

    Task<Comment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    void AttachView(View view);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Comment comment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Comment comment, CancellationToken cancellationToken = default);
    Task DeleteAsync(Comment comment, CancellationToken cancellationToken = default);
}
