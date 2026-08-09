namespace Threads.Application.Interfaces.Likes;

public interface ILikeService
{
    Task<bool> AddLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
    Task<bool> RemoveLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default);
}
