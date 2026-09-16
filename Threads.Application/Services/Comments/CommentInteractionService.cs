using Threads.Application.DTOs.Comments;
using Threads.Application.Interfaces.Bookmarks;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Interfaces.Reposts;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Comments;

public sealed class CommentInteractionService
{
    private readonly IBookmarkRepository _bookmarkRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly ILikeRepository _likeRepository;
    private readonly IRepostRepository _repostRepository;
    private readonly CommentQueryService _commentQueryService;
    private readonly CommentResponseFactory _responseFactory;

    public CommentInteractionService(
        ICommentRepository commentRepository,
        IBookmarkRepository bookmarkRepository,
        ILikeRepository likeRepository,
        IRepostRepository repostRepository,
        CommentQueryService commentQueryService,
        CommentResponseFactory responseFactory)
    {
        _bookmarkRepository = bookmarkRepository;
        _commentRepository = commentRepository;
        _likeRepository = likeRepository;
        _repostRepository = repostRepository;
        _commentQueryService = commentQueryService;
        _responseFactory = responseFactory;
    }

    public async Task<CommentResponse?> LikeAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var comment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        if (comment is null)
        {
            return null;
        }

        var existingLike = await _likeRepository.GetByUserAndCommentAsync(
            userId,
            id,
            cancellationToken);

        if (existingLike is null)
        {
            var like = new CommentLike
            {
                UserId = userId,
                CommentId = id
            };

            try
            {
                await _likeRepository.AddAsync(like, cancellationToken);
            }
            catch (Exception exception) when (IsDuplicateWriteException(exception))
            {
            }
        }

        return await _commentQueryService.GetByIdAsync(id, cancellationToken, userId);
    }

    public async Task<CommentResponse?> UnlikeAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var comment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        if (comment is null)
        {
            return null;
        }

        var existingLike = await _likeRepository.GetByUserAndCommentAsync(
            userId,
            id,
            cancellationToken);

        if (existingLike is not null)
        {
            await _likeRepository.DeleteAsync(existingLike, cancellationToken);
        }

        return await _commentQueryService.GetByIdAsync(id, cancellationToken, userId);
    }

    public async Task<CommentResponse?> BookmarkAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var comment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        if (comment is null)
        {
            return null;
        }

        var existingBookmark = await _bookmarkRepository.GetByUserAndCommentId(
            userId,
            id,
            cancellationToken);

        if (existingBookmark is null)
        {
            var bookmark = new CommentBookmark
            {
                UserId = userId,
                CommentId = id
            };

            try
            {
                await _bookmarkRepository.AddAsync(bookmark, cancellationToken);
            }
            catch (Exception exception) when (IsDuplicateWriteException(exception))
            {
            }
        }

        return await _commentQueryService.GetByIdAsync(id, cancellationToken, userId);
    }

    public async Task<CommentResponse?> UnbookmarkAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var comment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        if (comment is null)
        {
            return null;
        }

        var existingBookmark = await _bookmarkRepository.GetByUserAndCommentId(
            userId,
            id,
            cancellationToken);

        if (existingBookmark is not null)
        {
            await _bookmarkRepository.DeleteAsync(existingBookmark, cancellationToken);
        }

        return await _commentQueryService.GetByIdAsync(id, cancellationToken, userId);
    }

    public async Task<CommentResponse?> RepostAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var comment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        if (comment is null)
        {
            return null;
        }

        var existingRepost = await _repostRepository.GetByUserAndCommentAsync(
            userId,
            id,
            cancellationToken);

        if (existingRepost is null)
        {
            var repost = new CommentRepost
            {
                UserId = userId,
                CommentId = id
            };

            try
            {
                await _repostRepository.AddAsync(repost, cancellationToken);
            }
            catch (Exception exception) when (IsDuplicateWriteException(exception))
            {
            }
        }

        return await _commentQueryService.GetByIdAsync(id, cancellationToken, userId);
    }

    public async Task<CommentResponse?> UnrepostAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var comment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        if (comment is null)
        {
            return null;
        }

        var existingRepost = await _repostRepository.GetByUserAndCommentAsync(
            userId,
            id,
            cancellationToken);

        if (existingRepost is not null)
        {
            await _repostRepository.DeleteAsync(existingRepost, cancellationToken);
        }

        return await _commentQueryService.GetByIdAsync(id, cancellationToken, userId);
    }

    public async Task<CommentResponse?> ViewAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var viewsCount = await _commentRepository.RecordViewAsync(id, userId, cancellationToken);

        if (viewsCount is null)
        {
            return null;
        }

        var updatedComment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        return updatedComment is null
            ? null
            : _responseFactory.Create(updatedComment, userId, viewsCount.Value);
    }

    private static bool IsDuplicateWriteException(Exception exception)
    {
        return exception.GetType().Name == "DbUpdateException";
    }
}
