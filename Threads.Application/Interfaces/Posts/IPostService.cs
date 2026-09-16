using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.DTOs.Posts.Responses;

namespace Threads.Application.Interfaces.Posts;

public interface IPostService
{
    Task<IReadOnlyCollection<PostResponse>> GetFeedAsync(CancellationToken cancellationToken = default, Guid? currentUserId = null);
    Task<CursorPageResponse<PostResponse>> GetByAuthorIdAsync(
        Guid authorId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<IReadOnlyCollection<PostResponse>> GetLikedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<IReadOnlyCollection<PostResponse>> GetBookmarkedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<IReadOnlyCollection<PostResponse>> GetRepostedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<CursorPageResponse<PostResponse>> SearchAsync(
        string query,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null);
    Task<PostResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, Guid? currentUserId = null);
    Task<PostResponse> CreateAsync(Guid authorId, CreatePostRequest request, CancellationToken cancellationToken = default);
    Task<PostResponse> UpdateAsync(
        Guid id,
        Guid currentUserId,
        UpdatePostRequest request,
        CancellationToken cancellationToken = default);
    Task<PostViewResponse?> RecordViewAsync(Guid id, Guid viewerId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken = default);
}
