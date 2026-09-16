using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.Interfaces.Posts;

namespace Threads.Application.Services.Posts;

public sealed class PostService : IPostService
{
    private readonly PostQueryService _postQueryService;
    private readonly PostManagementService _postManagementService;
    private readonly PostInteractionService _postInteractionService;

    public PostService(
        PostQueryService postQueryService,
        PostManagementService postManagementService,
        PostInteractionService postInteractionService)
    {
        _postQueryService = postQueryService;
        _postManagementService = postManagementService;
        _postInteractionService = postInteractionService;
    }

    public Task<IReadOnlyCollection<PostResponse>> GetFeedAsync(
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _postQueryService.GetFeedAsync(cancellationToken, currentUserId);
    }

    public Task<CursorPageResponse<PostResponse>> GetByAuthorIdAsync(
        Guid authorId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _postQueryService.GetByAuthorIdAsync(
            authorId,
            pagination,
            cancellationToken,
            currentUserId);
    }

    public Task<IReadOnlyCollection<PostResponse>> GetLikedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _postQueryService.GetLikedByUserIdAsync(
            userId,
            limit,
            cursor,
            cancellationToken,
            currentUserId);
    }

    public Task<IReadOnlyCollection<PostResponse>> GetBookmarkedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _postQueryService.GetBookmarkedByUserIdAsync(
            userId,
            limit,
            cursor,
            cancellationToken,
            currentUserId);
    }

    public Task<IReadOnlyCollection<PostResponse>> GetRepostedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _postQueryService.GetRepostedByUserIdAsync(
            userId,
            limit,
            cursor,
            cancellationToken,
            currentUserId);
    }

    public Task<CursorPageResponse<PostResponse>> SearchAsync(
        string query,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _postQueryService.SearchAsync(
            query,
            pagination,
            cancellationToken,
            currentUserId);
    }

    public Task<PostResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _postQueryService.GetByIdAsync(id, cancellationToken, currentUserId);
    }

    public Task<PostResponse> CreateAsync(
        Guid authorId,
        CreatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        return _postManagementService.CreateAsync(authorId, request, cancellationToken);
    }

    public Task<PostResponse> UpdateAsync(
        Guid id,
        Guid currentUserId,
        UpdatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        return _postManagementService.UpdateAsync(id, currentUserId, request, cancellationToken);
    }

    public Task<PostViewResponse?> RecordViewAsync(
        Guid id,
        Guid viewerId,
        CancellationToken cancellationToken = default)
    {
        return _postInteractionService.RecordViewAsync(id, viewerId, cancellationToken);
    }

    public Task DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return _postManagementService.DeleteAsync(id, currentUserId, cancellationToken);
    }
}
