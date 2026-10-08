using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Comments;

public interface ICommentRepository
{
    Task<IReadOnlyCollection<CommentSummaryReadModel>> GetByAuthorIdAsync(
        Guid authorId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CommentSummaryReadModel>> GetByPostIdAsync(
        Guid postId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CommentSummaryReadModel>> GetAncestorsAsync(
        Guid commentId,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CommentSummaryReadModel>> GetRepliesAsync(
        Guid parentCommentId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CommentSummaryReadModel>> GetSummariesByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CommentVersion>> GetVersionsAsync(
        Guid commentId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CommentVersion>> GetVersionsByIdsAsync(
        IReadOnlyCollection<Guid> versionIds,
        CancellationToken cancellationToken = default);

    Task<Comment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CommentSummaryReadModel?> GetSummaryByIdAsync(
        Guid id,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetPostIdByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> VersionExistsAsync(Guid commentId, Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CommentSummaryReadModel>> GetLikedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CommentSummaryReadModel>> GetBookmarkedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CommentSummaryReadModel>> GetRepostedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<int?> RecordViewAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);
    Task AddAsync(Comment comment, CancellationToken cancellationToken = default);
    void RemovePoll(Poll poll);
    Task UpdateAsync(Comment comment, CancellationToken cancellationToken = default);
    Task DeleteAsync(Comment comment, CancellationToken cancellationToken = default);
}
