using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Services.Common;

namespace Threads.Application.Services.Comments;

public sealed class CommentQueryService
{
    private readonly ICommentRepository _commentRepository;
    private readonly CommentResponseFactory _responseFactory;

    public CommentQueryService(
        ICommentRepository commentRepository,
        CommentResponseFactory responseFactory)
    {
        _commentRepository = commentRepository;
        _responseFactory = responseFactory;
    }

    public async Task<CursorPageResponse<CommentResponse>> GetByPostIdAsync(
        Guid postId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var cursor = CursorCodec.Decode(pagination.Cursor);
        var comments = await _commentRepository.GetByPostIdAsync(
            postId,
            pagination.Limit,
            cursor,
            currentUserId,
            cancellationToken);
        var hasMore = comments.Count > pagination.Limit;
        var pageComments = comments.Take(pagination.Limit).ToList();
        var items = pageComments
            .Select(_responseFactory.Create)
            .ToList();

        return new CursorPageResponse<CommentResponse>
        {
            Items = items,
            HasMore = hasMore,
            NextCursor = hasMore
                ? CursorCodec.Encode(pageComments[^1].CreatedAt, pageComments[^1].Id)
                : null
        };
    }

    public async Task<CommentResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var comment = await _commentRepository.GetSummaryByIdAsync(
            id,
            currentUserId,
            cancellationToken);

        if (comment is null)
        {
            return null;
        }

        return _responseFactory.Create(comment);
    }

    public async Task<IReadOnlyCollection<CommentResponse>> GetBookmarkedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var comments = await _commentRepository.GetBookmarkedByUserIdAsync(
            userId,
            limit,
            cursor,
            currentUserId,
            cancellationToken);

        return comments
            .Select(_responseFactory.Create)
            .ToList();
    }

    public async Task<IReadOnlyCollection<CommentResponse>> GetLikedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var comments = await _commentRepository.GetLikedByUserIdAsync(
            userId,
            limit,
            cursor,
            currentUserId,
            cancellationToken);

        return comments
            .Select(_responseFactory.Create)
            .ToList();
    }

    public async Task<IReadOnlyCollection<CommentResponse>> GetRepostedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var comments = await _commentRepository.GetRepostedByUserIdAsync(
            userId,
            limit,
            cursor,
            currentUserId,
            cancellationToken);

        return comments
            .Select(_responseFactory.Create)
            .ToList();
    }

}
