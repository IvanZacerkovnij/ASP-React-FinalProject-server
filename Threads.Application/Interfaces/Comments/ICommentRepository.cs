using Threads.Application.DTOs.Pagination;
using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Comments;

public interface ICommentRepository
{
    Task<IReadOnlyCollection<Comment>> GetByPostIdAsync(
        Guid postId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default);

    Task<Comment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        bool trackChanges = true);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Comment>> GetLikedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Comment>> GetBookmarkedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Comment>> GetRepostedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> GetViewCountsAsync(
        IReadOnlyCollection<Guid> commentIds,
        CancellationToken cancellationToken = default);
    Task<int?> RecordViewAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);
    Task AddAsync(Comment comment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Comment comment, CancellationToken cancellationToken = default);
    Task DeleteAsync(Comment comment, CancellationToken cancellationToken = default);
}
