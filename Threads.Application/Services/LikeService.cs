using Threads.Application.Interfaces.Likes;
using Threads.Application.Interfaces.Posts;
using Threads.Domain.Entities;

namespace Threads.Application.Services;

public class LikeService : ILikeService
{
    private readonly ILikeRepository _likeRepository;
    private readonly IPostRepository _postRepository;

    public LikeService(ILikeRepository likeRepository, IPostRepository postRepository)
    {
        _likeRepository = likeRepository;
        _postRepository = postRepository;
    }

    public async Task<bool> AddLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetByIdAsync(postId, cancellationToken);

        if (post is null)
        {
            return false;
        }

        var existingLike = await _likeRepository.GetByUserAndPostAsync(
            userId,
            postId,
            cancellationToken);

        if (existingLike is not null)
        {
            return false;
        }

        var like = new Like
        {
            UserId = userId,
            PostId = postId
        };

        try
        {
            await _likeRepository.AddAsync(like, cancellationToken);
        }
        catch (Exception exception) when (IsDuplicateWriteException(exception))
        {
            return false;
        }

        return true;
    }

    public async Task<bool> RemoveLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var existingLike = await _likeRepository.GetByUserAndPostAsync(
            userId,
            postId,
            cancellationToken);

        if (existingLike is null)
        {
            return false;
        }

        await _likeRepository.DeleteAsync(existingLike, cancellationToken);

        return true;
    }

    private static bool IsDuplicateWriteException(Exception exception)
    {
        return exception.GetType().Name == "DbUpdateException";
    }
}
