using Threads.Application.DTOs.Users;
using Threads.Application.DTOs.Recommendations;

namespace Threads.Application.Interfaces.Recommendations;

public interface IRecommendationRepository
{
    Task<IReadOnlyCollection<TrendReadModel>> GetTrendsAsync(int limit, DateTimeOffset asOf, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Guid>> GetFeedIdsAsync(Guid? userId, int count, DateTimeOffset asOf, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Guid>> RankPostsAsync(Guid userId, DateTimeOffset asOf, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Guid>> RankUsersAsync(Guid userId, DateTimeOffset asOf, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Guid>> GetAvailableIdsAsync(Guid userId, string kind, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UserSummaryReadModel>> GetUsersAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
