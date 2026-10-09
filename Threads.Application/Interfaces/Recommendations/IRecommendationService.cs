using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Users;

namespace Threads.Application.Interfaces.Recommendations;

public interface IRecommendationService
{
    Task<IReadOnlyCollection<PostResponse>> GetFeedAsync(Guid? userId, int count, CancellationToken cancellationToken = default);
    Task<CursorPageResponse<PostResponse>> GetPostsAsync(Guid userId, CursorPageRequest request, CancellationToken cancellationToken = default);
    Task<CursorPageResponse<UserShortResponse>> GetUsersAsync(Guid userId, CursorPageRequest request, CancellationToken cancellationToken = default);
}
