using Threads.Application.DTOs.ScheduledPosts;

namespace Threads.Application.Interfaces.ScheduledPosts;

public interface IScheduledPostService
{
    Task<IReadOnlyCollection<ScheduledPostResponse>> GetAsync(
        Guid authorId,
        CancellationToken cancellationToken = default);
    Task<ScheduledPostResponse> CreateAsync(
        Guid authorId,
        CreateScheduledPostRequest request,
        CancellationToken cancellationToken = default);
    Task<ScheduledPostResponse> UpdateAsync(
        Guid id,
        Guid currentUserId,
        UpdateScheduledPostRequest request,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
    Task<int> PublishDueAsync(CancellationToken cancellationToken = default);
}
