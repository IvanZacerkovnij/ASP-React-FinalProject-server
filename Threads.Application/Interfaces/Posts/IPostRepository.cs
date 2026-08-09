using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Posts;

public interface IPostRepository
{
    Task<IReadOnlyCollection<Post>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Post>> GetByAuthorIdAsync(Guid authorId, CancellationToken cancellationToken = default);
    Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Post post, CancellationToken cancellationToken = default);
    Task UpdateAsync(Post post, CancellationToken cancellationToken = default);
    Task DeleteAsync(Post post, CancellationToken cancellationToken = default);
}
