using Threads.Application.DTOs.Posts;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Pagination;
using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Posts;

public interface IPostRepository
{
    Task<IReadOnlyCollection<PostSummaryReadModel>> GetRandomAsync(
        int count,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PostSummaryReadModel>> GetByAuthorIdAsync(
        Guid authorId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PostSummaryReadModel>> GetLikedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PostSummaryReadModel>> GetBookmarkedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PostSummaryReadModel>> GetRepostedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PostSummaryReadModel>> SearchAsync(
        string query,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PostContentReadModel?> GetContentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PostEngagementReadModel?> GetEngagementByIdAsync(
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
