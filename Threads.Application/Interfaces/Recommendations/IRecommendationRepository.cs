using Threads.Application.DTOs.Users;

namespace Threads.Application.Interfaces.Recommendations;

public interface IRecommendationRepository
{
    Task<IReadOnlyCollection<Guid>> GetFeedIdsAsync(Guid? userId, int count, DateTimeOffset asOf, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Guid>> RankPostsAsync(Guid userId, DateTimeOffset asOf, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Guid>> RankUsersAsync(Guid userId, DateTimeOffset asOf, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Guid>> GetAvailableIdsAsync(Guid userId, string kind, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UserSummaryReadModel>> GetUsersAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
