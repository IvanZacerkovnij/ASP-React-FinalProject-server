using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.Interfaces.Posts;

namespace Threads.Application.Services.Posts;

public sealed class PostInteractionService
{
    private readonly IPostRepository _postRepository;

    public PostInteractionService(IPostRepository postRepository)
    {
        _postRepository = postRepository;
    }

    public async Task<PostViewResponse?> RecordViewAsync(
        Guid id,
        Guid viewerId,
        CancellationToken cancellationToken = default)
    {
        var viewsCount = await _postRepository.RecordViewAsync(id, viewerId, cancellationToken);

        return viewsCount is null
            ? null
            : new PostViewResponse
            {
                PostId = id,
                ViewsCount = viewsCount.Value
            };
    }
}
