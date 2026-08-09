using Threads.Application.DTOs.Posts;

namespace Threads.Application.Interfaces.Posts;

public interface IPostService
{
    Task<IReadOnlyCollection<PostResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PostResponse>> GetFeedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PostResponse>> GetByAuthorIdAsync(Guid authorId, CancellationToken cancellationToken = default);
    Task<PostResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PostResponse> CreateAsync(Guid authorId, CreatePostRequest request, CancellationToken cancellationToken = default);
    Task<PostResponse?> UpdateAsync(Guid id, UpdatePostRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
