using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Reposts;
using Threads.Domain.Entities;

namespace Threads.Application.Services;

public class RepostService : IRepostService
{
    private readonly IRepostRepository _repostRepository;
    private readonly IPostRepository _postRepository;

    public RepostService(IRepostRepository repostRepository, IPostRepository postRepository)
    {
        _repostRepository = repostRepository;
        _postRepository = postRepository;
    }

    public async Task<bool> AddRepostAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetByIdAsync(postId, cancellationToken);

        if (post is null)
        {
            return false;
        }

        var existingRepost = await _repostRepository.GetByUserAndPostAsync(userId, postId, cancellationToken);

        if (existingRepost is not null)
        {
            return false;
        }

        post.RepostsCount++;

        var repost = new Repost
        {
            UserId = userId,
            PostId = postId,
            CommentId = null
        };

        try
        {
            await _repostRepository.AddAsync(repost, cancellationToken);
        }
        catch (Exception exception) when (IsDuplicateWriteException(exception))
        {
            post.RepostsCount--;
            return false;
        }

        return true;
    }

    public async Task<bool> RemoveRepostAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var repost = await _repostRepository.GetByUserAndPostAsync(userId, postId, cancellationToken);

        if (repost is null)
        {
            return false;
        }

        var post = await _postRepository.GetByIdAsync(postId, cancellationToken);

        if (post is not null && post.RepostsCount > 0)
        {
            post.RepostsCount--;
        }

        await _repostRepository.DeleteAsync(repost, cancellationToken);

        return true;
    }

    private static bool IsDuplicateWriteException(Exception exception)
    {
        return exception.GetType().Name == "DbUpdateException";
    }
}
