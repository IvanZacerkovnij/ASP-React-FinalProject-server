using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Versions;

namespace Threads.Application.Interfaces.Comments;

public interface ICommentService
{
    Task<CursorPageResponse<CommentResponse>> GetByAuthorIdAsync(
        Guid authorId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<CursorPageResponse<CommentResponse>> GetByPostIdAsync(
        Guid postId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<CommentResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<CommentThreadResponse?> GetThreadAsync(
        Guid id,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<EditHistoryResponse<CommentResponse>?> GetEditHistoryAsync(
        Guid id,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<IReadOnlyCollection<CommentResponse>> GetLikedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<IReadOnlyCollection<CommentResponse>> GetBookmarkedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<IReadOnlyCollection<CommentResponse>> GetRepostedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<CommentResponse> CreateAsync(Guid authorId, CreateCommentRequest request, CancellationToken cancellationToken = default);
    Task<CommentResponse> UpdateAsync(
        Guid id,
        Guid currentUserId,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default);
    Task<CommentViewResponse?> RecordViewAsync(
        Guid id,
        Guid viewerId,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken = default);
}
