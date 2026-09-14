using AutoMapper;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Locations;
using Threads.Application.DTOs.Users;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Bookmarks;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Reposts;
using Threads.Domain.Entities;

namespace Threads.Application.Services;

public class CommentService : ICommentService
{
    private readonly IBookmarkRepository _bookmarkRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly ILikeRepository _likeRepository;
    private readonly IPostRepository _postRepository;
    private readonly IRepostRepository _repostRepository;
    private readonly IObjectStorageService _objectStorageService;
    private readonly IMapper _mapper;

    public CommentService(
        ICommentRepository commentRepository,
        IBookmarkRepository bookmarkRepository,
        ILikeRepository likeRepository,
        IPostRepository postRepository,
        IRepostRepository repostRepository,
        IObjectStorageService objectStorageService,
        IMapper mapper)
    {
        _bookmarkRepository = bookmarkRepository;
        _commentRepository = commentRepository;
        _likeRepository = likeRepository;
        _postRepository = postRepository;
        _repostRepository = repostRepository;
        _objectStorageService = objectStorageService;
        _mapper = mapper;
    }

    public async Task<IReadOnlyCollection<CommentResponse>> GetByPostIdAsync(
        Guid postId,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var comments = await _commentRepository.GetByPostIdAsync(postId, cancellationToken);
        var viewCounts = await GetViewCountsAsync(comments, cancellationToken);

        return comments
            .Select(comment => MapCommentResponse(
                comment,
                currentUserId,
                viewCounts.GetValueOrDefault(comment.Id)))
            .ToList();
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

        return MapCommentResponse(comment, currentUserId, viewsCount);
    }

    public async Task<IReadOnlyCollection<CommentResponse>> GetBookmarkedByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var comments = await _commentRepository.GetBookmarkedByUserIdAsync(userId, cancellationToken);
        var viewCounts = await GetViewCountsAsync(comments, cancellationToken);

        return comments
            .Select(comment => MapCommentResponse(
                comment,
                currentUserId,
                viewCounts.GetValueOrDefault(comment.Id),
                comment.Bookmarks.FirstOrDefault(bookmark => bookmark.UserId == userId)?.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyCollection<CommentResponse>> GetLikedByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var comments = await _commentRepository.GetLikedByUserIdAsync(userId, cancellationToken);
        var viewCounts = await GetViewCountsAsync(comments, cancellationToken);

        return comments
            .Select(comment => MapCommentResponse(
                comment,
                currentUserId,
                viewCounts.GetValueOrDefault(comment.Id),
                comment.Likes.FirstOrDefault(like => like.UserId == userId)?.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyCollection<CommentResponse>> GetRepostedByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var comments = await _commentRepository.GetRepostedByUserIdAsync(userId, cancellationToken);
        var viewCounts = await GetViewCountsAsync(comments, cancellationToken);

        return comments
            .Select(comment => MapCommentResponse(
                comment,
                currentUserId,
                viewCounts.GetValueOrDefault(comment.Id),
                comment.Reposts.FirstOrDefault(repost => repost.UserId == userId)?.CreatedAt))
            .ToList();
    }

    public async Task<CommentResponse> CreateAsync(
        Guid authorId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new RequestValidationException("Comment content is required.");
        }

        var post = await _postRepository.GetByIdAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            throw new NotFoundException("Post was not found.");
        }

        Comment? parentComment = null;

        if (request.ParentCommentId.HasValue)
        {
            parentComment = await _commentRepository.GetByIdAsync(request.ParentCommentId.Value, cancellationToken);

            if (parentComment is null)
            {
                throw new NotFoundException("Parent comment was not found.");
            }

            if (parentComment.PostId != request.PostId)
            {
                throw new RequestValidationException("Parent comment does not belong to the specified post.");
            }
        }

        var comment = _mapper.Map<Comment>(request);
        comment.AuthorId = authorId;
        comment.Content = request.Content.Trim();
        comment.ParentCommentId = parentComment?.Id;

        await _commentRepository.AddAsync(comment, cancellationToken);

        var createdComment = await _commentRepository.GetByIdAsync(comment.Id, cancellationToken);

        return MapCommentResponse(createdComment ?? comment, authorId, viewsCount: 0);
    }

    public async Task<CommentResponse?> UpdateAsync(
        Guid id,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new RequestValidationException("Comment content is required.");
        }

        var comment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        if (comment is null)
        {
            return null;
        }

        comment.Content = request.Content.Trim();
        comment.UpdatedAt = DateTimeOffset.UtcNow;

        await _commentRepository.UpdateAsync(comment, cancellationToken);

        var updatedComment = await _commentRepository.GetByIdAsync(comment.Id, cancellationToken);
        var viewsCount = await GetViewCountAsync(comment.Id, cancellationToken);

        return MapCommentResponse(updatedComment ?? comment, currentUserId, viewsCount);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var comment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        if (comment is null)
        {
            return false;
        }

        await _commentRepository.DeleteAsync(comment, cancellationToken);

        return true;
    }

    public async Task<CommentResponse?> LikeAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
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
            var like = new Like
            {
                UserId = userId,
                PostId = null,
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

        return await GetResponseByIdAsync(id, userId, cancellationToken);
    }

    public async Task<CommentResponse?> UnlikeAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
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

        return await GetResponseByIdAsync(id, userId, cancellationToken);
    }

    public async Task<CommentResponse?> BookmarkAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
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
            var bookmark = new Bookmark
            {
                UserId = userId,
                PostId = null,
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

        return await GetResponseByIdAsync(id, userId, cancellationToken);
    }

    public async Task<CommentResponse?> UnbookmarkAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
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

        return await GetResponseByIdAsync(id, userId, cancellationToken);
    }

    public async Task<CommentResponse?> RepostAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
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
            var repost = new Repost
            {
                UserId = userId,
                PostId = null,
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

        return await GetResponseByIdAsync(id, userId, cancellationToken);
    }

    public async Task<CommentResponse?> UnrepostAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
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

        return await GetResponseByIdAsync(id, userId, cancellationToken);
    }

    public async Task<CommentResponse?> ViewAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var viewsCount = await _commentRepository.RecordViewAsync(id, userId, cancellationToken);

        if (viewsCount is null)
        {
            return null;
        }

        var updatedComment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        return updatedComment is null
            ? null
            : MapCommentResponse(updatedComment, userId, viewsCount.Value);
    }

    private async Task<CommentResponse?> GetResponseByIdAsync(
        Guid commentId,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var comment = await _commentRepository.GetByIdAsync(commentId, cancellationToken);

        if (comment is null)
        {
            return null;
        }

        var viewsCount = await GetViewCountAsync(commentId, cancellationToken);

        return MapCommentResponse(comment, currentUserId, viewsCount);
    }

    private async Task<IReadOnlyDictionary<Guid, int>> GetViewCountsAsync(
        IReadOnlyCollection<Comment> comments,
        CancellationToken cancellationToken)
    {
        return await _commentRepository.GetViewCountsAsync(
            comments.Select(comment => comment.Id).ToArray(),
            cancellationToken);
    }

    private async Task<int> GetViewCountAsync(Guid commentId, CancellationToken cancellationToken)
    {
        var viewCounts = await _commentRepository.GetViewCountsAsync([commentId], cancellationToken);

        return viewCounts.GetValueOrDefault(commentId);
    }

    private CommentResponse MapCommentResponse(
        Comment comment,
        Guid? currentUserId,
        int viewsCount,
        DateTimeOffset? actionAt = null)
    {
        var response = _mapper.Map<CommentResponse>(comment);

        return new CommentResponse
        {
            Id = response.Id,
            PostId = response.PostId,
            ParentCommentId = response.ParentCommentId,
            Content = response.Content,
            Author = MapUserShortResponse(comment.Author),
            LikesCount = response.LikesCount,
            IsLikedByCurrentUser = currentUserId.HasValue &&
                comment.Likes.Any(like => like.UserId == currentUserId.Value),
            RepliesCount = response.RepliesCount,
            IsBookmarkedByCurrentUser = currentUserId.HasValue &&
                comment.Bookmarks.Any(bookmark => bookmark.UserId == currentUserId.Value),
            RepostsCount = response.RepostsCount,
            IsRepostedByCurrentUser = currentUserId.HasValue &&
                comment.Reposts.Any(repost => repost.UserId == currentUserId.Value),
            ViewsCount = viewsCount,
            ActionAt = actionAt,
            CreatedAt = response.CreatedAt,
            UpdatedAt = response.UpdatedAt
        };
    }

    private static bool IsDuplicateWriteException(Exception exception)
    {
        return exception.GetType().Name == "DbUpdateException";
    }

    private UserShortResponse MapUserShortResponse(User user)
    {
        var response = _mapper.Map<UserShortResponse>(user);

        return new UserShortResponse
        {
            Id = response.Id,
            Username = response.Username,
            DisplayName = response.DisplayName,
            Location = MapLocation(user),
            AvatarUrl = string.IsNullOrWhiteSpace(user.AvatarObjectKey)
                ? null
                : _objectStorageService.GetReadUrl(user.AvatarObjectKey),
            IsVerified = response.IsVerified
        };
    }

    private static LocationResponse? MapLocation(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Location))
        {
            return null;
        }

        return new LocationResponse
        {
            Id = string.IsNullOrWhiteSpace(user.LocationPlaceId)
                ? user.Location
                : user.LocationPlaceId,
            Name = user.Location,
            Country = user.LocationCountry ?? string.Empty,
            Latitude = user.LocationLatitude ?? 0,
            Longitude = user.LocationLongitude ?? 0
        };
    }
}
