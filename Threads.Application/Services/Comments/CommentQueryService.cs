using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Services.Common;
using Threads.Domain.Entities;

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
            cancellationToken);
        var hasMore = comments.Count > pagination.Limit;
        var pageComments = comments.Take(pagination.Limit).ToList();
        var viewCounts = await GetViewCountsAsync(pageComments, cancellationToken);
        var items = pageComments
            .Select(comment => _responseFactory.Create(
                comment,
                currentUserId,
                viewCounts.GetValueOrDefault(comment.Id)))
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
        var comment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        if (comment is null)
        {
            return null;
        }

        var viewsCount = await GetViewCountAsync(id, cancellationToken);

        return _responseFactory.Create(comment, currentUserId, viewsCount);
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
            cancellationToken);
        var viewCounts = await GetViewCountsAsync(comments, cancellationToken);

        return comments
            .Select(comment => _responseFactory.Create(
                comment,
                currentUserId,
                viewCounts.GetValueOrDefault(comment.Id),
                comment.CommentBookmarks.FirstOrDefault(bookmark => bookmark.UserId == userId)?.CreatedAt))
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
            cancellationToken);
        var viewCounts = await GetViewCountsAsync(comments, cancellationToken);

        return comments
            .Select(comment => _responseFactory.Create(
                comment,
                currentUserId,
                viewCounts.GetValueOrDefault(comment.Id),
                comment.CommentLikes.FirstOrDefault(like => like.UserId == userId)?.CreatedAt))
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
            cancellationToken);
        var viewCounts = await GetViewCountsAsync(comments, cancellationToken);

        return comments
            .Select(comment => _responseFactory.Create(
                comment,
                currentUserId,
                viewCounts.GetValueOrDefault(comment.Id),
                comment.CommentReposts.FirstOrDefault(repost => repost.UserId == userId)?.CreatedAt))
            .ToList();
    }

    public async Task<int> GetViewCountAsync(
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var viewCounts = await _commentRepository.GetViewCountsAsync([commentId], cancellationToken);

        return viewCounts.GetValueOrDefault(commentId);
    }

    private async Task<IReadOnlyDictionary<Guid, int>> GetViewCountsAsync(
        IReadOnlyCollection<Comment> comments,
        CancellationToken cancellationToken)
    {
        return await _commentRepository.GetViewCountsAsync(
            comments.Select(comment => comment.Id).ToArray(),
            cancellationToken);
    }
}
