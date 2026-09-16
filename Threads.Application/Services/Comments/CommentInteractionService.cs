using Threads.Application.DTOs.Comments;
using Threads.Application.Interfaces.Comments;

namespace Threads.Application.Services.Comments;

public sealed class CommentInteractionService
{
    private readonly ICommentRepository _commentRepository;

    public CommentInteractionService(ICommentRepository commentRepository)
    {
        _commentRepository = commentRepository;
    }

    public async Task<CommentViewResponse?> RecordViewAsync(
        Guid id,
        Guid viewerId,
        CancellationToken cancellationToken = default)
    {
        var viewsCount = await _commentRepository.RecordViewAsync(id, viewerId, cancellationToken);

        return viewsCount is null
            ? null
            : new CommentViewResponse
            {
                CommentId = id,
                ViewsCount = viewsCount.Value
            };
    }
}
