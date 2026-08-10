namespace Threads.Application.Interfaces.Reposts;

public interface IRepostService
{
    Task<bool> AddRepostAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
    Task<bool> RemoveRepostAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
}
