using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Interfaces.Comments;

namespace Threads.Application.Services.Comments;

public sealed class CommentService : ICommentService
{
    private readonly CommentQueryService _commentQueryService;
    private readonly CommentManagementService _commentManagementService;
    private readonly CommentInteractionService _commentInteractionService;

    public CommentService(
        CommentQueryService commentQueryService,
        CommentManagementService commentManagementService,
        CommentInteractionService commentInteractionService)
    {
        _commentQueryService = commentQueryService;
        _commentManagementService = commentManagementService;
        _commentInteractionService = commentInteractionService;
    }

    public Task<CursorPageResponse<CommentResponse>> GetByPostIdAsync(
        Guid postId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _commentQueryService.GetByPostIdAsync(
            postId,
            pagination,
            cancellationToken,
            currentUserId);
    }

    public Task<CommentResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _commentQueryService.GetByIdAsync(id, cancellationToken, currentUserId);
    }

    public Task<IReadOnlyCollection<CommentResponse>> GetLikedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _commentQueryService.GetLikedByUserIdAsync(
            userId,
            limit,
            cursor,
            cancellationToken,
            currentUserId);
    }

    public Task<IReadOnlyCollection<CommentResponse>> GetBookmarkedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _commentQueryService.GetBookmarkedByUserIdAsync(
            userId,
            limit,
            cursor,
            cancellationToken,
            currentUserId);
    }

    public Task<IReadOnlyCollection<CommentResponse>> GetRepostedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _commentQueryService.GetRepostedByUserIdAsync(
            userId,
            limit,
            cursor,
            cancellationToken,
            currentUserId);
    }

    public Task<CommentResponse> CreateAsync(
        Guid authorId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        return _commentManagementService.CreateAsync(authorId, request, cancellationToken);
    }

    public Task<CommentResponse?> UpdateAsync(
        Guid id,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _commentManagementService.UpdateAsync(
            id,
            request,
            cancellationToken,
            currentUserId);
    }

    public Task<CommentResponse?> LikeAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _commentInteractionService.LikeAsync(id, userId, cancellationToken);
    }

    public Task<CommentResponse?> UnlikeAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _commentInteractionService.UnlikeAsync(id, userId, cancellationToken);
    }

    public Task<CommentResponse?> BookmarkAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _commentInteractionService.BookmarkAsync(id, userId, cancellationToken);
    }

    public Task<CommentResponse?> UnbookmarkAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _commentInteractionService.UnbookmarkAsync(id, userId, cancellationToken);
    }

    public Task<CommentResponse?> RepostAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _commentInteractionService.RepostAsync(id, userId, cancellationToken);
    }

    public Task<CommentResponse?> UnrepostAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _commentInteractionService.UnrepostAsync(id, userId, cancellationToken);
    }

    public Task<CommentResponse?> ViewAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _commentInteractionService.ViewAsync(id, userId, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _commentManagementService.DeleteAsync(id, cancellationToken);
    }
}
