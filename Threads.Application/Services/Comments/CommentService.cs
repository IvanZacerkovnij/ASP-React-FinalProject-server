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

    public Task<CommentResponse> UpdateAsync(
        Guid id,
        Guid currentUserId,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        return _commentManagementService.UpdateAsync(
            id,
            currentUserId,
            request,
            cancellationToken);
    }

    public Task<CommentViewResponse?> RecordViewAsync(
        Guid id,
        Guid viewerId,
        CancellationToken cancellationToken = default)
    {
        return _commentInteractionService.RecordViewAsync(id, viewerId, cancellationToken);
    }

    public Task DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return _commentManagementService.DeleteAsync(id, currentUserId, cancellationToken);
    }
}
