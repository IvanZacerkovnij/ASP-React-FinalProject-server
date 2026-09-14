using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;

namespace Threads.Application.Interfaces.Comments;

public interface ICommentService
{
    Task<CursorPageResponse<CommentResponse>> GetByPostIdAsync(
        Guid postId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<CommentResponse?> GetByIdAsync(
        Guid id,
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
    Task<CommentResponse?> UpdateAsync(
        Guid id,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<CommentResponse?> LikeAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<CommentResponse?> UnlikeAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<CommentResponse?> BookmarkAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<CommentResponse?> UnbookmarkAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<CommentResponse?> RepostAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<CommentResponse?> UnrepostAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<CommentResponse?> ViewAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
