using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Services.Common;

namespace Threads.Application.Services.Comments;

public sealed class CommentThreadService
{
    private readonly ICommentRepository _commentRepository;
    private readonly IPostService _postService;
    private readonly CommentResponseFactory _responseFactory;

    public CommentThreadService(
        ICommentRepository commentRepository,
        IPostService postService,
        CommentResponseFactory responseFactory)
    {
        _commentRepository = commentRepository;
        _postService = postService;
        _responseFactory = responseFactory;
    }

    public async Task<CommentThreadResponse?> GetAsync(
        Guid commentId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        var target = await _commentRepository.GetSummaryByIdAsync(
            commentId,
            currentUserId,
            cancellationToken);

        if (target is null)
        {
            return null;
        }

        var post = await _postService.GetByIdAsync(
            target.PostId,
            cancellationToken,
            currentUserId);

        if (post is null)
        {
            return null;
        }

        var ancestors = await _commentRepository.GetAncestorsAsync(
            commentId,
            currentUserId,
            cancellationToken);
        var cursor = CursorCodec.Decode(pagination.Cursor);
        var replies = await _commentRepository.GetRepliesAsync(
            commentId,
            pagination.Limit,
            cursor,
            currentUserId,
            cancellationToken);
        var hasMore = replies.Count > pagination.Limit;
        var pageReplies = replies.Take(pagination.Limit).ToList();

        return new CommentThreadResponse
        {
            Post = post,
            Ancestors = ancestors.Select(_responseFactory.Create).ToList(),
            Target = _responseFactory.Create(target),
            Replies = new CursorPageResponse<CommentResponse>
            {
                Items = pageReplies.Select(_responseFactory.Create).ToList(),
                HasMore = hasMore,
                NextCursor = hasMore
                    ? CursorCodec.Encode(pageReplies[^1].CreatedAt, pageReplies[^1].Id)
                    : null
            }
        };
    }
}
