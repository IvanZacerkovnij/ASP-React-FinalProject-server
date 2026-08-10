using Threads.Application.DTOs.Posts;

namespace Threads.Application.Interfaces.Posts;

public interface IPostService
{
    Task<IReadOnlyCollection<PostResponse>> GetAllAsync(CancellationToken cancellationToken = default, Guid? currentUserId = null);
    Task<IReadOnlyCollection<PostResponse>> GetFeedAsync(CancellationToken cancellationToken = default, Guid? currentUserId = null);
    Task<IReadOnlyCollection<PostResponse>> GetByAuthorIdAsync(Guid authorId, CancellationToken cancellationToken = default, Guid? currentUserId = null);
    Task<IReadOnlyCollection<PostResponse>> GetLikedByUserIdAsync(Guid userId, CancellationToken cancellationToken = default, Guid? currentUserId = null);
    Task<IReadOnlyCollection<PostResponse>> GetRepostedByUserIdAsync(Guid userId, CancellationToken cancellationToken = default, Guid? currentUserId = null);
    Task<IReadOnlyCollection<PostResponse>> SearchAsync(string query, CancellationToken cancellationToken = default, Guid? currentUserId = null);
    Task<PostResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, Guid? currentUserId = null);
    Task<PostResponse> CreateAsync(Guid authorId, CreatePostRequest request, CancellationToken cancellationToken = default);
    Task<PostResponse?> UpdateAsync(Guid id, UpdatePostRequest request, CancellationToken cancellationToken = default);
    Task<PostViewResponse?> RecordViewAsync(Guid id, Guid viewerId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
