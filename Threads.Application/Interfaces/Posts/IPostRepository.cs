using Threads.Application.DTOs.Posts;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Pagination;
using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Posts;

public interface IPostRepository
{
    Task<IReadOnlyCollection<Post>> GetRandomAsync(int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Post>> GetByAuthorIdAsync(
        Guid authorId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Post>> GetLikedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Post>> GetBookmarkedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Post>> GetRepostedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Post>> SearchAsync(
        string query,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default);
    Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PostReadModel?> GetReadModelByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PostStateReadModel?> GetStateByIdAsync(
        Guid id,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> GetViewCountsAsync(
        IReadOnlyCollection<Guid> postIds,
        CancellationToken cancellationToken = default);
    Task<int?> RecordViewAsync(Guid id, Guid viewerId, CancellationToken cancellationToken = default);
    Task AddAsync(Post post, CancellationToken cancellationToken = default);
    Task UpdateAsync(Post post, CancellationToken cancellationToken = default);
    Task DeleteAsync(Post post, CancellationToken cancellationToken = default);
}
