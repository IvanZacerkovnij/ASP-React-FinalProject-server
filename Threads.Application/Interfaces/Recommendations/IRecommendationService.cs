using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Users;
using Threads.Application.DTOs.Recommendations;

namespace Threads.Application.Interfaces.Recommendations;

public interface IRecommendationService
{
    Task<TrendRecommendationsResponse> GetTrendsAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PostResponse>> GetFeedAsync(Guid? userId, int count, CancellationToken cancellationToken = default);
    Task<CursorPageResponse<PostResponse>> GetPostsAsync(Guid userId, CursorPageRequest request, CancellationToken cancellationToken = default);
    Task<CursorPageResponse<UserShortResponse>> GetUsersAsync(Guid userId, CursorPageRequest request, CancellationToken cancellationToken = default);
}
